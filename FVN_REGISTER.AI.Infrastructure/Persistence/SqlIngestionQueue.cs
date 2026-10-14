using FVN_REGISTER.AI.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
namespace FVN_REGISTER.AI.Infrastructure.Persistence;
public sealed class SqlIngestionQueue(AiPlatformDbContext db):IIngestionQueue
{
 public async Task EnqueueAsync(Guid id,CancellationToken ct=default)
 {
  if(await db.IngestionJobs.AnyAsync(x=>x.DocumentId==id,ct))return;
  db.IngestionJobs.Add(new AiIngestionJobRecord{Id=Guid.NewGuid(),DocumentId=id,Status="Queued",CreatedAtUtc=DateTimeOffset.UtcNow});
  await db.SaveChangesAsync(ct);
 }
}