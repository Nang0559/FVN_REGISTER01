using FVN_REGISTER.Contract.Dtos.Payroll;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.Payroll;

public interface IPayrollInputService
{
    Task<ServiceResult<PayrollPeriodDto>> GetOrCreateCurrentPeriodAsync(int actorUserId, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<PayrollPeriodDto>>> GetPeriodsAsync(CancellationToken ct = default);
    Task<ServiceResult<PayrollPrepareDto>> PrepareAsync(int periodId, int actorUserId, CancellationToken ct = default);
    Task<ServiceResult<PayrollPeriodDto>> LockAsync(int periodId, int actorUserId, CancellationToken ct = default);
    Task<ServiceResult<PayrollExportDto>> ExportAsync(int periodId, int actorUserId, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<PayrollInputDto>>> GetInputsAsync(int periodId, CancellationToken ct = default);
    Task<PayrollPrintResultDto> GetPrintDataAsync(int periodId, CancellationToken ct = default);
}
