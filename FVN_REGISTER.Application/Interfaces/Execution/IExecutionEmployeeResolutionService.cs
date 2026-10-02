using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.Execution;

public interface IExecutionEmployeeResolutionService
{
    Task<ServiceResult<ExecutionEmployeeResolutionDto>> DecideAsync(
        int userId, string employeeCode, long reconciliationId,
        ExecutionEmployeeDecisionRequest request, CancellationToken ct = default);
}