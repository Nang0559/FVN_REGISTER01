using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Safe default. Replace with a signed FVN_REGISTER backend adapter before enabling HR case analysis.</summary>
public sealed class UnconfiguredAuthorizedHRFactsProvider : IAuthorizedHRFactsProvider
{
    public Task<OfficialHRFacts?> GetFactsAsync(string tenantCode, string actorId, HRCaseType caseType,
        string? requestedEmployeeCode, DateOnly workDate, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("HR_FACTS_PROVIDER_NOT_CONFIGURED: configure the trusted FVN_REGISTER adapter before enabling reconciliation.");
}
