using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.Execution;

public interface IExecutionHrClaimService
{
    Task<ServiceResult<ExecutionHrClaimDto>> ClaimAsync(
        int userId,
        string employeeCode,
        long reconciliationId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ExecutionHrClaimDto>> ReleaseAsync(
        int userId,
        string employeeCode,
        long reconciliationId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<ExecutionHrClaimDto>>> GetClaimsAsync(
        int userId,
        string employeeCode,
        IReadOnlyCollection<long> reconciliationIds,
        CancellationToken cancellationToken = default);
}
