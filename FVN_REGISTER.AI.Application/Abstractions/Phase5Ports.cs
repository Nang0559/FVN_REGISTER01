using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Application.Abstractions;

public interface IGoldenDatasetAdministration
{
    Task EvaluateCaseAsync(string tenantCode, Guid caseId, Guid? releaseId, string actualJson, bool passed,
        string? failureReason, CancellationToken cancellationToken = default);
}
