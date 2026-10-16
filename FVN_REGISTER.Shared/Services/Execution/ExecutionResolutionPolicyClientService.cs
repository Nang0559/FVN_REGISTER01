using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Shared.Handlers;

namespace FVN_REGISTER.Shared.Services.Execution;

public sealed class ExecutionResolutionPolicyClientService : IExecutionResolutionPolicyClientService
{
    private readonly IHttpClientWithAuth _http;
    public ExecutionResolutionPolicyClientService(IHttpClientWithAuth http) => _http = http;

    public Task<ApiResponse<IReadOnlyList<ExecutionResolutionPolicyDto>>> GetAllAsync(CancellationToken ct = default) =>
        _http.GetAsync<IReadOnlyList<ExecutionResolutionPolicyDto>>("api/execution/policies", ct);

    public Task<ApiResponse<ExecutionResolutionPolicyDto>> CreateAsync(ExecutionResolutionPolicyRequest request, CancellationToken ct = default) =>
        _http.PostAsync<ExecutionResolutionPolicyDto>("api/execution/policies", request, ct);

    public Task<ApiResponse<ExecutionResolutionPolicyDto>> UpdateAsync(int id, ExecutionResolutionPolicyRequest request, CancellationToken ct = default) =>
        _http.PutAsync<ExecutionResolutionPolicyDto>($"api/execution/policies/{id}", request, ct);

    public Task<ApiResponse<object>> DeactivateAsync(int id, CancellationToken ct = default) =>
        _http.DeleteAsync<object>($"api/execution/policies/{id}", ct);
}