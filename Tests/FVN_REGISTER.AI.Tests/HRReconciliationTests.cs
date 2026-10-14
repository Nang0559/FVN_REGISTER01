using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Services;

namespace FVN_REGISTER.AI.Tests;

public sealed class HRReconciliationTests
{
    [Fact]
    public async Task AnalyzeAsync_FlagsDifferenceBetweenReportedAndConfirmedOvertime()
    {
        var facts = new OfficialHRFacts("E0001", new DateOnly(2026, 10, 1), "Approved",
            null, null, 2m, null, null, 1.5m, 1.5m, "HC", false,
            null, null, "FVN_REGISTER", "OT-REQ-001", DateTimeOffset.UtcNow);
        var service = new HRReconciliationService(new StubFactsProvider(facts));

        var result = await service.AnalyzeAsync("FCCVN", "hr-user",
            new HRCaseAnalysisRequest(HRCaseType.Overtime, "E0001", new DateOnly(2026, 10, 1),
                "Tôi đăng ký 2 giờ OT nhưng hệ thống chỉ ghi nhận 1,5 giờ.", null, "evidence-1",
                ReportedHours: 2m));

        Assert.Equal(HRComparisonOutcome.Discrepancy, result.Outcome);
        Assert.Contains(result.Comparisons, x => x.RuleCode == "OT-ACTUAL-001" &&
            x.Outcome == HRComparisonOutcome.Discrepancy);
        Assert.True(result.RequiresHumanReview);
        Assert.Equal("OT-REQ-001", result.OfficialRecordReference);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsFactsForDifferentEmployee()
    {
        var facts = new OfficialHRFacts("E0002", new DateOnly(2026, 10, 1), "Approved",
            null, null, 2m, null, null, 2m, 2m, "HC", false,
            null, null, "FVN_REGISTER", "OT-REQ-002", DateTimeOffset.UtcNow);
        var service = new HRReconciliationService(new StubFactsProvider(facts));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync("FCCVN", "hr-user",
            new HRCaseAnalysisRequest(HRCaseType.Overtime, "E0001", new DateOnly(2026, 10, 1),
                "Check my OT", null, null)));
    }

    private sealed class StubFactsProvider(OfficialHRFacts facts) : IAuthorizedHRFactsProvider
    {
        public Task<OfficialHRFacts?> GetFactsAsync(string tenantCode, string actorId, HRCaseType caseType,
            string? requestedEmployeeCode, DateOnly workDate, CancellationToken cancellationToken = default)
            => Task.FromResult<OfficialHRFacts?>(facts);
    }
}
