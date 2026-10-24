using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Application boundary for HR Assistant. Business facts and authorization remain owned by FVN_REGISTER.</summary>
public sealed class HRCaseAssistantAdapter(IHRReconciliationService reconciliation) : IHRCaseAssistantAdapter
{
    public Task<HRReconciliationAnalysis> AnalyzeAsync(string tenantCode, string actorId,
        HRCaseAssistantRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return reconciliation.AnalyzeWithAuthorizedFactsAsync(
            tenantCode,
            actorId,
            new AuthorizedHRCaseAnalysisRequest(request.Request, request.Facts),
            cancellationToken);
    }
}
