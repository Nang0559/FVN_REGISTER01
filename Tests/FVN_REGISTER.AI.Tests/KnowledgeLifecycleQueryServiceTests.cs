using FVN_REGISTER.AI.Infrastructure.Persistence;
using FVN_REGISTER.AI.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Tests;

public sealed class KnowledgeLifecycleQueryServiceTests
{
    [Fact]
    public async Task GetStatusAsync_DoesNotReportPublishedWhenHumanReviewWasRevoked()
    {
        await using var db = CreateDb();
        var documentId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        db.Documents.Add(new AiDocumentRecord
        {
            Id = documentId, TenantCode = "FCCVN", FileName = "policy.pdf", Extension = ".pdf",
            MediaType = "application/pdf", Sha256 = new string('a', 64), StorageKey = "policy/1",
            UploadedBy = "hr-1", Status = "Approved"
        });
        db.Reviews.Add(new AiReviewRecord
        {
            Id = reviewId, DocumentId = documentId, TenantCode = "FCCVN",
            ReviewerId = "hr-1", Decision = "Rejected", OriginalPayloadJson = "{}",
            ReviewedAtUtc = DateTimeOffset.UtcNow
        });
        db.KnowledgeChunks.Add(new AiKnowledgeChunkRecord
        {
            Id = Guid.NewGuid(), TenantCode = "FCCVN", DocumentId = documentId, ReviewId = reviewId,
            ChunkOrdinal = 0, Content = "Confidential policy text", ContentHash = new string('b', 64),
            AccessScopeJson = "[\"HR\"]", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await new KnowledgeLifecycleQueryService(db).GetStatusAsync(
            "FCCVN", "hr-1", documentId, new[] { "HR" });

        Assert.NotNull(result);
        Assert.Equal("Unpublished", result.Status);
        Assert.Equal(0, result.ActiveChunkCount);
    }

    [Fact]
    public async Task GetStatusAsync_RequiresApprovedDocumentAndMatchingApprovedReview()
    {
        await using var db = CreateDb();
        var documentId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        db.Documents.Add(new AiDocumentRecord
        {
            Id = documentId, TenantCode = "FCCVN", FileName = "policy.pdf", Extension = ".pdf",
            MediaType = "application/pdf", Sha256 = new string('c', 64), StorageKey = "policy/2",
            UploadedBy = "hr-1", Status = "Approved"
        });
        db.Reviews.Add(new AiReviewRecord
        {
            Id = reviewId, DocumentId = documentId, TenantCode = "FCCVN",
            ReviewerId = "hr-1", Decision = "Approved", OriginalPayloadJson = "{}",
            ReviewedAtUtc = DateTimeOffset.UtcNow
        });
        db.KnowledgeChunks.Add(new AiKnowledgeChunkRecord
        {
            Id = Guid.NewGuid(), TenantCode = "FCCVN", DocumentId = documentId, ReviewId = reviewId,
            ChunkOrdinal = 0, Content = "Approved policy text", ContentHash = new string('d', 64),
            AccessScopeJson = "[\"HR\"]", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new KnowledgeLifecycleQueryService(db);
        var result = await service.GetStatusAsync("FCCVN", "hr-1", documentId, new[] { "HR" });

        Assert.NotNull(result);
        Assert.Equal("Published", result.Status);
        Assert.Equal(1, result.ActiveChunkCount);
    }

    private static AiPlatformDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AiPlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
