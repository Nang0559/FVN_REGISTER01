using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Shared.Handlers;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Shared.Services.Security;

public sealed class EndpointInventoryClientService : IEndpointInventoryClientService
{
    private readonly IHttpClientWithAuth _http;
    private readonly ILogger<EndpointInventoryClientService> _logger;

    public EndpointInventoryClientService(IHttpClientWithAuth http, ILogger<EndpointInventoryClientService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public Task<ApiResponse<List<EndpointInventorySummaryDto>>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<List<EndpointInventorySummaryDto>>("api/security/endpoints", ct);

    public Task<ApiResponse<EndpointInventorySummaryDto>> GetAsync(string deviceKey, CancellationToken ct = default)
        => GetAsync<EndpointInventorySummaryDto>($"api/security/endpoints/{Uri.EscapeDataString(deviceKey)}", ct);

    public Task<ApiResponse<int>> EvaluateAsync(long endpointDeviceId, CancellationToken ct = default)
        => PostAsync<int>($"api/security/endpoints/{endpointDeviceId}/evaluate", new { }, ct);

    public Task<ApiResponse<bool>> LinkEquipmentAsync(long endpointDeviceId, int equipmentAssetId, CancellationToken ct = default)
        => PutAsync<bool>($"api/security/endpoints/{endpointDeviceId}/equipment", new EndpointEquipmentLinkRequest(equipmentAssetId), ct);

    private async Task<ApiResponse<T>> GetAsync<T>(string url, CancellationToken ct)
    {
        try { return await _http.GetAsync<T>(url, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _logger.LogError(ex, "[ENDPOINT_INVENTORY_CLIENT] GET {Url}", url); return ApiResponse<T>.Fail("Không thể tải Endpoint Inventory."); }
    }

    private async Task<ApiResponse<T>> PostAsync<T>(string url, object body, CancellationToken ct)
    {
        try { return await _http.PostAsync<T>(url, body, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _logger.LogError(ex, "[ENDPOINT_INVENTORY_CLIENT] POST {Url}", url); return ApiResponse<T>.Fail("Không thể thực hiện đánh giá Endpoint."); }
    }

    private async Task<ApiResponse<T>> PutAsync<T>(string url, object body, CancellationToken ct)
    {
        try { return await _http.PutAsync<T>(url, body, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _logger.LogError(ex, "[ENDPOINT_INVENTORY_CLIENT] PUT {Url}", url); return ApiResponse<T>.Fail("Không thể liên kết Equipment với Endpoint."); }
    }
}
