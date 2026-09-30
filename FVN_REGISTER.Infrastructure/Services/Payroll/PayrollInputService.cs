using System.Text;
using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Application.Interfaces.Orchestrators;
using FVN_REGISTER.Application.Interfaces.Payroll;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Payroll;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Entities.Approvers;
using FVN_REGISTER.Core.Entities.Payroll;
using FVN_REGISTER.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Payroll;

public sealed class PayrollInputService : IPayrollInputService
{
    private readonly FVNWEBAPPContext _db;
    private readonly IApprovalWorkflowOrchestrator<PayrollPeriodSubject> _approvalWorkflow;
    private readonly ICurrentUserService _currentUser;

    public PayrollInputService(
        FVNWEBAPPContext db,
        IApprovalWorkflowOrchestrator<PayrollPeriodSubject> approvalWorkflow,
        ICurrentUserService currentUser)
    {
        _db = db;
        _approvalWorkflow = approvalWorkflow;
        _currentUser = currentUser;
    }

    public Task<ServiceResult<PayrollPeriodDto>> GetOrCreateCurrentPeriodAsync(int actorUserId, CancellationToken ct = default) => GuardValueAsync(() => GetOrCreateCurrentPeriodCoreAsync(actorUserId, ct));

    private static async Task<ServiceResult<T>> GuardValueAsync<T>(Func<Task<T>> operation)
    {
        try { return ServiceResult<T>.Ok(await operation()); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException or ArgumentException or InvalidOperationException)
        { return ServiceResult<T>.Fail(ex.Message); }
    }

    private async Task<PayrollPeriodDto> GetOrCreateCurrentPeriodCoreAsync(int actorUserId, CancellationToken ct = default)
    {
        var rows = await _db.PayrollCalculationPeriods
            .FromSqlInterpolated($"EXEC dbo.usp_EnsurePayrollPeriod @AsOfDate={DateTime.Today}, @ActorUserId={actorUserId}")
            .AsNoTracking()
            .ToListAsync(ct);
        var row = rows.FirstOrDefault();
        if (row is null) throw new InvalidOperationException("Không thể tạo/xác định kỳ lương hiện tại.");
        return Map(row);
    }

    public Task<ServiceResult<IReadOnlyList<PayrollPeriodDto>>> GetPeriodsAsync(CancellationToken ct = default) => GuardAsync(GetPeriodsCoreAsync);

    private async Task<IReadOnlyList<PayrollPeriodDto>> GetPeriodsCoreAsync(CancellationToken ct = default)
        => await _db.PayrollCalculationPeriods.AsNoTracking()
            .Where(x => x.IsActive != false)
            .OrderByDescending(x => x.FromDate)
            .Select(x => new PayrollPeriodDto(x.Id, x.PeriodCode, x.FromDate, x.ToDate, x.Status,
                x.CalculatedAt, x.CalculatedBy, x.LockedAt, x.LockedBy, x.ExportedAt, x.ExportedBy))
            .ToListAsync(ct);

    public Task<ServiceResult<PayrollPrepareDto>> PrepareAsync(int periodId, int actorUserId, CancellationToken ct = default) => GuardAsync(() => PrepareCoreAsync(periodId, actorUserId, ct));

    private async Task<PayrollPrepareDto> PrepareCoreAsync(int periodId, int actorUserId, CancellationToken ct = default)
    {
        var period = await GetPeriodAsync(periodId, ct);
        Ensure21To20(period);
        if (period.Status is "Locked" or "Exported")
            throw new InvalidOperationException("Kỳ lương đã khóa/xuất, không được chuẩn bị lại.");

        var results = await _db.Database.SqlQueryRaw<PayrollPrepareResult>(
            "EXEC dbo.usp_PreparePayrollPeriod @PeriodId={0}, @ActorUserId={1}", periodId, actorUserId)
            .ToListAsync(ct);
        var result = results.FirstOrDefault();
        return new PayrollPrepareDto(periodId, result?.InputRows ?? 0);
    }

