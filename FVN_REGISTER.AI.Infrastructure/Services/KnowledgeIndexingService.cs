using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Human-controlled publication boundary for RAG.</summary>
public sealed class KnowledgeIndexingService(
    AiPlatformDbContext db,
    IOriginalDocumentStore documents,
    IDocumentTextExtractor textExtractor,
    IEmbeddingGenerator embeddings,
    IVectorKnowledgeStore vectorStore) : IKnowledgeIndexingService
{
    private const int MaximumChunkCharacters = 6000;
    private const int ChunkOverlapCharacters = 500;
    private const int MaximumEmbeddingConcurrency = 8;

    public async Task<int> PublishApprovedDocumentAsync(string tenantCode, Guid documentId, Guid reviewId,
        IReadOnlyCollection<string> accessScopes, CancellationToken cancellationToken = default)
    {
        tenantCode=Required(tenantCode,64); ArgumentNullException.ThrowIfNull(accessScopes);
        if(accessScopes.Count is <1 or >50||accessScopes.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>128))throw new ArgumentException("At least one valid access scope is required.",nameof(accessScopes));
        var document=await db.Documents.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==documentId&&x.TenantCode==tenantCode&&x.Status=="Approved",cancellationToken)??throw new KeyNotFoundException("Approved document was not found for this tenant.");
        var review=await db.Reviews.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==reviewId&&x.DocumentId==documentId&&x.TenantCode==tenantCode&&(x.Decision=="Approved"||x.Decision=="Corrected"),cancellationToken)??throw new UnauthorizedAccessException("A matching human-approved review is required before publication.");

        string text;
        if(review.Decision=="Corrected"&&!string.IsNullOrWhiteSpace(review.CorrectedPayloadJson))
        {
            text="Human-approved corrected payload:\n"+review.CorrectedPayloadJson;
        }
        else
        {
            // Publish the text the reviewer actually approved: the representation stored by the worker.
            // Re-running OCR here made publishing of scanned/large PDFs slow enough to time out the HTTP request.
            var storedRepresentation=await db.Extractions.AsNoTracking().Where(x=>x.DocumentId==documentId&&x.TenantCode==tenantCode).OrderByDescending(x=>x.CreatedAtUtc).Select(x=>x.RepresentationJson).FirstOrDefaultAsync(cancellationToken);
            text=TryReadRepresentationText(storedRepresentation)??string.Empty;
            if(string.IsNullOrWhiteSpace(text))
            {
                await using var original=await documents.OpenReadAsync(document.StorageKey,cancellationToken);
                text=await textExtractor.ExtractTextAsync(original,document.MediaType,cancellationToken);
            }
        }
        if(string.IsNullOrWhiteSpace(text))throw new InvalidDataException("KNOWLEDGE_DOCUMENT_HAS_NO_TEXT");
        if(text.Length>2_000_000)throw new InvalidDataException("KNOWLEDGE_DOCUMENT_TEXT_LIMIT_EXCEEDED");
        var pieces=SplitIntoChunks(text,MaximumChunkCharacters,ChunkOverlapCharacters);
        if(pieces.Count is <1 or >1000)throw new InvalidDataException("KNOWLEDGE_CHUNK_COUNT_OUT_OF_RANGE");

        var scopeJson=JsonSerializer.Serialize(accessScopes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        var chunks=new List<KnowledgeChunk>(pieces.Count);
        for(var index=0;index<pieces.Count;index++)
        {
            var piece=pieces[index];var id=StableChunkId(documentId,reviewId,index,piece.Text);var metadata=new Dictionary<string,string>{{"reviewId",review.Id.ToString()},{"chunkOrdinal",index.ToString()},{"pageNumber",piece.PageNumber?.ToString()??""},{"sectionPath",piece.SectionPath??""},{"accessScopes",scopeJson}};
            chunks.Add(new KnowledgeChunk(id.ToString(),documentId.ToString(),piece.Text,metadata));
        }

        var vectorArray=new EmbeddingVector[pieces.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0,pieces.Count),new ParallelOptions{MaxDegreeOfParallelism=MaximumEmbeddingConcurrency,CancellationToken=cancellationToken},async(index,ct)=>{vectorArray[index]=await embeddings.GenerateAsync(pieces[index].Text,ct);});
        var vectors=vectorArray.ToList();

        await using var transaction=IsInMemory()?null:await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,cancellationToken);
        try
        {
            // Re-check the approval boundary after potentially long extraction/embedding work and
            // inside the publication transaction. A review revoked while embeddings were generated
            // must never be published using the earlier, stale approval snapshot.
            var stillApproved = await db.Documents.AsNoTracking().AnyAsync(
                x => x.Id == documentId && x.TenantCode == tenantCode && x.Status == "Approved",
                cancellationToken);
            var stillReviewed = await db.Reviews.AsNoTracking().AnyAsync(
                x => x.Id == reviewId && x.DocumentId == documentId && x.TenantCode == tenantCode &&
                     (x.Decision == "Approved" || x.Decision == "Corrected"),
                cancellationToken);
            if (!stillApproved || !stillReviewed)
                throw new UnauthorizedAccessException("Document approval or human review changed before knowledge publication.");

            var existing=await db.KnowledgeChunks.Where(x=>x.TenantCode==tenantCode&&x.DocumentId==documentId).ToDictionaryAsync(x=>x.ChunkOrdinal,cancellationToken);
            foreach(var item in existing.Values)item.IsActive=false;
            await db.SaveChangesAsync(cancellationToken);
            await vectorStore.UpsertAsync(tenantCode,chunks,vectors,cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if(transaction is not null)await transaction.CommitAsync(cancellationToken);return chunks.Count;
        }
        catch{if(transaction is not null)await transaction.RollbackAsync(CancellationToken.None);throw;}
    }

    public async Task UnpublishDocumentAsync(string tenantCode,Guid documentId,CancellationToken cancellationToken=default)
    {
        tenantCode=Required(tenantCode,64);await using var transaction=IsInMemory()?null:await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,cancellationToken);
        try{var rows=await db.KnowledgeChunks.Where(x=>x.TenantCode==tenantCode&&x.DocumentId==documentId&&x.IsActive).ToListAsync(cancellationToken);foreach(var row in rows)row.IsActive=false;await db.SaveChangesAsync(cancellationToken);if(transaction is not null)await transaction.CommitAsync(cancellationToken);}catch{if(transaction is not null)await transaction.RollbackAsync(CancellationToken.None);throw;}
    }

    private bool IsInMemory()=>string.Equals(db.Database.ProviderName,"Microsoft.EntityFrameworkCore.InMemory",StringComparison.Ordinal);
    private static Guid StableChunkId(Guid documentId,Guid reviewId,int ordinal,string text){var bytes=SHA256.HashData(Encoding.UTF8.GetBytes($"{documentId:N}|{reviewId:N}|{ordinal}|{text}"));Span<byte> guidBytes=stackalloc byte[16];bytes.AsSpan(0,16).CopyTo(guidBytes);return new Guid(guidBytes);}
    private static string? TryReadRepresentationText(string? representationJson)
    {
        if(string.IsNullOrWhiteSpace(representationJson))return null;
        try
        {
            var representation=JsonSerializer.Deserialize<DocumentRepresentation>(representationJson,new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var value=representation?.ToModelText();
            return string.IsNullOrWhiteSpace(value)?null:value;
        }
        catch(JsonException){return null;}
    }
    private static List<TextChunk> SplitIntoChunks(string text,int maximumCharacters,int overlapCharacters)
    {
        var result=new List<TextChunk>();var buffer=new StringBuilder();int? page=null;string? section=null;var currentPage=1;
        foreach(var raw in text.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))
        {
            var line=FVN_REGISTER.AI.Infrastructure.Extraction.OcrTextMarkers.StripLine(raw.Trim());if(line.Length==0)continue;
            if(line.StartsWith("[PAGE ",StringComparison.Ordinal)&&line.EndsWith(']')&&int.TryParse(line.AsSpan(6,line.Length-7),out var pageNumber)){Flush(false);currentPage=pageNumber;continue;}
            if(line.Length>maximumCharacters){Flush(false);for(var offset=0;offset<line.Length;offset+=maximumCharacters-overlapCharacters){var length=Math.Min(maximumCharacters,line.Length-offset);result.Add(new TextChunk(line.Substring(offset,length),currentPage,section));if(offset+length>=line.Length)break;}continue;}
            if(buffer.Length>0&&buffer.Length+line.Length+1>maximumCharacters)Flush(true);
            if(buffer.Length==0)page=currentPage;if(line.Length<=180&&(line.EndsWith(':')||line.StartsWith('#')))section=line;if(buffer.Length>0)buffer.AppendLine();buffer.Append(line);
        }
        Flush(false);return result;
        void Flush(bool keepOverlap){if(buffer.Length==0)return;var value=buffer.ToString();result.Add(new TextChunk(value,page,section));buffer.Clear();if(keepOverlap&&overlapCharacters>0){var tail=value[^Math.Min(overlapCharacters,value.Length)..];buffer.Append(tail);}page=keepOverlap?page:null;}
    }
    private static string Required(string value,int max)=>!string.IsNullOrWhiteSpace(value)&&value.Trim().Length<=max?value.Trim():throw new ArgumentException($"Value must contain 1 to {max} characters.");
    private sealed record TextChunk(string Text,int? PageNumber,string? SectionPath);
}
