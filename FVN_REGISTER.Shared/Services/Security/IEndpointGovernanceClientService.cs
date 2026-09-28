using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Enums;
using Microsoft.AspNetCore.Components.Forms;

namespace FVN_REGISTER.Shared.Services.Security;

public interface IEndpointGovernanceClientService
{
    Task<ApiResponse<List<EndpointGovernancePolicyDto>>> GetPoliciesAsync(EndpointGovernanceItemType? itemType = null, EndpointTargetType? targetType = null, CancellationToken ct = default);
    Task<ApiResponse<EndpointGovernancePolicyDto>> CreateVersionAsync(EndpointGovernancePolicyUpsertRequest request, CancellationToken ct = default);
    Task<ApiResponse<EndpointGovernancePolicyDto>> SubmitAsync(int policyId, CancellationToken ct = default);
    Task<ApiResponse<EndpointGovernancePolicyDto>> PublishAsync(int policyId, CancellationToken ct = default);
    Task<ApiResponse<EndpointGovernanceExcelPreviewDto>> PreviewExcelAsync(IBrowserFile file, EndpointGovernanceItemType itemType, EndpointTargetType targetType, CancellationToken ct = default);
    Task<ApiResponse<EndpointGovernancePolicyDto>> ImportExcelAsync(IBrowserFile file, string policyCode, string policyName, EndpointGovernanceItemType itemType, EndpointTargetType targetType, string? remark, CancellationToken ct = default);
    Task<ApiResponse<List<EndpointGovernanceRequestDto>>> GetRequestsAsync(bool mineOnly = true, CancellationToken ct = default);
    Task<ApiResponse<EndpointGovernanceRequestDto>> CreateRequestAsync(EndpointGovernanceRequestCreateDto request, CancellationToken ct = default);
    Task<ApiResponse<EndpointGovernanceRequestDto>> ReviewAsync(int requestId, bool approved, string? comment = null, CancellationToken ct = default);
    Task<ApiResponse<List<EndpointComplianceFindingDto>>> GetFindingsAsync(long? endpointDeviceId = null, bool openOnly = true, CancellationToken ct = default);
}
