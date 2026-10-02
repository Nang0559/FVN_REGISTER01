using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Utils;
namespace FVN_REGISTER.Application.Interfaces.Execution;
public interface IExecutionReconciliationService
{
Task<ServiceResult<ExecutionReconciliationDto>> EnsureAttendanceFeedbackAsync(string employeeCode,DateOnly workDate,CancellationToken cancellationToken=default);
Task<ServiceResult<ExecutionReconciliationDto>> GetAsync(string employeeCode,long reconciliationId,CancellationToken cancellationToken=default);
Task<ServiceResult<ExecutionReconciliationDetailDto>> GetDetailAsync(string employeeCode,long reconciliationId,CancellationToken cancellationToken=default);
Task<ServiceResult<IReadOnlyList<ExecutionReconciliationDto>>> GetMineAsync(string employeeCode,DateOnly from,DateOnly to,CancellationToken cancellationToken=default);
Task<ServiceResult<ExecutionReconciliationDto>> UpsertAsync(string employeeCode,ExecutionReconciliationUpsertRequest request,CancellationToken cancellationToken=default,int? actorUserId=null);
Task<ServiceResult<ExecutionConfirmationDto>> SubmitConfirmationAsync(string employeeCode,long reconciliationId,ExecutionConfirmationRequest request,CancellationToken cancellationToken=default);
Task<ServiceResult<ExecutionEvidenceDto>> AddEvidenceAsync(string employeeCode,int userId,long confirmationId,ExecutionEvidenceRequest request,CancellationToken cancellationToken=default);
Task<ServiceResult<int>> UploadEvidenceFileAsync(string employeeCode,int userId,long confirmationId,string fileName,string? contentType,long length,Stream content,CancellationToken cancellationToken=default);
}