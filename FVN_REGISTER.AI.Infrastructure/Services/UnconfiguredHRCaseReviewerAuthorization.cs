using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Fail-closed default. Only FVN_REGISTER can attest current HR review permission.</summary>
public sealed class UnconfiguredHRCaseReviewerAuthorization : IHRCaseReviewerAuthorization
{
    public Task<bool> CanReviewEmployeeAsync(string tenantCode, string actorId, string employeeCode, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
