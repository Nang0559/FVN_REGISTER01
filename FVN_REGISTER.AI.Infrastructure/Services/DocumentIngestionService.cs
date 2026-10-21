using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.AI.Infrastructure.Services;

public sealed class DocumentIngestionService(
    AiPlatformDbContext db,
    IOriginalDocumentStore store,
    IUploadContentValidator validator,
    IOptions<AIPlatformOptions> options) : IDocumentIngestionService
{
    public async Task<IngestionReceipt> IngestAsync(
        string tenant, string actor, string fileName, Stream content,
        CancellationToken ct = default, string? moduleCode = null, string? schemaCode = null,
        string sourceType = DocumentSourceTypes.Upload, string? sourceReference = null,
        DateTimeOffset? retentionUntilUtc = null)
    {
        tenant = Required(tenant, 64);
        actor = Required(actor, 128);
        sourceType = Required(sourceType, 32);
        if (!DocumentSourceTypes.IsSupported(sourceType))
            throw new ArgumentException($"Unsupported document source type '{sourceType}'.", nameof(sourceType));
        if ((moduleCode is null) != (schemaCode is null))
            throw new ArgumentException("ModuleCode and SchemaCode must be supplied together.");
        moduleCode = moduleCode is null ? null : Required(moduleCode, 64);
        schemaCode = schemaCode is null ? null : Required(schemaCode, 128);
        sourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : Required(sourceReference, 512);
        if (retentionUntilUtc.HasValue && retentionUntilUtc.Value <= DateTimeOffset.UtcNow)
            throw new ArgumentException("RetentionUntilUtc must be in the future.", nameof(retentionUntilUtc));

        var valid = await validator.ValidateAsync(content, fileName, ct);
        var max = (long)Math.Clamp(options.Value.MaximumFileSizeMb, 1, 250) * 1024 * 1024;
        var id = Guid.NewGuid();
        var stored = await store.SaveAsync(id, new LimitStream(content, max), ct);
        try
        {
            var duplicate = await db.Documents.FirstOrDefaultAsync(
                x => x.TenantCode == tenant && x.DedupActive && x.Sha256 == stored.Sha256 &&
                     x.ModuleCode == moduleCode && x.SchemaCode == schemaCode, ct);
            if (duplicate is not null)
            {
                var oldJob = await db.IngestionJobs.SingleOrDefaultAsync(x => x.DocumentId == duplicate.Id, ct);

                // Deduplication must not discard the only healthy copy when the existing
                // document metadata points to a blob that has already disappeared.
                if (!await StoredContentExistsAsync(duplicate.StorageKey, ct))
                {
                    duplicate.StorageKey = stored.StorageKey;
                    duplicate.Length = stored.Length;

                    if (oldJob is null)
                    {
                        oldJob = new AiIngestionJobRecord
                        {
                            Id = Guid.NewGuid(),
                            DocumentId = duplicate.Id,
                            Status = "Queued",
                            CreatedAtUtc = DateTimeOffset.UtcNow
                        };
                        db.IngestionJobs.Add(oldJob);
                        duplicate.Status = "Quarantined";
                    }
                    else if (oldJob.Status is "DeadLetter" or "Failed" or "Running")
                    {
                        ResetJobForRetry(oldJob);
                        duplicate.Status = "Quarantined";
                    }

                    await db.SaveChangesAsync(ct);
                    return new(duplicate.Id, oldJob.Id, duplicate.FileName, duplicate.Length,
                        duplicate.Sha256, duplicate.Status, true);
                }

                // Existing blob is healthy: discard only the newly uploaded duplicate.
                await store.DeleteAsync(stored.StorageKey, ct);
                if (oldJob is not null && oldJob.Status is "DeadLetter" or "Failed")
                {
                    ResetJobForRetry(oldJob);
                    duplicate.Status = "Quarantined";
                    await db.SaveChangesAsync(ct);
                }

                return new(duplicate.Id, oldJob?.Id ?? Guid.Empty, duplicate.FileName, duplicate.Length,
                    duplicate.Sha256, duplicate.Status, true);
            }

            var job = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            db.Documents.Add(new AiDocumentRecord
            {
                Id = id,
                TenantCode = tenant,
                FileName = valid.FileName,
                Extension = valid.Extension,
                MediaType = valid.MediaType,
                Length = stored.Length,
                Sha256 = stored.Sha256,
                StorageKey = stored.StorageKey,
                UploadedBy = actor,
                ModuleCode = moduleCode,
                SchemaCode = schemaCode,
                SourceType = sourceType,
                SourceReference = sourceReference ?? valid.FileName,
                RetentionUntilUtc = retentionUntilUtc,
                ReceivedAtUtc = now,
                Status = "Quarantined",
                DedupActive = true
            });
            db.IngestionJobs.Add(new AiIngestionJobRecord
            {
                Id = job, DocumentId = id, Status = "Queued", CreatedAtUtc = now
            });
            await db.SaveChangesAsync(ct);
            return new(id, job, valid.FileName, stored.Length, stored.Sha256, "Quarantined", false);
        }
        catch (DbUpdateException)
        {
            var old = await db.Documents.AsNoTracking().FirstOrDefaultAsync(
                x => x.TenantCode == tenant && x.DedupActive && x.Sha256 == stored.Sha256 &&
                     x.ModuleCode == moduleCode && x.SchemaCode == schemaCode, ct);
            if (old is null)
            {
                await store.DeleteAsync(stored.StorageKey, CancellationToken.None);
                throw;
            }

            if (!await StoredContentExistsAsync(old.StorageKey, ct))
            {
                // A concurrent upload won the filtered unique index, but its old metadata
                // references a missing blob. Repoint that metadata to this verified copy.
                db.ChangeTracker.Clear();
                var repair = await db.Documents.SingleAsync(x => x.Id == old.Id, ct);
                repair.StorageKey = stored.StorageKey;
                repair.Length = stored.Length;

                var repairJob = await db.IngestionJobs.SingleOrDefaultAsync(x => x.DocumentId == old.Id, ct);
                if (repairJob is null)
                {
                    repairJob = new AiIngestionJobRecord
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = old.Id,
                        Status = "Queued",
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    };
                    db.IngestionJobs.Add(repairJob);
                    repair.Status = "Quarantined";
                }
                else if (repairJob.Status is "DeadLetter" or "Failed" or "Running")
                {
                    ResetJobForRetry(repairJob);
                    repair.Status = "Quarantined";
                }

                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch
                {
                    await store.DeleteAsync(stored.StorageKey, CancellationToken.None);
                    throw;
                }

                return new(repair.Id, repairJob.Id, repair.FileName, repair.Length,
                    repair.Sha256, repair.Status, true);
            }

            await store.DeleteAsync(stored.StorageKey, CancellationToken.None);
            var job = await db.IngestionJobs.AsNoTracking().FirstOrDefaultAsync(x => x.DocumentId == old.Id, ct);
            return new(old.Id, job?.Id ?? Guid.Empty, old.FileName, old.Length, old.Sha256, old.Status, true);
        }
        catch
        {
            await store.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<DocumentStatusResult?> GetStatusAsync(Guid id, string tenant, CancellationToken ct = default)
    {
        tenant = Required(tenant, 64);
        var doc = await db.Documents.AsNoTracking()
            .Where(x => x.Id == id && x.TenantCode == tenant)
            .Select(x => new { x.Id, x.FileName, x.Status, x.ReceivedAtUtc })
            .SingleOrDefaultAsync(ct);
        if (doc is null) return null;
        var job = await db.IngestionJobs.AsNoTracking()
            .Where(x => x.DocumentId == id)
            .Select(x => new { x.Id, x.Status, x.FailureCode })
            .SingleOrDefaultAsync(ct);
        return new(doc.Id, doc.FileName, doc.Status, doc.ReceivedAtUtc, job?.Id, job?.Status, job?.FailureCode);
    }

    private async Task<bool> StoredContentExistsAsync(string storageKey, CancellationToken ct)
    {
        try
        {
            await using var stream = await store.OpenReadAsync(storageKey, ct);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static void ResetJobForRetry(AiIngestionJobRecord job)
    {
        job.Status = "Queued";
        job.AttemptCount = 0;
        job.NextAttemptAtUtc = DateTimeOffset.UtcNow;
        job.StartedAtUtc = null;
        job.LastAttemptAtUtc = null;
        job.LastErrorAtUtc = null;
        job.LeaseOwner = null;
        job.LeaseUntilUtc = null;
        job.CompletedAtUtc = null;
        job.FailureCode = null;
    }

    private static string Required(string value, int max) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length > max
            ? throw new ArgumentException($"Value must contain 1 to {max} characters.")
            : value.Trim();

    private sealed class LimitStream(Stream inner, long limit) : Stream
    {
        private long read;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => read; set => throw new NotSupportedException(); }
        public override int Read(byte[] b, int o, int c) => ReadAsync(b.AsMemory(o, c)).AsTask().GetAwaiter().GetResult();
        public override async ValueTask<int> ReadAsync(Memory<byte> b, CancellationToken ct = default)
        {
            var remaining = limit - read;
            if (remaining < 0) throw new InvalidDataException("Upload exceeds configured size.");
            var allowed = (int)Math.Min(b.Length, remaining + 1);
            var n = await inner.ReadAsync(b[..allowed], ct);
            read += n;
            if (read > limit) throw new InvalidDataException("Upload exceeds configured size.");
            return n;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long o, SeekOrigin so) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }
}
