using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Application.Abstractions;
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
        var service = new HRReconciliationService(new StubFactsProvider(facts), db, new UnconfiguredAIModelProvider());

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
        var service = new HRReconciliationService(new StubFactsProvider(facts), db, new UnconfiguredAIModelProvider());

        var result = await service.AnalyzeAsync("FCCVN", "employee-1",
            new HRCaseAnalysisRequest(HRCaseType.Overtime, "E0001", new DateOnly(2026, 10, 1),
                "Tôi phản hồi số giờ OT.", null, null, EvidenceDocumentId: documentId));

        Assert.Contains(result.EvidenceReferences, x => x == $"ai-document:{documentId}");
        Assert.Contains("hourCandidates", Assert.Single(db.HRCases).EvidenceCandidateJson);
        Assert.Contains(result.Comparisons, x => x.RuleCode == "OT-ACTUAL-001" &&
            x.Outcome == HRComparisonOutcome.Discrepancy);
    }


    [Fact]
    public async Task AnalyzeWithAuthorizedFactsAsync_ReportsRegisteredVersusActualOtIntervalDifference()
    {
        var workDate = new DateOnly(2026, 10, 1);
        var facts = new OfficialHRFacts("E0001", workDate, "Approved",
            new DateTimeOffset(2026, 10, 1, 18, 0, 0, TimeSpan.FromHours(7)),
            new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.FromHours(7)),
            2m,
            new DateTimeOffset(2026, 10, 1, 18, 15, 0, TimeSpan.FromHours(7)),
            new DateTimeOffset(2026, 10, 1, 19, 45, 0, TimeSpan.FromHours(7)),
            1.5m, 1.5m, "HC", false, null, null, "FVN_REGISTER", "OT:REQ-001", DateTimeOffset.UtcNow);
        await using var db = CreateDb();
        var service = new HRReconciliationService(new StubFactsProvider(facts), db, new UnconfiguredAIModelProvider());

        var result = await service.AnalyzeWithAuthorizedFactsAsync("FCCVN", "42",
            new AuthorizedHRCaseAnalysisRequest(
                new HRCaseAnalysisRequest(HRCaseType.Overtime, "E0001", workDate, "OT thực tế bị thiếu giờ.", null, null, ReportedHours: 2m),
                facts));

        Assert.Contains(result.Comparisons, x => x.RuleCode == "OT-INTERVAL-001" && x.Outcome == HRComparisonOutcome.Discrepancy);
    }
    [Fact]
    public async Task AnalyzeWithAuthorizedFactsAsync_PersistsCaseWithoutCallingUnconfiguredFactsProvider()
    {
        var facts = new OfficialHRFacts("E0001", new DateOnly(2026, 10, 1), "Approved",
            null, null, 2m, null, null, 1.5m, 1.5m, "HC", false,
            null, null, "FVN_REGISTER.ReportDispatcher", "OT:REQ-001", DateTimeOffset.UtcNow);
        await using var db = CreateDb();
        var service = new HRReconciliationService(new StubFactsProvider(facts), db, new UnconfiguredAIModelProvider());

        var result = await service.AnalyzeWithAuthorizedFactsAsync("FCCVN", "42",
            new AuthorizedHRCaseAnalysisRequest(
                new HRCaseAnalysisRequest(HRCaseType.Overtime, "E0001", new DateOnly(2026, 10, 1),
                    "Tôi đăng ký 2 giờ nhưng công ghi nhận 1,5 giờ.", null, null, ReportedHours: 2m),
                facts));

        Assert.Equal(HRComparisonOutcome.Discrepancy, result.Outcome);
        Assert.Single(db.HRCases);
    }

    [Fact]
    public async Task AnalyzeAsync_FlagsEmployeeAndDateMismatchFoundInEvidence()
    {
        var workDate = new DateOnly(2026, 10, 1);
        var facts = new OfficialHRFacts("E0001", workDate, "Calculated", null, null, null,
            new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(7)),
            new DateTimeOffset(2026, 10, 1, 17, 0, 0, TimeSpan.FromHours(7)),
            8m, 0m, "HC", false, null, null, "FVN_REGISTER", "ATT:E0001:20261001", DateTimeOffset.UtcNow);
        await using var db = CreateDb();
        var documentId = Guid.NewGuid();
        db.Documents.Add(new AiDocumentRecord
        {
            Id = documentId, TenantCode = "FCCVN", UploadedBy = "employee-1",
            FileName = "attendance-proof.txt", Extension = ".txt", MediaType = "text/plain",
            Sha256 = new string('b', 64), StorageKey = "tenant/attendance.txt", Status = "AwaitingHumanReview"
        });
        db.Extractions.Add(new AiExtractionRecord
        {
            Id = Guid.NewGuid(), DocumentId = documentId, TenantCode = "FCCVN",
            DocumentType = "HR.AttendanceEvidence", ClassificationConfidence = 0.8m,
            CandidateJson = """{"extractedFields":{"employeeCode":"E0002","dateCandidates":["2026-10-02"],"timeCandidates":["08:00","17:00"]},"evidence":[]}""",
            ParserVersion = "test-v1", CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new HRReconciliationService(new StubFactsProvider(facts), db, new UnconfiguredAIModelProvider());

        var result = await service.AnalyzeAsync("FCCVN", "employee-1",
            new HRCaseAnalysisRequest(HRCaseType.Attendance, "E0001", workDate,
                "Giờ vào ra trong bảng công của tôi không đúng.", null, null, EvidenceDocumentId: documentId));

        Assert.Contains(result.Comparisons, x => x.RuleCode == "EVIDENCE-EMPLOYEE-001" && x.Outcome == HRComparisonOutcome.Discrepancy);
        Assert.Contains(result.Comparisons, x => x.RuleCode == "EVIDENCE-WORKDATE-001" && x.Outcome == HRComparisonOutcome.Discrepancy);
        Assert.Contains(result.Comparisons, x => x.RuleCode == "ATT-SOURCE-001" && x.Outcome == HRComparisonOutcome.Matched);
    }
    [Fact]
    public async Task ReviewAsync_PersistsFinalReplyAndImmutableReviewHistory()
    {
        var facts = new OfficialHRFacts("E0001", new DateOnly(2026, 10, 1), "Approved",
            null, null, 2m, null, null, 1.5m, 1.5m, "HC", false,
            null, null, "FVN_REGISTER", "OT-REQ-001", DateTimeOffset.UtcNow);
        await using var db = CreateDb();
        var analysis = await new HRReconciliationService(new StubFactsProvider(facts), db,
            new UnconfiguredAIModelProvider()).AnalyzeAsync("FCCVN", "42",
            new HRCaseAnalysisRequest(HRCaseType.Overtime, "E0001", new DateOnly(2026, 10, 1),
                "OT phản hồi", null, null, ReportedHours: 2m));
        var reviewer = new AllowReviewer();
        var service = new HRCaseReviewService(db, reviewer);

        var result = await service.ReviewAsync("FCCVN", "hr-1", analysis.CaseId,
            new HRCaseReviewRequest("Edited", "HR đã kiểm tra và sẽ phản hồi chính thức.", "Đã xác minh."));

        Assert.Equal("Approved", result.Status);
        Assert.Equal("Edited", result.Decision);
        Assert.Equal("HR đã kiểm tra và sẽ phản hồi chính thức.", db.HRCases.Single().FinalReply);
        Assert.Single(db.HRCaseReviews);
    }

    [Fact]
    public async Task ReviewAsync_RejectsWhenReviewerAuthorizationIsMissing()
    {
        var facts = new OfficialHRFacts("E0001", new DateOnly(2026, 10, 1), "Approved",
            null, null, 2m, null, null, 1.5m, 1.5m, "HC", false,
            null, null, "FVN_REGISTER", "OT-REQ-001", DateTimeOffset.UtcNow);
        await using var db = CreateDb();
        var analysis = await new HRReconciliationService(new StubFactsProvider(facts), db,
            new UnconfiguredAIModelProvider()).AnalyzeAsync("FCCVN", "42",
            new HRCaseAnalysisRequest(HRCaseType.Overtime, "E0001", new DateOnly(2026, 10, 1),
                "OT phản hồi", null, null, ReportedHours: 2m));
        var service = new HRCaseReviewService(db, new DenyReviewer());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReviewAsync("FCCVN", "intruder",
            analysis.CaseId, new HRCaseReviewRequest("Approved", "Không hợp lệ", null)));
    }

    [Fact]
    public async Task AnalyzeWithAuthorizedFactsAsync_RejectsUndefinedCaseType()
    {
        var workDate = new DateOnly(2026, 10, 1);
        var facts = new OfficialHRFacts("E0001", workDate, "Approved",
            null, null, 2m, null, null, 2m, 2m, "HC", false,
            null, null, "FVN_REGISTER", "OT:REQ-001", DateTimeOffset.UtcNow);
        await using var db = CreateDb();
        var service = new HRReconciliationService(new StubFactsProvider(facts), db, new UnconfiguredAIModelProvider());
        var request = new HRCaseAnalysisRequest((HRCaseType)999, "E0001", workDate,
            "Invalid case type", null, null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.AnalyzeWithAuthorizedFactsAsync("FCCVN", "42",
            new AuthorizedHRCaseAnalysisRequest(request, facts)));
        Assert.Empty(db.HRCases);
    }

    private sealed class AllowReviewer : IHRCaseReviewerAuthorization
    {
        public Task<bool> CanReviewCaseAsync(string tenantCode, string actorId, string employeeCode,
            HRCaseType caseType, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class DenyReviewer : IHRCaseReviewerAuthorization
    {
        public Task<bool> CanReviewCaseAsync(string tenantCode, string actorId, string employeeCode,
            HRCaseType caseType, CancellationToken cancellationToken = default) => Task.FromResult(false);
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
