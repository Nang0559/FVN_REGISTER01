using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Responses;

namespace FVN_REGISTER.Shared.Services.Security;

public interface IEndpointInventoryClientService
{
    Task<ApiResponse<List<EndpointInventorySummaryDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResponse<EndpointInventorySummaryDto>> GetAsync(string deviceKey, CancellationToken ct = default);
    Task<ApiResponse<int>> EvaluateAsync(long endpointDeviceId, CancellationToken ct = default);
    Task<ApiResponse<bool>> LinkEquipmentAsync(long endpointDeviceId, int equipmentAssetId, CancellationToken ct = default);
}