    public Task<ServiceResult<PayrollPeriodDto>> LockAsync(int periodId, int actorUserId, CancellationToken ct = default) => GuardAsync(() => LockCoreAsync(periodId, actorUserId, ct));

    private async Task<PayrollPeriodDto> LockCoreAsync(int periodId, int actorUserId, CancellationToken ct = default)
    {
        var period = await GetPeriodAsync(periodId, ct);
        Ensure21To20(period);
        if (period.Status != "Calculated")
            throw new InvalidOperationException("Chỉ được khóa kỳ lương ở trạng thái Calculated.");

        var calculatedAt = period.CalculatedAt
            ?? throw new InvalidOperationException("Kỳ lương chưa có thời điểm snapshot.");

        var inputCount = await _db.PayrollInputs.CountAsync(
            x => x.PayrollPeriodId == periodId && x.IsActive != false, ct);
        if (inputCount == 0)
            throw new InvalidOperationException("Không thể khóa kỳ lương rỗng.");

        await EnsurePayrollReadyAsync(period, ct);

        var stale = await _db.ExecutionReconciliations.AsNoTracking()
            .AnyAsync(x => x.IsActive != false && x.WorkDate >= period.FromDate && x.WorkDate <= period.ToDate
                && x.ModifiedAt.HasValue && x.ModifiedAt.Value > calculatedAt, ct);
        if (stale)
            throw new InvalidOperationException("Snapshot Payroll Input đã cũ. Hãy Prepare lại kỳ lương trước khi khóa.");

        var lockedRows = await _db.PayrollCalculationPeriods
            .FromSqlInterpolated($"EXEC dbo.usp_LockPayrollPeriod @PeriodId={periodId}, @ActorUserId={actorUserId}")
            .AsNoTracking()
            .ToListAsync(ct);
        var locked = lockedRows.FirstOrDefault()
            ?? throw new InvalidOperationException("Không thể khóa kỳ lương.");

        // Chốt chỉ đóng băng dữ liệu. Sau chốt bắt buộc phải có Approval Snapshot
        // trước khi được in/xuất. Routing vẫn dùng PositionCode HRM hiện tại của
        // người thực hiện chốt, hoàn toàn không thay đổi route của Leave/OT/Trip.
        var identity = _currentUser.GetCurrentUser();
        if (identity == null || string.IsNullOrWhiteSpace(identity.EmployeeCode))
            throw new InvalidOperationException("Không xác định được nhân viên HRM của người chốt kỳ để khởi tạo phê duyệt.");

        var approvalInit = await _approvalWorkflow.InitApprovalAsync(
            periodId,
            ApprovalBuildContext.ForPayrollPeriod(
                periodId,
                identity.EmployeeCode!,
                identity.DeptCode ?? string.Empty,
                identity.PositionCode ?? string.Empty),
            ct);

        if (!approvalInit.Success)
            throw new InvalidOperationException(
                approvalInit.Message ?? "Đã chốt kỳ lương nhưng chưa thể khởi tạo luồng phê duyệt.");

        return Map(locked);
    }

    public Task<ServiceResult<PayrollExportDto>> ExportAsync(int periodId, int actorUserId, CancellationToken ct = default) => GuardAsync(() => ExportCoreAsync(periodId, actorUserId, ct));

