using System.Text.Json;
using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Entities.WorkCalendar;
using FVN_REGISTER.Core.Enums;
using Microsoft.EntityFrameworkCore;
using FVN_REGISTER.Application.Interfaces.Execution;

namespace FVN_REGISTER.Infrastructure.Services.Execution;

public sealed class ExecutionEmployeeResolutionService : IExecutionEmployeeResolutionService
{
    private const string EmployeeActionType = "EXECUTION_RESULT_CONFIRMATION";
    private const string HrAppealActionType = "EXECUTION_EVIDENCE_REVIEW";

    private readonly FVNWEBAPPContext _db;
    private readonly IHrmAttendanceCalculationService _attendanceCalculation;

    public ExecutionEmployeeResolutionService(
        FVNWEBAPPContext db,
        IHrmAttendanceCalculationService attendanceCalculation)
    {
        _db = db;
        _attendanceCalculation = attendanceCalculation;
    }

    public async Task<ServiceResult<ExecutionEmployeeResolutionDto>> DecideAsync(
        int userId, string employeeCode, long reconciliationId,
        ExecutionEmployeeDecisionRequest request, CancellationToken ct = default)
    {
        try
        {
            var decision = request.Decision?.Trim().ToUpperInvariant();
            if (decision is not ("ACCEPT" or "APPEAL"))
                return ServiceResult<ExecutionEmployeeResolutionDto>.Fail("Quyết định phải là ACCEPT hoặc APPEAL.");

            var comment = request.Comment?.Trim();
            if (decision == "APPEAL" && string.IsNullOrWhiteSpace(comment))
                return ServiceResult<ExecutionEmployeeResolutionDto>.Fail("Khiếu nại phải có nội dung.");
            if (comment?.Length > 2000)
                return ServiceResult<ExecutionEmployeeResolutionDto>.Fail("Nội dung tối đa 2000 ký tự.");

            await using var tx = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, ct);

            var r = await _db.ExecutionReconciliations
                .FirstOrDefaultAsync(x => x.Id == reconciliationId && x.IsActive != false, ct);
            if (r is null)
                return ServiceResult<ExecutionEmployeeResolutionDto>.Fail("Không tìm thấy reconciliation.");

            var employee = await _db.Employees
                .AsNoTracking()
                .Where(x => x.Id == r.EmployeeId && x.IsActive != false)
                .Select(x => new { x.Id, x.EmployeeCode, x.DeptCode })
                .SingleOrDefaultAsync(ct);
            if (employee is null || !string.Equals(employee.EmployeeCode, employeeCode, StringComparison.OrdinalIgnoreCase))
                return ServiceResult<ExecutionEmployeeResolutionDto>.Fail("Bạn không có quyền xử lý reconciliation này.");

            if (r.ReconciliationStatus != "AwaitingEmployeeDecision")
                return ServiceResult<ExecutionEmployeeResolutionDto>.Fail(
                    $"Case đang ở trạng thái {r.ReconciliationStatus}, không thể xác nhận/khiếu nại.");

            var policy = ParsePolicy(r.ResolutionPolicySnapshotJson);
            if (policy is null)
                return ServiceResult<ExecutionEmployeeResolutionDto>.Fail(
                    "Case chưa có snapshot resolution policy. Không thể tự ý áp dụng luật hiện tại.");

            var now = DateTime.Now;
            var oldStatus = r.ReconciliationStatus;

            r.EmployeeDecisionStatus = decision == "ACCEPT" ? "Accepted" : "Disputed";
            r.EmployeeDecisionAt = now;
            r.EmployeeDecisionBy = userId;
            r.EmployeeDecisionComment = comment;

            CompleteEmployeeResultAction(r, userId, now);

            if (decision == "ACCEPT")
            {
                r.ReconciliationStatus = "Resolved";
                r.RequiresConfirmation = false;
                r.RequiresEvidence = false;
                r.FinalizedAt = now;
                r.FinalizedBy = userId;
                r.ResolvedAt = r.ResolvedAt ?? now;
                r.ResolvedBy = r.ResolvedBy ?? userId;

                await ApplyCorrectionIfRequiredAsync(r, policy.CorrectionMode, userId, ct);
                await FinalizeCalendarAsync(r, ct);

                _db.ExecutionReconciliationHistory.Add(new F03ExecutionReconciliationHistory
                {
                    ReconciliationId = r.Id,
                    FromStatus = oldStatus,
                    ToStatus = "Resolved",
                    EventType = "EMPLOYEE_ACCEPTED_HR_RESULT",
                    Reason = comment ?? "Nhân viên đồng ý kết quả HR.",
                    ActorUserId = userId,
                    ActorEmployeeId = employee.Id,
                    CreatedAt = now
                });
            }
            else
            {
                if (!policy.AllowEmployeeAppeal)
                    return ServiceResult<ExecutionEmployeeResolutionDto>.Fail("Policy không cho phép khiếu nại.");

                if (r.AppealRound >= policy.MaxAppealRounds)
                    return ServiceResult<ExecutionEmployeeResolutionDto>.Fail(
                        "Case đã đạt số vòng khiếu nại tối đa theo policy. Không thể mở thêm vòng.");

                r.AppealRound++;
                r.ReconciliationStatus = r.AppealRound >= policy.MaxAppealRounds
                    ? "FinalDecisionPending"
                    : "AppealReviewing";

                if (policy.RequireEvidenceOnAppeal)
                {
                    var confirmationId = r.ConfirmationId;
                    if (confirmationId.HasValue && !await _db.ExecutionConfirmationEvidence.AnyAsync(
                            x => x.ConfirmationId == confirmationId.Value
                                && x.IsActive != false
                                && x.ReviewStatus == "Pending", ct))
                    {
                        // Evidence may be submitted after opening the appeal.
                    }
                }

                await EnsureHrAppealActionAsync(r, employee.Id, now, policy.AppealReviewHours, ct);

                _db.ExecutionReconciliationHistory.Add(new F03ExecutionReconciliationHistory
                {
                    ReconciliationId = r.Id,
                    FromStatus = oldStatus,
                    ToStatus = r.ReconciliationStatus,
                    EventType = "EMPLOYEE_APPEAL",
                    Reason = comment,
                    ActorUserId = userId,
                    ActorEmployeeId = employee.Id,
                    CreatedAt = now
                });
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ServiceResult<ExecutionEmployeeResolutionDto>.Ok(
                new ExecutionEmployeeResolutionDto(
                    r.Id,
                    r.ReconciliationStatus,
                    decision,
                    comment,
                    r.AppealRound,
                    policy.MaxAppealRounds,
                    false,
                    r.ReconciliationStatus != "Resolved" && r.AppealRound < policy.MaxAppealRounds,
                    r.ReconciliationStatus == "Resolved",
                    now));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return ServiceResult<ExecutionEmployeeResolutionDto>.Fail(ex.Message); }
    }

