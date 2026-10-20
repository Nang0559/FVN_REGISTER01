using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Application.Interfaces.LateEarly;
using FVN_REGISTER.Application.Interfaces.OT;
using FVN_REGISTER.Application.Interfaces.Orchestrators;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Models.Subjects;
using FVN_REGISTER.Contract.Requests.Approvals;
using FVN_REGISTER.Contract.Requests.LateEarly;
using FVN_REGISTER.Contract.Responses.LateEarly;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Entities.LateEarly;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Core.Entities.Common;

namespace FVN_REGISTER.Infrastructure.Services.LateEarly;

/// <summary>
/// Request lifecycle for late arrival / early leave. Registrar eligibility is delegated to
/// the same OT registration policy tables and Feature Operator scope; no separate registrar model.
/// </summary>
public sealed class LateEarlyService : ILateEarlyService
{
    private readonly IUnitOfWork _uow;
    private readonly FVNWEBAPPContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IOtRegistrationPolicy _registrationPolicy;
    private readonly IApprovalWorkflowOrchestrator<LateEarlyRequestSubject> _workflow;
    private readonly IApprovalSelectionService _approvalSelections;

    public LateEarlyService(
        IUnitOfWork uow,
        FVNWEBAPPContext db,
        ICurrentUserService currentUser,
        IAuthorizationService authorization,
        IOtRegistrationPolicy registrationPolicy,
        IApprovalWorkflowOrchestrator<LateEarlyRequestSubject> workflow,
        IApprovalSelectionService approvalSelections)
    {
        _uow = uow;
        _db = db;
        _currentUser = currentUser;
        _authorization = authorization;
        _registrationPolicy = registrationPolicy;
        _workflow = workflow;
        _approvalSelections = approvalSelections;
    }

