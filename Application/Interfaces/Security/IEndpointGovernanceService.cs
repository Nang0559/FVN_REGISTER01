using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Application.Interfaces.Security;

/// <summary>
/// Canonical Endpoint Governance boundary. Inventory collection remains separate.
/// Catalog changes use RequestModule.Endpoint and the existing Common Approval infrastructure.
/// </summary>
public interface IEndpointGovernanceService
{
    Task<ServiceResult<IReadOnlyList<EndpointGovernancePolicyDto>>> GetPoliciesAsync(
        EndpointGovernanceItemType? itemType,
        EndpointTargetType? targetType,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<EndpointGovernancePolicyDto>> GetPolicyAsync(
        int policyId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<EndpointGovernancePolicyDto>> CreateVersionAsync(
        EndpointGovernancePolicyUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<EndpointGovernancePolicyDto>> SubmitPolicyAsync(
        int policyId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<EndpointGovernancePolicyDto>> PublishAsync(
        int policyId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<EndpointGovernanceRequestDto>> CreateRequestAsync(
        EndpointGovernanceRequestCreateDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<EndpointGovernanceRequestDto>>> GetRequestsAsync(
        bool mineOnly,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<EndpointGovernanceRequestDto>> ReviewAsync(
        int requestId,
        bool approved,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<EndpointComplianceFindingDto>>> GetFindingsAsync(
        long? endpointDeviceId,
        bool openOnly,
        CancellationToken cancellationToken = default);
}