    private async Task<PayrollExportDto> ExportCoreAsync(int periodId, int actorUserId, CancellationToken ct = default)
    {
        var period = await GetPeriodAsync(periodId, ct);
        Ensure21To20(period);
        if (period.Status is not ("Locked" or "Exported"))
            throw new InvalidOperationException("Chỉ được xuất kỳ lương sau khi đã khóa.");

        await EnsurePayrollReadyAsync(period, ct);
        await EnsurePayrollApprovedAsync(periodId, ct);

        var rowsResult = await GetInputsAsync(periodId, ct);
        if (!rowsResult.IsSuccess) throw new InvalidOperationException(rowsResult.Message ?? "Không thể đọc Payroll Input.");
        var rows = rowsResult.Data ?? Array.Empty<PayrollInputDto>();
        if (rows.Count == 0)
            throw new InvalidOperationException("Kỳ lương không có Payroll Input để xuất.");

        var sb = new StringBuilder();
        sb.AppendLine("EmployeeCode,EmployeeName,WorkDate,WorkMinutes,LeaveTotal,OTMinutes,SnapshotAt");
        foreach (var row in rows)
        {
            sb.Append(Escape(row.EmployeeCode)).Append(',')
              .Append(Escape(row.EmployeeName)).Append(',')
              .Append(row.WorkDate.ToString("yyyy-MM-dd")).Append(',')
              .Append(row.WorkMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(row.LeaveTotal.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(row.OTMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(row.SnapshotAt.ToString("yyyy-MM-dd HH:mm:ss")).AppendLine();
        }

        var now = DateTime.Now;
        if (period.Status != "Exported")
        {
            period.Status = "Exported";
            period.ExportedAt = now;
            period.ExportedBy = actorUserId;
            period.ModifiedBy = actorUserId;
            period.ModifiedAt = now;
            period.LastModifiedSource = "PAYROLL_EXPORT";
            await _db.SaveChangesAsync(ct);
        }

        return new PayrollExportDto(period.Id, period.PeriodCode, period.ExportedAt ?? now, rows.Count,
            $"Payroll_{period.PeriodCode}.csv", "text/csv; charset=utf-8", new UTF8Encoding(true).GetBytes(sb.ToString()));
    }

    public async Task<PayrollPrintResultDto> GetPrintDataAsync(int periodId, CancellationToken ct = default)
    {
        var period = await GetPeriodAsync(periodId, ct);
        Ensure21To20(period);
        if (period.Status is not ("Calculated" or "Locked"))
            throw new InvalidOperationException("Chỉ được in bảng công khi kỳ lương đã Calculated hoặc Locked.");

        await EnsurePayrollReadyAsync(period, ct);
        var calculatedAt = period.CalculatedAt ?? throw new InvalidOperationException("Kỳ lương chưa có snapshot chính thức.");

        var stale = await _db.ExecutionReconciliations.AsNoTracking()
            .AnyAsync(x => x.IsActive != false && x.WorkDate >= period.FromDate && x.WorkDate <= period.ToDate
                && x.ModifiedAt.HasValue && x.ModifiedAt.Value > calculatedAt, ct);
        if (stale)
            throw new InvalidOperationException("Bảng công đã thay đổi sau lần snapshot. Hãy Prepare lại kỳ lương trước khi in.");

        // Calculated is the editable/reviewable stage. Once Locked, the print is a
        // controlled output of the frozen dataset and therefore requires approval.
        if (period.Status == "Locked")
            await EnsurePayrollApprovedAsync(periodId, ct);

        var inputsResult = await GetInputsAsync(periodId, ct);
        if (!inputsResult.IsSuccess)
            throw new InvalidOperationException(inputsResult.Message ?? "Không thể đọc dữ liệu bảng công.");
        var inputs = inputsResult.Data?.ToList() ?? new List<PayrollInputDto>();
        if (inputs.Count == 0)
            throw new InvalidOperationException("Kỳ lương không có dữ liệu bảng công để in.");

        return new PayrollPrintResultDto(Map(period), inputs);
    }

    public Task<ServiceResult<IReadOnlyList<PayrollInputDto>>> GetInputsAsync(int periodId, CancellationToken ct = default) => GuardAsync(() => GetInputsCoreAsync(periodId, ct));

    private async Task<IReadOnlyList<PayrollInputDto>> GetInputsCoreAsync(int periodId, CancellationToken ct = default)
    {
        _ = await GetPeriodAsync(periodId, ct);
        return await (
            from input in _db.PayrollInputs.AsNoTracking()
            join employee in _db.Employees.AsNoTracking() on input.EmployeeId equals employee.Id
            where input.PayrollPeriodId == periodId && input.IsActive != false
            orderby employee.EmployeeCode, input.WorkDate
            select new PayrollInputDto(input.Id, input.PayrollPeriodId, input.EmployeeId, employee.EmployeeCode,
                employee.EmployeeName, input.WorkDate, input.WorkMinutes, input.LeaveTotal, input.OTMinutes, input.SnapshotAt))
            .ToListAsync(ct);
    }

    private async Task EnsurePayrollApprovedAsync(int periodId, CancellationToken ct)
    {
        var snapshot = await _db.ApprovalSnapshots.AsNoTracking()
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.RequestId == periodId && x.RequestType == RequestModule.Payroll, ct);

        if (snapshot == null)
            throw new InvalidOperationException("Kỳ đã chốt nhưng chưa có Snapshot phê duyệt. Hãy khởi tạo lại luồng phê duyệt.");

        var histories = await _db.ApprovalHistories.AsNoTracking()
            .Where(x => x.RequestId == periodId && x.RequestType == RequestModule.Payroll)
            .ToListAsync(ct);

        var required = snapshot.Steps.Where(x => x.IsRequired).ToList();
        if (required.Count == 0 || required.Any(step => histories.Any(h => h.StepId == step.Id && h.Decision == DecisionType.Rejected)))
            throw new InvalidOperationException("Kỳ chốt chưa được phê duyệt hoàn tất.");

        if (!required.All(step => histories.Any(h => h.StepId == step.Id && h.Decision == DecisionType.Approved)))
            throw new InvalidOperationException("Kỳ chốt đang chờ phê duyệt đủ các cấp.");
    }

    private async Task EnsurePayrollReadyAsync(F03PayrollCalculationPeriod period, CancellationToken ct)
    {
        var unresolved = await _db.ExecutionReconciliations.AsNoTracking()
            .AnyAsync(x => x.IsActive != false && x.WorkDate >= period.FromDate && x.WorkDate <= period.ToDate
                && x.ReconciliationStatus != "Resolved" && x.ReconciliationStatus != "Matched" && x.ReconciliationStatus != "None", ct);
        if (unresolved)
            throw new InvalidOperationException("Kỳ lương còn Execution Reconciliation Mismatch/AwaitingConfirmation chưa được giải quyết.");

        var failedOrPendingCorrection = await _db.ExecutionCorrections.AsNoTracking()
            .AnyAsync(x => x.IsActive != false && x.WorkDate >= period.FromDate && x.WorkDate <= period.ToDate
                && x.Status != "Applied" && x.Status != "Cancelled", ct);
        if (failedOrPendingCorrection)
            throw new InvalidOperationException("Kỳ lương còn Execution Correction Pending/Failed chưa được xử lý.");
    }

    private async Task<F03PayrollCalculationPeriod> GetPeriodAsync(int periodId, CancellationToken ct)
        => await _db.PayrollCalculationPeriods.FirstOrDefaultAsync(x => x.Id == periodId && x.IsActive != false, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy kỳ lương.");

    private static void Ensure21To20(F03PayrollCalculationPeriod period)
    {
        if (period.FromDate.Day != 21 || period.ToDate != period.FromDate.AddMonths(1).AddDays(-1))
            throw new InvalidOperationException("Kỳ lương phải theo chu kỳ ngày 21 đến ngày 20.");
    }

    private static PayrollPeriodDto Map(F03PayrollCalculationPeriod x)
        => new(x.Id, x.PeriodCode, x.FromDate, x.ToDate, x.Status, x.CalculatedAt, x.CalculatedBy, x.LockedAt, x.LockedBy, x.ExportedAt, x.ExportedBy);

    private static string Escape(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private static async Task<ServiceResult<T>> GuardAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return ServiceResult<T>.Ok(await operation());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException or ArgumentException or InvalidOperationException)
        {
            return ServiceResult<T>.Fail(ex.Message);
        }
    }

    private sealed class PayrollPrepareResult
    {
        public int PeriodId { get; set; }
        public int InputRows { get; set; }
    }
}