    private async Task EnsureHrAppealActionAsync(
        F03ExecutionReconciliation r, int employeeId, DateTime now, int dueHours, CancellationToken ct)
    {
        var existing = await _db.ActionItems.FirstOrDefaultAsync(x =>
            x.IsActive != false
            && x.ActionType == HrAppealActionType
            && x.SourceId == r.Id.ToString()
            && (x.Status == ActionItemStatus.Open || x.Status == ActionItemStatus.InProgress),
            ct);

        if (existing is not null)
            return;

        _db.ActionItems.Add(new F03ActionItem
        {
            ActionId = Guid.NewGuid(),
            ModuleCode = r.ModuleCode,
            SourceType = r.SourceType,
            SourceId = r.Id.ToString(),
            ParticipantId = r.ParticipantId,
            EmployeeId = employeeId,
            AssignedToEmployeeId = employeeId,
            WorkDate = r.WorkDate,
            ActionType = HrAppealActionType,
            Title = r.AppealRound >= 1 ? "Xử lý khiếu nại đối soát công" : "Xử lý phản hồi đối soát công",
            Summary = $"Vòng khiếu nại {r.AppealRound}.",
            Severity = 2,
            Priority = 50,
            Status = ActionItemStatus.Open,
            DueAt = now.AddHours(Math.Max(1, dueHours)),
            DetailRoute = $"/execution/hr?reconciliationId={r.Id}",
            ReferenceNo = r.SourceId,
            PayloadJson = JsonSerializer.Serialize(new { r.Id, r.AppealRound }),
            CreatedBy = 0,
            CreatedAt = now,
            LastModifiedSource = "EXECUTION_EMPLOYEE_APPEAL"
        });
    }

    private void CompleteEmployeeResultAction(F03ExecutionReconciliation r, int userId, DateTime now)
    {
        var actions = _db.ActionItems.Where(x =>
            x.IsActive != false
            && x.ActionType == EmployeeActionType
            && x.SourceId == r.Id.ToString()
            && x.AssignedToEmployeeId == r.EmployeeId
            && (x.Status == ActionItemStatus.Open || x.Status == ActionItemStatus.InProgress));

        foreach (var a in actions)
        {
            a.Status = ActionItemStatus.Completed;
            a.CompletedAt = now;
            a.ModifiedBy = userId;
            a.ModifiedAt = now;
            a.LastModifiedSource = "EXECUTION_EMPLOYEE_DECISION";
        }
    }

    private static ResolutionPolicySnapshot? ParsePolicy(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<ResolutionPolicySnapshot>(json);
    }

