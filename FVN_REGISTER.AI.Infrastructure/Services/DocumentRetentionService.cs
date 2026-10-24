using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

public sealed class DocumentRetentionService(
    AiPlatformDbContext db,
    IOriginalDocumentStore store) : IDocumentRetentionService
{
    private const int BatchSize = 100;
    private const string RetentionLockResource = "FVN_AI:RETENTION_PURGE";

    public Task<DocumentRetentionResult> ArchiveExpiredAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        // SQL Server's retrying execution strategy requires the complete transaction
        // to run inside ExecuteAsync. This also makes the transaction the retry unit.
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            // A failed attempt may have left entities tracked as modified. Start each
            // attempt with a clean tracker so a retry reloads the database state.
            db.ChangeTracker.Clear();

            await using var transaction = IsInMemory()
                ? null
                : await db.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                if (transaction is not null)
                {
                    await db.Database.ExecuteSqlRawAsync(
                        $"EXEC sp_getapplock @Resource=N'{RetentionLockResource}', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=0;",
                        cancellationToken);
                }

                var documents = await db.Documents
                    .Where(x => x.RetentionUntilUtc.HasValue
                        && x.RetentionUntilUtc <= nowUtc
                        && x.Status != "Archived")
                    .OrderBy(x => x.RetentionUntilUtc)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);

                var archived = 0;
                var purged = 0;

                foreach (var document in documents)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var storageKey = document.StorageKey;

                    try
                    {
                        if (!storageKey.StartsWith("retained://", StringComparison.OrdinalIgnoreCase))
                        {
                            await store.DeleteAsync(storageKey, cancellationToken);
                            purged++;
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        // Idempotent recovery: the file may have been removed by a
                        // previous interrupted attempt.
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // Treat an already-missing storage directory as purged.
                    }

                    var knowledge = await db.KnowledgeChunks
                        .Where(x => x.TenantCode == document.TenantCode && x.DocumentId == document.Id)
                        .ToListAsync(cancellationToken);

                    foreach (var chunk in knowledge)
                    {
                        chunk.IsActive = false;
                        chunk.Content = null;
                        chunk.EmbeddingJson = null;
                    }

                    var extractions = await db.Extractions
                        .Where(x => x.TenantCode == document.TenantCode && x.DocumentId == document.Id)
                        .ToListAsync(cancellationToken);

                    foreach (var extraction in extractions)
                    {
                        extraction.CandidateJson = null;
                        extraction.RepresentationJson = null;
                    }

                    document.DedupActive = false;
                    document.Status = "Archived";
                    document.StorageKey = $"retained://purged/{document.Id:N}";
                    document.ContentPurgedAtUtc = nowUtc;
                    archived++;
                }

                if (archived > 0)
                {
                    await db.SaveChangesAsync(cancellationToken);
                }

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return new DocumentRetentionResult(archived, purged, nowUtc);
            }
            catch
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }

                throw;
            }
        });
    }

    private bool IsInMemory() =>
        string.Equals(
            db.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);
}
