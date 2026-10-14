using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Services;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FVN_REGISTER.AI.Infrastructure.Providers;

namespace FVN_REGISTER.AI.Tests;

public sealed class HRReconciliationTests
{
    [Fact]
    public async Task AnalyzeAsync_FlagsDifferenceBetweenReportedAndConfirmedOvertime()
    {
        var facts = new OfficialHRFacts("E0001", new DateOnly(2026, 10, 1), "Approved",
            null, null, 2m, null, null, 1.5m, 1.5m, "HC", false,
            null, null, "FVN_REGISTER", "OT-REQ-001", DateTimeOffset.UtcNow);
        await using var db = CreateDb();
        var service = new HRReconciliationService(new StubFactsProvider(facts), db, new UnconfiguredAIModelProvider());

        var result = await service.AnalyzeAsync("FCCVN", "hr-user",
            new HRCaseAnalysisRequest(HRCaseType.Overtime, "E0001", new DateOnly(2026, 10, 1),
                "Tôi đăng ký 2 giờ OT nhưng hệ thống chỉ ghi nhận 1,5 giờ.", null, "evidence-1",
                ReportedHours: 2m));

        Assert.Equal(HRComparisonOutcome.Discrepancy, result.Outcome);
        Assert.Contains(result.Comparisons, x => x.RuleCode == "OT-ACTUAL-001" &&
            x.Outcome == HRComparisonOutcome.Discrepancy);
        Assert.True(result.RequiresHumanReview);
        Assert.Equal("OT-REQ-001", result.OfficialRecordReference);
        Assert.Single(db.HRCases);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsFactsForDifferentEmployee()
    {
        var facts = new OfficialHRFacts("E0002", new DateOnly(2026, 10, 1), "Approved",
            null, null, 2m, null, null, 2m, 2m, "HC", false,
            null, null, "FVN_REGISTER", "OT-REQ-002", DateTimeOffset.UtcNow);
        await using var db = CreateDb();
        var service = new HRReconciliationService(new StubFactsProvider(facts), db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync("FCCVN", "hr-user",
            new HRCaseAnalysisRequest(HRCaseType.Overtime, "E0001", new DateOnly(2026, 10, 1),
                "Check my OT", null, null)));
    }


    [Fact]
    public async Task AnalyzeAsync_UsesTenantAndUploaderScopedExtractionCandidate()
    {
        var facts = new OfficialHRFacts("E0001", new DateOnly(2026, 10, 1), "Approved",
            null, null, 2m, null, null, 1.5m, 1.5m, "HC", false,
            null, null, "FVN_REGISTER", "OT-REQ-001", DateTimeOffset.UtcNow);
        await using var db = CreateDb();
        var documentId = Guid.NewGuid();
        db.Documents.Add(new AiDocumentRecord
        {
            Id = documentId, TenantCode = "FCCVN", UploadedBy = "employee-1",
            FileName = "ot-proof.txt", Extension = ".txt", MediaType = "text/plain",
            Sha256 = new string('a', 64), StorageKey = "tenant/doc.txt", Status = "AwaitingHumanReview"
        });
        db.Extractions.Add(new AiExtractionRecord
        {
            Id = Guid.NewGuid(), DocumentId = documentId, TenantCode = "FCCVN",
            DocumentType = "HR.OvertimeEvidence", ClassificationConfidence = 0.8m,
            CandidateJson = """{"extractedFields":{"hourCandidates":["2"]}}""",
            ParserVersion = "test-v1", CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new HRReconciliationService(new StubFactsProvider(facts), db);

        var result = await service.AnalyzeAsync("FCCVN", "employee-1",
            new HRCaseAnalysisRequest(HRCaseType.Overtime, "E0001", new DateOnly(2026, 10, 1),
                "Tôi phản hồi số giờ OT.", null, null, EvidenceDocumentId: documentId));

        Assert.Contains(result.EvidenceReferences, x => x == $"ai-document:{documentId}");
        Assert.Contains(result.Comparisons, x => x.RuleCode == "OT-ACTUAL-001" &&
            x.Outcome == HRComparisonOutcome.Discrepancy);
    }

    private static AiPlatformDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AiPlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class StubFactsProvider(OfficialHRFacts facts) : IAuthorizedHRFactsProvider
    {
        public Task<OfficialHRFacts?> GetFactsAsync(string tenantCode, string actorId, HRCaseType caseType,
            string? requestedEmployeeCode, DateOnly workDate, CancellationToken cancellationToken = default)
            => Task.FromResult<OfficialHRFacts?>(facts);
    }
}
