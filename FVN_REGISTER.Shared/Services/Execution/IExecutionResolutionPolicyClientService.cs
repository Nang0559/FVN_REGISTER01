using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Responses;

namespace FVN_REGISTER.Shared.Services.Execution;

public interface IExecutionResolutionPolicyClientService
{
    Task<ApiResponse<IReadOnlyList<ExecutionResolutionPolicyDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResponse<ExecutionResolutionPolicyDto>> CreateAsync(ExecutionResolutionPolicyRequest request, CancellationToken ct = default);
    Task<ApiResponse<ExecutionResolutionPolicyDto>> UpdateAsync(int id, ExecutionResolutionPolicyRequest request, CancellationToken ct = default);
    Task<ApiResponse<object>> DeactivateAsync(int id, CancellationToken ct = default);
}