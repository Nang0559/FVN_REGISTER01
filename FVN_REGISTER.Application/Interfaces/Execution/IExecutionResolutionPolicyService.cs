using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.Execution;

public interface IExecutionResolutionPolicyService
{
    Task<ServiceResult<IReadOnlyList<ExecutionResolutionPolicyDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ServiceResult<ExecutionResolutionPolicyDto>> SaveAsync(int? id, ExecutionResolutionPolicyRequest request, int actorUserId, CancellationToken ct = default);
    Task<ServiceResult<object>> DeactivateAsync(int id, int actorUserId, CancellationToken ct = default);
}