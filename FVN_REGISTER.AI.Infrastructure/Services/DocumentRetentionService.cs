using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FVN_REGISTER.AI.Infrastructure.Services;

public sealed class DocumentRetentionService(AiPlatformDbContext db,IOriginalDocumentStore store):IDocumentRetentionService
{
 private const int BatchSize=100;

 public async Task<DocumentRetentionResult> ArchiveExpiredAsync(DateTimeOffset nowUtc,CancellationToken cancellationToken=default)
 {
  var transaction = db.Database.IsInMemory() ? null : await db.Database.BeginTransactionAsync(cancellationToken);
  try
  {
   if (transaction is not null)
   {
    await db.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource=N'FVN_AI:RETENTION_PURGE', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=0;", cancellationToken);
   }

   var documents=await db.Documents.Where(x=>x.RetentionUntilUtc.HasValue&&x.RetentionUntilUtc<=nowUtc&&x.Status!="Archived").OrderBy(x=>x.RetentionUntilUtc).Take(BatchSize).ToListAsync(cancellationToken);
   var archived=0;var purged=0;
   foreach(var document in documents)
   {
    cancellationToken.ThrowIfCancellationRequested();var storageKey=document.StorageKey;
    try{if(!storageKey.StartsWith("retained://",StringComparison.OrdinalIgnoreCase)){await store.DeleteAsync(storageKey,cancellationToken);purged++;}}
    catch(FileNotFoundException){ }
    catch(DirectoryNotFoundException){ }

    var knowledge = await db.KnowledgeChunks.Where(x=>x.TenantCode==document.TenantCode&&x.DocumentId==document.Id).ToListAsync(cancellationToken);
    foreach(var chunk in knowledge)
    {
        chunk.IsActive=false;
        chunk.Content=null;
        chunk.EmbeddingJson=null;
    }
    var extractions = await db.Extractions.Where(x=>x.TenantCode==document.TenantCode&&x.DocumentId==document.Id).ToListAsync(cancellationToken);
    foreach(var extraction in extractions)
    {
        extraction.CandidateJson=null;
        extraction.RepresentationJson=null;
    }

    document.DedupActive=false;
    document.Status="Archived";
    document.StorageKey=$"retained://purged/{document.Id:N}";
    document.ContentPurgedAtUtc=nowUtc;
    archived++;
   }
   if(archived>0)await db.SaveChangesAsync(cancellationToken);
   if(transaction is not null) await transaction.CommitAsync(cancellationToken);
   return new(archived,purged,nowUtc);
  }
  catch
  {
   if(transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
   throw;
  }
  finally
  {
   if(transaction is not null) await transaction.DisposeAsync();
  }
 }
}
