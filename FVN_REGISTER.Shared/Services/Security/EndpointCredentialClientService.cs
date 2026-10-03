using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Shared.Handlers;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Shared.Services.Security;

public sealed class EndpointCredentialClientService : IEndpointCredentialClientService
{
    private readonly IHttpClientWithAuth _http;
    private readonly ILogger<EndpointCredentialClientService> _logger;
    public EndpointCredentialClientService(IHttpClientWithAuth http, ILogger<EndpointCredentialClientService> logger){_http=http;_logger=logger;}

    public async Task<ApiResponse<EndpointCredentialStatusDto>> GetStatusAsync(string deviceKey, CancellationToken ct=default)
    {
        try { return await _http.GetAsync<EndpointCredentialStatusDto>($"api/security/endpoints/credentials/{Uri.EscapeDataString(deviceKey)}/status", ct); }
        catch(Exception ex) when(ex is not OperationCanceledException){_logger.LogError(ex,"[ENDPOINT_CREDENTIAL] Status");return ApiResponse<EndpointCredentialStatusDto>.Fail("Không thể kiểm tra trạng thái credential Endpoint.");}
    }

    public async Task<ApiResponse<EndpointCredentialStatusDto>> GetEquipmentStatusAsync(int equipmentAssetId, CancellationToken ct=default)
    {
        try { return await _http.GetAsync<EndpointCredentialStatusDto>($"api/security/endpoints/credentials/equipment/{equipmentAssetId}/status", ct); }
        catch(Exception ex) when(ex is not OperationCanceledException){_logger.LogError(ex,"[ENDPOINT_CREDENTIAL] Equipment status");return ApiResponse<EndpointCredentialStatusDto>.Fail("Không thể kiểm tra trạng thái Endpoint Agent của thiết bị.");}
    }

    public async Task<ApiResponse<IReadOnlyList<EndpointCredentialHistoryDto>>> GetEquipmentHistoryAsync(int equipmentAssetId, CancellationToken ct=default)
    {
        try { return await _http.GetAsync<IReadOnlyList<EndpointCredentialHistoryDto>>($"api/security/endpoints/credentials/equipment/{equipmentAssetId}/history", ct); }
        catch(Exception ex) when(ex is not OperationCanceledException){_logger.LogError(ex,"[ENDPOINT_CREDENTIAL] Equipment history");return ApiResponse<IReadOnlyList<EndpointCredentialHistoryDto>>.Fail("Không thể tải lịch sử credential Endpoint Agent.");}
    }

    public async Task<ApiResponse<EndpointCredentialProvisionResult>> ProvisionForEquipmentAsync(int equipmentAssetId, CancellationToken ct=default)
    {
        try { return await _http.PostAsync<EndpointCredentialProvisionResult>($"api/security/endpoints/credentials/equipment/{equipmentAssetId}/provision", new { }, ct); }
        catch(Exception ex) when(ex is not OperationCanceledException){_logger.LogError(ex,"[ENDPOINT_CREDENTIAL] Equipment provision");return ApiResponse<EndpointCredentialProvisionResult>.Fail("Không thể cấp credential Endpoint Agent cho thiết bị.");}
    }

    public async Task<ApiResponse<object>> RevokeForEquipmentAsync(int equipmentAssetId, CancellationToken ct=default)
    {
        try { return await _http.PostAsync<object>($"api/security/endpoints/credentials/equipment/{equipmentAssetId}/revoke", new { }, ct); }
        catch(Exception ex) when(ex is not OperationCanceledException){_logger.LogError(ex,"[ENDPOINT_CREDENTIAL] Equipment revoke");return ApiResponse<object>.Fail("Không thể thu hồi credential Endpoint Agent của thiết bị.");}
    }

    public async Task<ApiResponse<EndpointCredentialProvisionResult>> ProvisionAsync(EndpointCredentialProvisionDto request,CancellationToken ct=default){try{return await _http.PostAsync<EndpointCredentialProvisionResult>("api/security/endpoints/credentials/provision",request,ct);}catch(Exception ex) when(ex is not OperationCanceledException){_logger.LogError(ex,"[ENDPOINT_CREDENTIAL] Provision");return ApiResponse<EndpointCredentialProvisionResult>.Fail("Không thể cấp/rotate credential Endpoint.");}}
    public async Task<ApiResponse<object>> RevokeAsync(string deviceKey,CancellationToken ct=default){try{return await _http.PostAsync<object>($"api/security/endpoints/credentials/{Uri.EscapeDataString(deviceKey)}/revoke",new{},ct);}catch(Exception ex) when(ex is not OperationCanceledException){_logger.LogError(ex,"[ENDPOINT_CREDENTIAL] Revoke");return ApiResponse<object>.Fail("Không thể thu hồi credential Endpoint.");}}
}
