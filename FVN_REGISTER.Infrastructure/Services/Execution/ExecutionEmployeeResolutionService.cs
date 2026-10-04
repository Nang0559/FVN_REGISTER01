using System.Text.Json;
using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Entities.WorkCalendar;
using FVN_REGISTER.Core.Entities.Security;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Enums;
using Microsoft.EntityFrameworkCore;
using FVN_REGISTER.Application.Interfaces.Execution;
using FVN_REGISTER.Application.Interfaces.HrmSync;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Contract.Dtos.Authentication;

namespace FVN_REGISTER.Infrastructure.Services.Execution;

public sealed class ExecutionEmployeeResolutionService : IExecutionEmployeeResolutionService
{
    private const string EmployeeActionType = "EXECUTION_RESULT_CONFIRMATION";
    private const string HrAppealActionType = "EXECUTION_EVIDENCE_REVIEW";

    private readonly FVNWEBAPPContext _db;
    private readonly IHrmAttendanceCalculationService _attendanceCalculation;
    private readonly IAuthorizationService _authorization;

    public ExecutionEmployeeResolutionService(
        FVNWEBAPPContext db,
        IHrmAttendanceCalculationService attendanceCalculation,
        IAuthorizationService authorization)
    {
        _db = db;
        _attendanceCalculation = attendanceCalculation;
        _authorization = authorization;
    }

