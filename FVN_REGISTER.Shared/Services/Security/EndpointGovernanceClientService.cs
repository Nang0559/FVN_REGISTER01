using System.Net.Http.Headers;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Shared.Handlers;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Shared.Services.Security;

public sealed class EndpointGovernanceClientService : IEndpointGovernanceClientService
{
    private readonly IHttpClientWithAuth _http;
    private readonly ILogger<EndpointGovernanceClientService> _logger;

    public EndpointGovernanceClientService(IHttpClientWithAuth http, ILogger<EndpointGovernanceClientService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public Task<ApiResponse<List<EndpointGovernancePolicyDto>>> GetPoliciesAsync(EndpointGovernanceItemType? itemType = null, EndpointTargetType? targetType = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (itemType.HasValue) query.Add($"itemType={(int)itemType.Value}");
        if (targetType.HasValue) query.Add($"targetType={(int)targetType.Value}");
        return GetAsync<List<EndpointGovernancePolicyDto>>($"api/security/endpoint-governance/policies{(query.Count == 0 ? string.Empty : "?" + string.Join("&", query))}", ct);
    }

    public Task<ApiResponse<EndpointGovernancePolicyDto>> CreateVersionAsync(EndpointGovernancePolicyUpsertRequest request, CancellationToken ct = default) => PostAsync<EndpointGovernancePolicyDto>("api/security/endpoint-governance/policies/versions", request, ct);
    public Task<ApiResponse<EndpointGovernancePolicyDto>> SubmitAsync(int policyId, CancellationToken ct = default) => PostAsync<EndpointGovernancePolicyDto>($"api/security/endpoint-governance/policies/{policyId}/submit", new { }, ct);
    public Task<ApiResponse<EndpointGovernancePolicyDto>> PublishAsync(int policyId, CancellationToken ct = default) => PostAsync<EndpointGovernancePolicyDto>($"api/security/endpoint-governance/policies/{policyId}/publish", new { }, ct);

    public async Task<ApiResponse<EndpointGovernanceExcelPreviewDto>> PreviewExcelAsync(IBrowserFile file, EndpointGovernanceItemType itemType, EndpointTargetType targetType, CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream(10 * 1024 * 1024, ct);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            content.Add(fileContent, "file", file.Name);
            return await _http.PostMultipartAsync<EndpointGovernanceExcelPreviewDto>($"api/security/endpoint-governance/policies/import/preview?itemType={(int)itemType}&targetType={(int)targetType}", content, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _logger.LogError(ex, "[ENDPOINT_CLIENT] Excel preview"); return ApiResponse<EndpointGovernanceExcelPreviewDto>.Fail("Không thể đọc file Excel Endpoint Governance."); }
    }

    public async Task<ApiResponse<EndpointGovernancePolicyDto>> ImportExcelAsync(IBrowserFile file, string policyCode, string policyName, EndpointGovernanceItemType itemType, EndpointTargetType targetType, string? remark, CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream(10 * 1024 * 1024, ct);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            content.Add(fileContent, "file", file.Name);
            content.Add(new StringContent(policyCode ?? string.Empty), "policyCode");
            content.Add(new StringContent(policyName ?? string.Empty), "policyName");
            content.Add(new StringContent(((int)itemType).ToString()), "itemType");
            content.Add(new StringContent(((int)targetType).ToString()), "targetType");
            content.Add(new StringContent(remark ?? string.Empty), "remark");
            return await _http.PostMultipartAsync<EndpointGovernancePolicyDto>("api/security/endpoint-governance/policies/import", content, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _logger.LogError(ex, "[ENDPOINT_CLIENT] Excel import"); return ApiResponse<EndpointGovernancePolicyDto>.Fail("Không thể import catalog Endpoint Governance."); }
    }

    public Task<ApiResponse<List<EndpointGovernanceRequestDto>>> GetRequestsAsync(bool mineOnly = true, CancellationToken ct = default) => GetAsync<List<EndpointGovernanceRequestDto>>($"api/security/endpoint-governance/requests?mineOnly={mineOnly}", ct);
    public Task<ApiResponse<EndpointGovernanceRequestDto>> CreateRequestAsync(EndpointGovernanceRequestCreateDto request, CancellationToken ct = default) => PostAsync<EndpointGovernanceRequestDto>("api/security/endpoint-governance/requests", request, ct);
    public Task<ApiResponse<EndpointGovernanceRequestDto>> ReviewAsync(int requestId, bool approved, string? comment = null, CancellationToken ct = default) => PostAsync<EndpointGovernanceRequestDto>($"api/security/endpoint-governance/requests/{requestId}/security-review", new { Approved = approved, Comment = comment }, ct);

    public Task<ApiResponse<List<EndpointComplianceFindingDto>>> GetFindingsAsync(long? endpointDeviceId = null, bool openOnly = true, CancellationToken ct = default)
    {
        var url = $"api/security/endpoint-governance/compliance/findings?openOnly={openOnly}";
        if (endpointDeviceId.HasValue) url += $"&endpointDeviceId={endpointDeviceId.Value}";
        return GetAsync<List<EndpointComplianceFindingDto>>(url, ct);
    }

    private async Task<ApiResponse<T>> GetAsync<T>(string url, CancellationToken ct)
    {
        try { return await _http.GetAsync<T>(url, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _logger.LogError(ex, "[ENDPOINT_CLIENT] GET {Url}", url); return ApiResponse<T>.Fail("Không thể tải dữ liệu Endpoint Governance."); }
    }

    private async Task<ApiResponse<T>> PostAsync<T>(string url, object body, CancellationToken ct)
    {
        try { return await _http.PostAsync<T>(url, body, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _logger.LogError(ex, "[ENDPOINT_CLIENT] POST {Url}", url); return ApiResponse<T>.Fail("Không thể thực hiện thao tác Endpoint Governance."); }
    }
}
