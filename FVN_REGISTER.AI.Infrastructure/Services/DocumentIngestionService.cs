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
                await store.DeleteAsync(stored.StorageKey, ct);
                var oldJob = await db.IngestionJobs.SingleOrDefaultAsync(x => x.DocumentId == duplicate.Id, ct);
                if (oldJob is not null && oldJob.Status is "DeadLetter" or "Failed")
                {
                    oldJob.Status = "Queued";
                    oldJob.AttemptCount = 0;
                    oldJob.NextAttemptAtUtc = DateTimeOffset.UtcNow;
                    oldJob.StartedAtUtc = null;
                    oldJob.LastAttemptAtUtc = null;
                    oldJob.LastErrorAtUtc = null;
                    oldJob.LeaseOwner = null;
                    oldJob.LeaseUntilUtc = null;
                    oldJob.CompletedAtUtc = null;
                    oldJob.FailureCode = null;
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
            await store.DeleteAsync(stored.StorageKey, CancellationToken.None);
            var old = await db.Documents.AsNoTracking().FirstOrDefaultAsync(
                x => x.TenantCode == tenant && x.DedupActive && x.Sha256 == stored.Sha256 &&
                     x.ModuleCode == moduleCode && x.SchemaCode == schemaCode, ct);
            if (old is null) throw;
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