    private async Task FinalizeCalendarAsync(
        F03ExecutionReconciliation reconciliation,
        CancellationToken ct)
    {
        var resolution = await _db.Set<F03ExecutionResolution>().AsNoTracking()
            .Where(x => x.ReconciliationId == reconciliation.Id && x.IsActive != false)
            .OrderByDescending(x => x.ResolvedAt)
            .FirstOrDefaultAsync(ct);

        var calendar = await _db.CalendarProjections.FirstOrDefaultAsync(x =>
            x.IsActive != false
            && x.EmployeeId == reconciliation.EmployeeId
            && x.WorkDate == reconciliation.WorkDate
            && x.ModuleCode == reconciliation.ModuleCode
            && x.SourceType == reconciliation.SourceType
            && x.SourceId == reconciliation.SourceId
            && x.ParticipantId == reconciliation.ParticipantId, ct);

        if (calendar is null) return;

        calendar.RequiresAction = false;
        if (resolution?.CalendarAction == "CANCEL")
        {
            calendar.StatusCode = "Cancelled";
            calendar.Marker = null;
            calendar.Severity = 0;
        }
        else if (resolution?.CalendarAction == "REFRESH" && resolution.Decision == "OK")
        {
            calendar.StatusCode = "Confirmed";
            calendar.Marker = null;
            calendar.Severity = 0;
        }
        else
        {
            calendar.StatusCode = "Resolved";
            calendar.Marker = null;
            calendar.Severity = 0;
        }

        calendar.Summary = string.IsNullOrWhiteSpace(calendar.Summary)
            ? "Đã hoàn tất đối soát."
            : $"{calendar.Summary} | Đã hoàn tất đối soát.";
        calendar.CalculatedAt = DateTime.Now;
        calendar.ModifiedAt = DateTime.Now;
        calendar.LastModifiedSource = "EXECUTION_EMPLOYEE_ACCEPT";
    }

    private async Task ApplyCorrectionIfRequiredAsync(
        F03ExecutionReconciliation r, byte correctionMode, int userId, CancellationToken ct)
    {
        if (correctionMode == 0) return;
        if (correctionMode != 1)
            throw new InvalidOperationException($"CorrectionMode={correctionMode} chưa có correction handler.");

        var period = await _db.PayrollCalculationPeriods.AsNoTracking()
            .Where(x => x.IsActive != false && x.FromDate <= r.WorkDate && x.ToDate >= r.WorkDate)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);

        if (period is null) throw new InvalidOperationException("Chưa có kỳ payroll chứa ngày cần correction.");
        if (period.Status is "Locked" or "Exported")
            throw new InvalidOperationException($"Kỳ payroll {period.PeriodCode} đã {period.Status}; không thể correction.");

        var employee = await _db.Employees.AsNoTracking()
            .Where(x => x.Id == r.EmployeeId)
            .Select(x => new { x.DeptCode, x.EmployeeCode })
            .SingleAsync(ct);

        var correction = new F03ExecutionCorrection
        {
            ReconciliationId = r.Id,
            ModuleCode = r.ModuleCode,
            CorrectionType = nameof(ExecutionCorrectionMode.AttendanceRecalculate),
            EmployeeId = r.EmployeeId,
            WorkDate = r.WorkDate,
            RequestedState = "ACCEPTED",
            Reason = r.EmployeeDecisionComment ?? "Employee accepted HR result.",
            Status = "Pending",
            CreatedBy = userId,
            CreatedAt = DateTime.Now,
            ModifiedBy = userId,
            ModifiedAt = DateTime.Now,
            LastModifiedSource = "EXECUTION_FINALIZE"
        };
        _db.ExecutionCorrections.Add(correction);
        await _db.SaveChangesAsync(ct);

        var calc = await _attendanceCalculation.CalculateAsync(
            new FVN_REGISTER.Contract.Dtos.HrmSync.HrmAttendanceCalculationRequestDto
            {
                DeptCode = employee.DeptCode,
                FromDate = r.WorkDate.ToDateTime(TimeOnly.MinValue),
                ToDate = r.WorkDate.ToDateTime(TimeOnly.MinValue)
            },
            $"EXECUTION-FINALIZE:{r.Id}",
            ct);

        if (!calc.IsSuccess)
            throw new InvalidOperationException(calc.Message ?? "Không thể recalculation.");

        correction.AppliedState = "RECALCULATED";
        correction.Status = "Applied";
        correction.AppliedAt = DateTime.Now;
        correction.AppliedBy = userId;
        correction.ModifiedBy = userId;
        correction.ModifiedAt = DateTime.Now;
        correction.LastModifiedSource = "EXECUTION_FINALIZE";
    }

    private sealed record ResolutionPolicySnapshot(
        byte CorrectionMode,
        int EmployeeResponseHours,
        int HrReviewHours,
        bool AllowEmployeeAppeal,
        byte MaxAppealRounds,
        int AppealReviewHours,
        bool RequireEvidenceOnAppeal,
        bool RequireFinalDecision,
        string? FinalDecisionPositionCode);
}