    public async Task<ServiceResult<LateEarlyRequestDto>> CreateDraftAsync(
        CreateLateEarlyRequestDto request, CancellationToken ct = default)
    {
        try
        {
            var user = _currentUser.GetCurrentUser()
                ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
            if (!Enum.IsDefined(request.LateEarlyType))
                throw new ArgumentException("Loại đăng ký không hợp lệ.");
            if (request.PlannedTime < TimeSpan.Zero || request.PlannedTime >= TimeSpan.FromDays(1))
                throw new ArgumentException("Giờ dự kiến phải nằm trong khoảng 00:00 đến 23:59.");
            if (string.IsNullOrWhiteSpace(request.EmployeeCode))
                throw new ArgumentException("Phải chọn nhân viên cần đăng ký.");
            if (string.IsNullOrWhiteSpace(request.ReasonCode))
                throw new ArgumentException("Lý do đăng ký là bắt buộc.");

            var employee = await _uow.Repository<F03Employee>().Query().AsNoTracking()
                .Where(x => x.IsActive == true && x.EmployeeCode == request.EmployeeCode.Trim())
                .Select(x => new { x.EmployeeCode, x.EmployeeName, x.DeptCode, x.PositionCode })
                .FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Không tìm thấy nhân viên được đăng ký.");

            if (!await _authorization.HasPersonalAsync(user, SecurityFunctionCodes.LateEarlyCreate, ct))
                throw new UnauthorizedAccessException("Bạn không có quyền tạo đăng ký đi muộn/về sớm.");
            if (string.IsNullOrWhiteSpace(user.EmployeeCode))
                throw new UnauthorizedAccessException("Tài khoản đăng ký chưa được liên kết với mã nhân viên.");

            var onBehalf = !string.Equals(user.EmployeeCode, employee.EmployeeCode, StringComparison.OrdinalIgnoreCase);
            if (onBehalf && !await _registrationPolicy.CanRegisterForEmployeesAsync(
                    user, new[] { employee.EmployeeCode }, "LATE_EARLY", ct))
                throw new UnauthorizedAccessException("Bạn không phải người đăng ký được chỉ định cho phòng ban của nhân viên này.");

            var now = DateTime.Now;
            var retroactive = request.WorkDate.Date < now.Date
                || (request.WorkDate.Date == now.Date && request.PlannedTime < now.TimeOfDay);
            if (retroactive && string.IsNullOrWhiteSpace(request.ReasonNote))
                throw new ArgumentException("Đăng ký bổ sung sau thời điểm phát sinh phải ghi rõ lý do.");

            // Serialize the monthly quota check and insert so two different request types
            // (late + early) cannot both pass the same remaining monthly allowance.
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var policy = await GetLateEarlyPolicyAsync(employee.DeptCode, ct);
            if (policy.IsActive && policy.MonthlyLimit > 0)
            {
                var monthStart = new DateTime(request.WorkDate.Year, request.WorkDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1);
                var used = await _uow.Repository<F03LateEarlyRequest>().Query().AsNoTracking()
                    .CountAsync(x => x.EmployeeCode == employee.EmployeeCode
                        && x.WorkDate >= monthStart && x.WorkDate < monthEnd
                        && x.IsActive == true
                        && x.RequestStatus != ApprovalStatus.Cancelled
                        && x.RequestStatus != ApprovalStatus.Rejected, ct);
                if (used >= policy.MonthlyLimit)
                    throw new InvalidOperationException(
                        $"Nhân viên đã đạt giới hạn {policy.MonthlyLimit} lượt đăng ký Đi muộn/Về sớm trong tháng {request.WorkDate:MM/yyyy}.");
            }

            var duplicate = await _uow.Repository<F03LateEarlyRequest>().Query()
                .AnyAsync(x => x.IsActive == true
                    && x.EmployeeCode == employee.EmployeeCode
                    && x.WorkDate == request.WorkDate.Date
                    && x.LateEarlyType == request.LateEarlyType
                    && x.RequestStatus != ApprovalStatus.Cancelled
                    && x.RequestStatus != ApprovalStatus.Rejected, ct);
            if (duplicate)
                throw new InvalidOperationException("Nhân viên đã có đăng ký cùng loại trong ngày này.");

            var entity = new F03LateEarlyRequest
            {
                EmployeeCode = employee.EmployeeCode,
                DeptCode = employee.DeptCode,
                CreatedBy = user.UserId,
                RequestCode = $"LE-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..30],
                LateEarlyType = request.LateEarlyType,
                WorkDate = request.WorkDate.Date,
                PlannedTime = request.PlannedTime,
                ReasonCode = request.ReasonCode.Trim(),
                ReasonNote = request.ReasonNote?.Trim(),
                RegisteredByEmployeeCode = user.EmployeeCode ?? employee.EmployeeCode,
                IsOnBehalf = onBehalf,
                IsRetroactive = retroactive,
                RequestStatus = ApprovalStatus.Draft
            };

            await _uow.Repository<F03LateEarlyRequest>().AddAsync(entity, ct);
            await _uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return ServiceResult<LateEarlyRequestDto>.Ok(ToDto(entity, employee.EmployeeName), "Đã tạo bản nháp đăng ký.");
        }
        catch (DbUpdateException)
        {
            return ServiceResult<LateEarlyRequestDto>.Fail("Đăng ký cùng loại trong ngày đã được tạo đồng thời. Hãy tải lại lịch sử.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException
            or ArgumentException or InvalidOperationException)
        {
            return ServiceResult<LateEarlyRequestDto>.Fail(ex.Message);
        }
    }

    public async Task<ServiceResult<LateEarlyRequestDto>> SubmitAsync(
        int requestId, List<ApprovalSelectionDto>? selections = null, CancellationToken ct = default)
    {
        try
        {
            var user = _currentUser.GetCurrentUser()
                ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
            var entity = await _uow.Repository<F03LateEarlyRequest>().Query()
                .FirstOrDefaultAsync(x => x.Id == requestId && x.IsActive == true, ct)
                ?? throw new KeyNotFoundException("Không tìm thấy đăng ký đi muộn/về sớm.");

            if (!await CanManageRequestAsync(user, entity, SecurityFunctionCodes.LateEarlyEdit, ct))
                throw new UnauthorizedAccessException("Bạn không có quyền gửi đăng ký này.");

            if (entity.RequestStatus is not (ApprovalStatus.Draft or ApprovalStatus.NeedsRevision))
                throw new InvalidOperationException("Chỉ bản nháp hoặc yêu cầu cần chỉnh sửa mới được gửi duyệt.");
            if (entity.IsRetroactive && string.IsNullOrWhiteSpace(entity.ReasonNote))
                throw new ArgumentException("Đăng ký bổ sung phải có lý do chi tiết.");

            if (selections is { Count: > 0 })
                await _approvalSelections.ReplaceAsync(RequestModule.LateEarly, entity.Id, selections, user.UserId, ct);

            var employee = await _uow.Repository<F03Employee>().Query().AsNoTracking()
                .Where(x => x.EmployeeCode == entity.EmployeeCode)
                .Select(x => new { x.DeptCode, x.PositionCode })
                .FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Không tìm thấy thông tin chức vụ của nhân viên.");

            entity.RequestStatus = ApprovalStatus.Pending;
            await _uow.SaveChangesAsync(ct);

            var lateEarlyPolicy = await GetLateEarlyPolicyAsync(entity.DeptCode ?? employee.DeptCode, ct);
            var requireLevel2 = lateEarlyPolicy.IsActive
                && ((entity.IsRetroactive && lateEarlyPolicy.RequireLevel2ForRetroactive)
                    || (entity.LateEarlyType == LateEarlyType.EarlyLeave
                        && lateEarlyPolicy.EarlyLeaveLevel2BeforeMinutes > 0
                        && await IsEarlyLeaveThresholdReachedAsync(
                            entity.EmployeeCode, entity.WorkDate, entity.PlannedTime,
                            lateEarlyPolicy.EarlyLeaveLevel2BeforeMinutes, ct)));

            var context = ApprovalBuildContext.ForLateEarly(
                entity.Id, entity.EmployeeCode, entity.DeptCode ?? employee.DeptCode,
                employee.PositionCode, entity.IsRetroactive, entity.LateEarlyType.ToString(), requireLevel2);

            var approval = await _workflow.InitApprovalAsync(entity.Id, context, ct);
            if (!approval.IsSuccess)
            {
                entity.RequestStatus = ApprovalStatus.Draft;
                await _uow.SaveChangesAsync(ct);
                return ServiceResult<LateEarlyRequestDto>.Fail(
                    approval.Message ?? "Không thể khởi tạo luồng duyệt đi muộn/về sớm.");
            }

            return ServiceResult<LateEarlyRequestDto>.Ok(await MapAsync(entity, ct), "Đã gửi đăng ký vào quy trình phê duyệt.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException
            or ArgumentException or InvalidOperationException)
        {
            return ServiceResult<LateEarlyRequestDto>.Fail(ex.Message);
        }
    }

    public async Task<ServiceResult<LateEarlyRequestDto>> GetAsync(int requestId, CancellationToken ct = default)
    {
        try
        {
            var user = _currentUser.GetCurrentUser()
                ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
            var entity = await _uow.Repository<F03LateEarlyRequest>().Query().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == requestId, ct)
                ?? throw new KeyNotFoundException("Không tìm thấy đăng ký.");
            var canView = await _authorization.CanAccessAsync(
                user, SecurityFunctionCodes.LateEarlyView, entity.EmployeeCode, entity.DeptCode, ct);
            if (!canView && !await CanManageRequestAsync(user, entity, SecurityFunctionCodes.LateEarlyView, ct))
                throw new UnauthorizedAccessException("Bạn không có quyền xem đăng ký này.");
            return ServiceResult<LateEarlyRequestDto>.Ok(await MapAsync(entity, ct));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException
            or ArgumentException or InvalidOperationException)
        {
            return ServiceResult<LateEarlyRequestDto>.Fail(ex.Message);
        }
    }

    public async Task<ServiceResult<List<LateEarlyRequestDto>>> GetMineAsync(CancellationToken ct = default)
    {
        try
        {
            var user = _currentUser.GetCurrentUser()
                ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
            var scope = await _authorization.GetScopeAsync(user.UserId, SecurityFunctionCodes.LateEarlyView, ct);
            var query = _uow.Repository<F03LateEarlyRequest>().Query().AsNoTracking()
                ;

            if (scope == AuthorizationScopeCodes.Own || scope == AuthorizationScopeCodes.Employee)
                query = query.Where(x => x.EmployeeCode == user.EmployeeCode || x.RegisteredByEmployeeCode == user.EmployeeCode);
            else if (scope == AuthorizationScopeCodes.Department)
                query = query.Where(x => x.DeptCode == user.DeptCode);
            else if (scope != AuthorizationScopeCodes.All)
                query = query.Where(x => false);

            var entities = await query.OrderByDescending(x => x.WorkDate).ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
            var codes = entities.Select(x => x.EmployeeCode).Distinct().ToList();
            var names = await _uow.Repository<F03Employee>().Query().AsNoTracking()
                .Where(x => codes.Contains(x.EmployeeCode))
                .Select(x => new { x.EmployeeCode, x.EmployeeName })
                .ToDictionaryAsync(x => x.EmployeeCode, x => x.EmployeeName, ct);
            return ServiceResult<List<LateEarlyRequestDto>>.Ok(entities
                .Select(x => ToDto(x, names.GetValueOrDefault(x.EmployeeCode))).ToList());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException
            or ArgumentException or InvalidOperationException)
        {
            return ServiceResult<List<LateEarlyRequestDto>>.Fail(ex.Message);
        }
    }

    public async Task<ServiceResult> CancelAsync(int requestId, string? reason, CancellationToken ct = default)
    {
        try
        {
            var user = _currentUser.GetCurrentUser()
                ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
            var entity = await _uow.Repository<F03LateEarlyRequest>().Query()
                .FirstOrDefaultAsync(x => x.Id == requestId && x.IsActive == true, ct)
                ?? throw new KeyNotFoundException("Không tìm thấy đăng ký.");
            if (!await CanManageRequestAsync(user, entity, SecurityFunctionCodes.LateEarlyCancel, ct))
                throw new UnauthorizedAccessException("Bạn không có quyền hủy đăng ký này.");
            if (entity.RequestStatus is not (ApprovalStatus.Draft or ApprovalStatus.Pending or ApprovalStatus.NeedsRevision))
                throw new InvalidOperationException("Chỉ bản nháp hoặc yêu cầu chưa hoàn tất mới được hủy.");

            entity.RequestStatus = ApprovalStatus.Cancelled;
            entity.IsActive = false;
            entity.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            await _uow.SaveChangesAsync(ct);
            return ServiceResult.Ok("Đã hủy đăng ký.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException
            or ArgumentException or InvalidOperationException)
        {
            return ServiceResult.Fail(ex.Message);
        }
    }

    private async Task<LateEarlyApprovalRuleDto> GetLateEarlyPolicyAsync(int deptCode, CancellationToken ct)
    {
        var codes = new[] { $"LATE_EARLY_POLICY_{deptCode}", "LATE_EARLY_POLICY_0" };
        var rows = await _db.BusinessRules.AsNoTracking()
            .Where(x => x.Module == RequestModule.LateEarly && codes.Contains(x.Code) && x.IsActive == true)
            .ToListAsync(ct);
        var selected = rows.FirstOrDefault(x => x.Code == codes[0])
            ?? rows.FirstOrDefault(x => x.Code == codes[1]);
        if (selected == null)
            return new LateEarlyApprovalRuleDto { DeptCode = deptCode };
        try
        {
            return JsonSerializer.Deserialize<LateEarlyApprovalRuleDto>(selected.ConfigJson ?? "{}")
                ?? new LateEarlyApprovalRuleDto { DeptCode = deptCode };
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"Cấu hình Approval Policy Đi muộn/Về sớm của phòng ban {deptCode} không hợp lệ. HR cần lưu lại cấu hình.");
        }
    }

    private async Task<bool> IsEarlyLeaveThresholdReachedAsync(
        string employeeCode, DateTime workDate, TimeSpan plannedTime, int thresholdMinutes, CancellationToken ct)
    {
        // Shift end is resolved from the HRM shift reference for this employee/date.
        // Overnight shifts are normalized onto the following day before calculating the difference.
        var minutesBeforeShiftEnd = await _db.Database.SqlQuery<int?>($"""
            SELECT TOP (1)
                CASE
                    WHEN DATEDIFF(MINUTE, CAST('00:00' AS time), s.EndTime)
                         < DATEDIFF(MINUTE, CAST('00:00' AS time), s.StartTime)
                    THEN
                        CASE
                            WHEN DATEDIFF(MINUTE, CAST('00:00' AS time), CAST({plannedTime} AS time))
                                 >= DATEDIFF(MINUTE, CAST('00:00' AS time), s.StartTime)
                                THEN DATEDIFF(MINUTE, CAST('00:00' AS time), s.EndTime) + 1440
                                     - DATEDIFF(MINUTE, CAST('00:00' AS time), CAST({plannedTime} AS time))
                            WHEN DATEDIFF(MINUTE, CAST('00:00' AS time), CAST({plannedTime} AS time))
                                 <= DATEDIFF(MINUTE, CAST('00:00' AS time), s.EndTime)
                                THEN DATEDIFF(MINUTE, CAST('00:00' AS time), s.EndTime)
                                     - DATEDIFF(MINUTE, CAST('00:00' AS time), CAST({plannedTime} AS time))
                            ELSE 0
                        END
                    WHEN CAST({plannedTime} AS time) <= s.StartTime THEN 0
                    ELSE DATEDIFF(MINUTE, CAST({plannedTime} AS time), s.EndTime)
                END AS [Value]
            FROM dbo.F03HrmShiftReference r
            INNER JOIN dbo.F03Shifts s
                ON (s.HrmCode = r.HrmShiftCode OR s.ShiftCode = r.HrmShiftCode
                    OR s.ShiftAbbr = r.HrmShiftAbbr)
            WHERE r.IsActive = 1
              AND r.EmployeeCode = {employeeCode}
              AND r.WorkDate = {workDate.Date}
              AND s.IsActive = 1
            ORDER BY r.SourceUpdatedAt DESC, r.Id DESC
            """).FirstOrDefaultAsync(ct);

        if (!minutesBeforeShiftEnd.HasValue)
            throw new InvalidOperationException(
                "Không xác định được ca làm việc HRM của nhân viên trong ngày đăng ký nên chưa thể áp dụng ngưỡng duyệt về sớm. Hãy đồng bộ lịch ca HRM rồi thử lại.");

        return minutesBeforeShiftEnd.Value >= thresholdMinutes;
    }

    private async Task<bool> CanManageRequestAsync(
        FVN_REGISTER.Contract.Dtos.Authentication.UserIdentityDto user,
        F03LateEarlyRequest entity,
        int functionCode,
        CancellationToken ct)
    {
        if (!await _authorization.HasPersonalAsync(user, functionCode, ct))
            return false;

        if (string.Equals(user.EmployeeCode, entity.EmployeeCode, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(user.EmployeeCode, entity.RegisteredByEmployeeCode, StringComparison.OrdinalIgnoreCase)
            && await _registrationPolicy.CanRegisterForEmployeesAsync(
                user, new[] { entity.EmployeeCode }, "LATE_EARLY", ct);
    }

    private async Task<LateEarlyRequestDto> MapAsync(F03LateEarlyRequest entity, CancellationToken ct)
    {
        var name = await _uow.Repository<F03Employee>().Query().AsNoTracking()
            .Where(x => x.EmployeeCode == entity.EmployeeCode)
            .Select(x => x.EmployeeName)
            .FirstOrDefaultAsync(ct);
        return ToDto(entity, name);
    }

    private static LateEarlyRequestDto ToDto(F03LateEarlyRequest entity, string? employeeName) => new()
    {
        Id = entity.Id,
        RequestCode = entity.RequestCode,
        EmployeeCode = entity.EmployeeCode,
        EmployeeName = employeeName,
        DeptCode = entity.DeptCode,
        LateEarlyType = entity.LateEarlyType,
        WorkDate = entity.WorkDate,
        PlannedTime = entity.PlannedTime,
        ReasonCode = entity.ReasonCode,
        ReasonNote = entity.ReasonNote,
        RegisteredByEmployeeCode = entity.RegisteredByEmployeeCode,
        IsOnBehalf = entity.IsOnBehalf,
        IsRetroactive = entity.IsRetroactive,
        RequestStatus = entity.RequestStatus,
        CreatedAt = entity.CreatedAt
    };
}