    public async Task ProcessExpiredEmployeeDecisionAsync(
        long reconciliationId, CancellationToken ct = default)
    {
        var row = await _db.ExecutionReconciliations.AsNoTracking()
            .Where(x => x.Id == reconciliationId
                && x.IsActive != false
                && x.ReconciliationStatus == "AwaitingEmployeeDecision")
            .Join(_db.Employees.AsNoTracking(),
                x => x.EmployeeId, e => e.Id,
                (x, e) => new
                {
                    Reconciliation = x,
                    EmployeeCode = e.EmployeeCode,
                    EmployeeId = e.Id
                })
            .SingleOrDefaultAsync(ct);

        if (row is null || string.IsNullOrWhiteSpace(row.Reconciliation.ResolutionPolicySnapshotJson))
            return;

        var policy = ParsePolicy(row.Reconciliation.ResolutionPolicySnapshotJson);
        if (policy is null)
            return;

        var action = await _db.ActionItems
            .FirstOrDefaultAsync(x =>
                x.IsActive != false
                && x.ActionType == EmployeeActionType
                && x.SourceId == reconciliationId.ToString()
                && (x.Status == ActionItemStatus.Open || x.Status == ActionItemStatus.InProgress)
                && x.DueAt.HasValue
                && x.DueAt.Value <= DateTime.Now, ct);

        if (action is null)
            return;

        if (policy.EmployeeTimeoutMode == 1)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(x => x.IsActive != false && x.EmployeeCode == row.EmployeeCode)
                .Select(x => new { x.Id })
                .SingleOrDefaultAsync(ct);

            if (user is not null)
            {
                await DecideAsync(
                    user.Id,
                    row.EmployeeCode,
                    reconciliationId,
                    new ExecutionEmployeeDecisionRequest
                    {
                        Decision = "ACCEPT",
                        Comment = "Hết thời hạn phản hồi theo policy; hệ thống tự động chấp nhận kết quả HR."
                    },
                    ct);
            }

            return;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        var reconciliation = await _db.ExecutionReconciliations
            .FirstOrDefaultAsync(x => x.Id == reconciliationId && x.IsActive != false, ct);
        if (reconciliation is null || reconciliation.ReconciliationStatus != "AwaitingEmployeeDecision")
            return;

        var now = DateTime.Now;
        var oldStatus = reconciliation.ReconciliationStatus;
        reconciliation.EmployeeDecisionStatus = "TimedOut";
        reconciliation.EmployeeDecisionAt = now;
        reconciliation.EmployeeDecisionComment =
            "Nhân viên không phản hồi trong thời hạn policy quy định.";
        reconciliation.ReconciliationStatus = "FinalDecisionPending";

        CompleteEmployeeResultAction(reconciliation, 0, now);
        reconciliation.ActionId = await EnsureHrAppealActionAsync(
            reconciliation,
            reconciliation.EmployeeId,
            now,
            policy.AppealReviewHours,
            ct);

        await MarkCalendarDisputedAsync(reconciliation, ct);

        _db.ExecutionReconciliationHistory.Add(new F03ExecutionReconciliationHistory
        {
            ReconciliationId = reconciliation.Id,
            FromStatus = oldStatus,
            ToStatus = reconciliation.ReconciliationStatus,
            EventType = "EMPLOYEE_RESPONSE_TIMEOUT",
            Reason = reconciliation.EmployeeDecisionComment,
            ActorUserId = null,
            ActorEmployeeId = reconciliation.EmployeeId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
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

                var latestResolution = await _db.Set<F03ExecutionResolution>().AsNoTracking()
                    .Where(x => x.ReconciliationId == r.Id && x.IsActive != false)
                    .OrderByDescending(x => x.ResolvedAt)
                    .FirstOrDefaultAsync(ct);

                if (latestResolution?.Decision == "OK")
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
                    var lastResolutionAt = await _db.Set<F03ExecutionResolution>().AsNoTracking()
                        .Where(x => x.ReconciliationId == r.Id && x.IsActive != false)
                        .OrderByDescending(x => x.ResolvedAt)
                        .Select(x => (DateTime?)x.ResolvedAt)
                        .FirstOrDefaultAsync(ct);

                    var hasAppealEvidence = confirmationId.HasValue
                        && await _db.ExecutionConfirmationEvidence.AnyAsync(
                            x => x.ConfirmationId == confirmationId.Value
                                && x.IsActive != false
                                && (!lastResolutionAt.HasValue || x.SubmittedAt > lastResolutionAt.Value), ct);

                    if (!hasAppealEvidence)
                        return ServiceResult<ExecutionEmployeeResolutionDto>.Fail(
                            "Policy yêu cầu evidence mới cho mỗi vòng khiếu nại. Vui lòng bổ sung evidence trước khi gửi khiếu nại.");
                }

                r.ActionId = await EnsureHrAppealActionAsync(
                    r, employee.Id, now, policy.AppealReviewHours, ct);
                await MarkCalendarDisputedAsync(r, ct);

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

    private async Task<Guid> EnsureHrAppealActionAsync(
        F03ExecutionReconciliation r, int employeeId, DateTime now, int dueHours, CancellationToken ct)
    {
        var existing = await _db.ActionItems.FirstOrDefaultAsync(x =>
            x.IsActive != false
            && x.ActionType == HrAppealActionType
            && x.SourceId == r.Id.ToString()
            && (x.Status == ActionItemStatus.Open || x.Status == ActionItemStatus.InProgress),
            ct);

        if (existing is not null)
            return existing.ActionId;

        // Operator eligibility must match the same authorization chain used when
        // the assigned action is later claimed/resolved:
        // RBAC -> Managed Scope -> Feature Operator.
        // Without the target employee context here, a narrow-scope operator can
        // be assigned successfully and only discover the mismatch when opening
        // the appeal, resulting in a late 403/Forbid.
        var targetEmployee = await _db.Employees.AsNoTracking()
            .Where(x => x.Id == employeeId)
            .Select(x => new { x.EmployeeCode, x.DeptCode })
            .SingleOrDefaultAsync(ct);

        if (targetEmployee is null)
            throw new InvalidOperationException(
                "Không xác định được nhân viên đích để kiểm tra scope Execution Review.");

        var operatorCandidates = await _db.Set<F03FeatureOperatorAssignment>().AsNoTracking()
            .Where(x => x.IsActive == true
                && x.FunctionCode == SecurityFunctionCodes.ExecutionReview
                && x.ResourceType == "EXECUTION_REVIEW"
                && x.ResourceId == null)
            .Join(
                _db.Users.AsNoTracking().Where(x => x.IsActive != false),
                assignment => assignment.EmployeeCode,
                user => user.EmployeeCode,
                (assignment, user) => new
                {
                    UserId = user.Id,
                    user.EmployeeCode,
                    user.DeptCode,
                    user.PermissionCode,
                    user.FullName,
                    user.LevelApprove,
                    assignment.CreatedAt,
                    AssignmentId = assignment.Id
                })
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.AssignmentId)
            .ToListAsync(ct);

        var eligibility = new List<(int Id, string EmployeeCode, bool Eligible)>(operatorCandidates.Count);
        foreach (var candidate in operatorCandidates)
        {
            var identity = new UserIdentityDto
            {
                UserId = candidate.UserId,
                EmployeeCode = candidate.EmployeeCode,
                DeptCode = candidate.DeptCode,
                Permission = candidate.PermissionCode,
                FullName = candidate.FullName,
                LevelApprove = candidate.LevelApprove,
                IsLoggedIn = true
            };

            var hasRbac = await _authorization.HasAsync(
                identity,
                SecurityFunctionCodes.ExecutionReview,
                ct);

            if (!hasRbac)
            {
                eligibility.Add((candidate.UserId, candidate.EmployeeCode, false));
                continue;
            }

            var canAccessTarget = await _authorization.CanAccessAsync(
                identity,
                SecurityFunctionCodes.ExecutionReview,
                targetEmployee.EmployeeCode,
                targetEmployee.DeptCode,
                ct);

            // Scope is evaluated here because it depends on the reconciliation target;
            // only the combined RBAC + target-scope result reaches the selector.
            eligibility.Add((
                candidate.UserId,
                candidate.EmployeeCode,
                FeatureOperatorAuthorizationPolicy.CanSelectOperator(hasRbac, canAccessTarget)));
        }

        var operatorUser = FeatureOperatorAuthorizationPolicy.SelectFirstEligibleOperator(eligibility);
        if (operatorUser is null)
            throw new InvalidOperationException(
                "Không tìm thấy nhân sự được phân công Execution Review có RBAC Execution Review, tài khoản active và scope với nhân viên đích để nhận vòng khiếu nại.");

        var operatorEmployee = await _db.Employees.AsNoTracking()
            .Where(x => x.IsActive != false && x.EmployeeCode == operatorUser.Value.EmployeeCode)
            .Select(x => (int?)x.Id)
            .SingleAsync(ct);

        var action = new F03ActionItem
        {
            ActionId = Guid.NewGuid(),
            ModuleCode = r.ModuleCode,
            SourceType = r.SourceType,
            SourceId = r.Id.ToString(),
            ParticipantId = r.ParticipantId,
            EmployeeId = employeeId,
            AssignedToEmployeeId = operatorEmployee,
            AssignedToUserId = operatorUser.Value.Id,
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
        };
        _db.ActionItems.Add(action);
        return action.ActionId;
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

    private async Task MarkCalendarDisputedAsync(
        F03ExecutionReconciliation reconciliation,
        CancellationToken ct)
    {
        var calendar = await _db.CalendarProjections.FirstOrDefaultAsync(x =>
            x.IsActive != false
            && x.EmployeeId == reconciliation.EmployeeId
            && x.WorkDate == reconciliation.WorkDate
            && x.ModuleCode == reconciliation.ModuleCode
            && x.SourceType == reconciliation.SourceType
            && x.SourceId == reconciliation.SourceId
            && x.ParticipantId == reconciliation.ParticipantId, ct);

        if (calendar is null) return;

        calendar.RequiresAction = true;
        calendar.StatusCode = reconciliation.ReconciliationStatus == "FinalDecisionPending"
            ? "FinalDecisionPending"
            : "EmployeeDisputed";
        calendar.Marker = "!";
        calendar.Severity = 2;
        calendar.Summary = string.IsNullOrWhiteSpace(calendar.Summary)
            ? "Nhân viên không đồng ý kết quả HR; đang chờ xử lý tiếp."
            : $"{calendar.Summary} | Nhân viên khiếu nại; đang chờ xử lý tiếp.";
        calendar.CalculatedAt = DateTime.Now;
        calendar.ModifiedAt = DateTime.Now;
        calendar.LastModifiedSource = "EXECUTION_EMPLOYEE_APPEAL";
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