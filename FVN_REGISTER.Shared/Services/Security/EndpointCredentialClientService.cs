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

    public async Task<ApiResponse<EndpointCredentialProvisionResult>> ProvisionAsync(EndpointCredentialProvisionDto request,CancellationToken ct=default){try{return await _http.PostAsync<EndpointCredentialProvisionResult>("api/security/endpoints/credentials/provision",request,ct);}catch(Exception ex) when(ex is not OperationCanceledException){_logger.LogError(ex,"[ENDPOINT_CREDENTIAL] Provision");return ApiResponse<EndpointCredentialProvisionResult>.Fail("Không thể cấp/rotate credential Endpoint.");}}
    public async Task<ApiResponse<object>> RevokeAsync(string deviceKey,CancellationToken ct=default){try{return await _http.PostAsync<object>($"api/security/endpoints/credentials/{Uri.EscapeDataString(deviceKey)}/revoke",new{},ct);}catch(Exception ex) when(ex is not OperationCanceledException){_logger.LogError(ex,"[ENDPOINT_CREDENTIAL] Revoke");return ApiResponse<object>.Fail("Không thể thu hồi credential Endpoint.");}}
}
