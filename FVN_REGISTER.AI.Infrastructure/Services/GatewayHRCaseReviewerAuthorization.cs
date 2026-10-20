using FVN_REGISTER.AI.Application.Contracts;
using Microsoft.Extensions.Configuration;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>
/// The AI API is a private service behind the authenticated FVN_REGISTER API gateway.
/// The gateway performs function-level and employee managed-scope checks before forwarding
/// case reads or review commands. Enable this adapter only when network access and secrets
/// are restricted to that gateway.
/// </summary>
public sealed class GatewayHRCaseReviewerAuthorization(IConfiguration configuration) : IHRCaseReviewerAuthorization
{
    public Task<bool> CanReviewCaseAsync(string tenantCode, string actorId, string employeeCode,
        HRCaseType caseType, CancellationToken cancellationToken = default)
    {
        var trusted = configuration.GetValue<bool>("AIPlatform:TrustFvnRegisterGatewayForHRReview");
        var signingKey = configuration["AIPlatform:ContextSigningKey"];
        var hasValidSigningKey = !string.IsNullOrWhiteSpace(signingKey) && signingKey.Length >= 32;
        return Task.FromResult(trusted && hasValidSigningKey &&
            !string.IsNullOrWhiteSpace(tenantCode) &&
            !string.IsNullOrWhiteSpace(actorId) &&
            !string.IsNullOrWhiteSpace(employeeCode));
    }
}
