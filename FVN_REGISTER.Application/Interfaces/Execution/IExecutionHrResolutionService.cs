using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Utils;
namespace FVN_REGISTER.Application.Interfaces.Execution;
public interface IExecutionHrResolutionService
{
Task<ServiceResult<IReadOnlyList<ExecutionHrReviewItemDto>>> GetPendingAsync(int userId,string? moduleCode,string? status,DateOnly? from,DateOnly? to,CancellationToken cancellationToken=default);
Task<ServiceResult<ExecutionReconciliationDetailDto>> GetDetailAsync(int userId,string employeeCode,long reconciliationId,CancellationToken cancellationToken=default);
Task<ServiceResult<ExecutionEvidenceDto>> ReviewEvidenceAsync(int userId,string employeeCode,long evidenceId,ExecutionEvidenceReviewRequest request,CancellationToken cancellationToken=default);
Task<ServiceResult<ExecutionHrResolutionDto>> ResolveAsync(int userId,string employeeCode,long reconciliationId,ExecutionHrResolutionRequest request,CancellationToken cancellationToken=default);
}