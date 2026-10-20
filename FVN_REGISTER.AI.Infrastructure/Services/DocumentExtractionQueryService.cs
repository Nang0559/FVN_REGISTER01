using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace FVN_REGISTER.AI.Infrastructure.Services;
public sealed class DocumentExtractionQueryService(AiPlatformDbContext db) : IDocumentExtractionQueryService
{
 public async Task<ExtractionReviewView?> GetLatestAsync(Guid documentId,string tenantCode,string actorId,CancellationToken cancellationToken=default)
 {
  var row=await db.Extractions.AsNoTracking().Where(x=>x.DocumentId==documentId&&x.TenantCode==tenantCode&&db.Documents.Any(d=>d.Id==x.DocumentId&&d.TenantCode==tenantCode&&d.UploadedBy==actorId)).OrderByDescending(x=>x.CreatedAtUtc).Select(x=>new{x.Id,x.DocumentId,x.DocumentType,x.ClassificationConfidence,x.CandidateJson,x.ParserVersion,x.ModelProvider,x.ModelVersion,x.PromptVersion,x.CreatedAtUtc,x.RepresentationJson}).FirstOrDefaultAsync(cancellationToken);
  if(row is null)return null;
  return new ExtractionReviewView(row.Id,row.DocumentId,row.DocumentType,(double)row.ClassificationConfidence,row.CandidateJson??"{}",row.ParserVersion,row.ModelProvider,row.ModelVersion,row.PromptVersion,row.CreatedAtUtc,row.RepresentationJson);
 }
}
