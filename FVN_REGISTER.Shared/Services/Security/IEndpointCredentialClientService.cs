using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Contract.Responses;

namespace FVN_REGISTER.Shared.Services.Security;

public interface IEndpointCredentialClientService
{
    Task<ApiResponse<EndpointCredentialStatusDto>> GetStatusAsync(string deviceKey, CancellationToken ct = default);
    Task<ApiResponse<EndpointCredentialStatusDto>> GetEquipmentStatusAsync(int equipmentAssetId, CancellationToken ct = default);
    Task<ApiResponse<IReadOnlyList<EndpointCredentialHistoryDto>>> GetEquipmentHistoryAsync(int equipmentAssetId, CancellationToken ct = default);
    Task<ApiResponse<EndpointCredentialProvisionResult>> ProvisionForEquipmentAsync(int equipmentAssetId, CancellationToken ct = default);
    Task<ApiResponse<object>> RevokeForEquipmentAsync(int equipmentAssetId, CancellationToken ct = default);
    Task<ApiResponse<EndpointCredentialProvisionResult>> ProvisionAsync(EndpointCredentialProvisionDto request, CancellationToken ct = default);
    Task<ApiResponse<object>> RevokeAsync(string deviceKey, CancellationToken ct = default);
}
