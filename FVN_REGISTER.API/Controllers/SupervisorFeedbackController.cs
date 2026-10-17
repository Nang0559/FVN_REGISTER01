using AppAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;
using FVN_REGISTER.Application.Interfaces.Execution;
using FVN_REGISTER.Application.Interfaces.OT;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Registration;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Contract.Requests.OT;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.OT;
using FVN_REGISTER.Core.Entities.WorkCalendar;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FVN_REGISTER.Contract.Dtos.OT;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/supervisor-feedback")]
public sealed class SupervisorFeedbackController : BaseApiController
{
    private readonly FVNWEBAPPContext _db;
    private readonly AppAuthorizationService _authorization;
    private readonly IOTService _otService;
    private readonly IOtRegistrationPolicy _policy;
    private readonly IExecutionReconciliationService _execution;

    public SupervisorFeedbackController(
        FVNWEBAPPContext db,
        AppAuthorizationService authorization,
        IOTService otService,
        IOtRegistrationPolicy policy,
        IExecutionReconciliationService execution,
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<SupervisorFeedbackController> logger,
        Microsoft.Extensions.Options.IOptionsMonitor<Application.Configuration.AuthDebugOptions> options)
        : base(currentUser, userLog, logger, options)
    {
        _db = db;
        _authorization = authorization;
        _otService = otService;
        _policy = policy;
        _execution = execution;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SupervisorFeedbackCreateDto dto, CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode)) return BadRequest(ApiResponse<object>.Fail("Thiếu mã nhân viên."));

        var employee = await _db.Employees.AsNoTracking()
            .Where(x => x.EmployeeCode == dto.EmployeeCode)
            .Select(x => new { x.Id, x.EmployeeCode, x.EmployeeName, x.DeptCode })
            .FirstOrDefaultAsync(ct);
        if (employee == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhân viên."));

        // Chỉ gửi phản hồi cho CHÍNH MÌNH: không cho gửi thay mã nhân viên khác.
        if (!string.Equals(employee.EmployeeCode?.Trim(), UserInfo.EmployeeCode?.Trim(), StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, ApiResponse<object>.Fail("Phản hồi cấp trên chỉ gửi cho chính nhân viên đang đăng nhập."));

        // Dùng CHUNG IOtRegistrationPolicy (rule phòng ban + chức vụ + Feature Operator OT.RegistrarManage).
        if (await _policy.IsRegistrarAsync(UserInfo, employee.DeptCode, "OT", ct))
            return BadRequest(ApiResponse<object>.Fail("Tài khoản này đã có quyền đăng ký OT; hãy dùng luồng đăng ký OT trực tiếp."));

        var rule = await ResolveRuleAsync(employee.DeptCode, "OT", ct);

        var attendance = await _db.HrmAttendanceCalculated.AsNoTracking()
            .Where(x => x.EmployeeCode == dto.EmployeeCode && x.WorkDate.Date == dto.WorkDate.Date)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.CheckInTime, x.CheckOutTime })
            .FirstOrDefaultAsync(ct);

        if (attendance?.CheckInTime == null || attendance.CheckOutTime == null)
            return BadRequest(ApiResponse<object>.Fail("Ngày này chưa có đủ giờ vào/ra nên không có OT thực tế. Hãy dùng \"Phản hồi HR\" để báo sự cố chấm công."));

        var approverId = await ResolveApproverIdAsync(rule, employee.DeptCode, ct);
        if (!approverId.HasValue)
            approverId = await ResolveFeatureOperatorApproverIdAsync(employee.DeptCode, ct);
        if (!approverId.HasValue)
            return BadRequest(ApiResponse<object>.Fail("Không tìm thấy cấp trên/người đăng ký OT được chỉ định cho phòng ban này."));

        var duplicate = await _db.Set<F03SupervisorFeedback>().AnyAsync(x =>
            x.EmployeeCode == dto.EmployeeCode && x.WorkDate.Date == dto.WorkDate.Date &&
            (x.Status == SupervisorFeedbackStatus.PendingSupervisor || x.Status == SupervisorFeedbackStatus.RegisteredPendingApprove), ct);
        if (duplicate) return Conflict(ApiResponse<object>.Fail("Ngày này đã có yêu cầu phản hồi cấp trên đang xử lý."));

        var entity = new F03SupervisorFeedback
        {
            EmployeeId = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            DepartmentCode = employee.DeptCode,
            WorkDate = dto.WorkDate.Date,
            ActualIn = attendance?.CheckInTime,
            ActualOut = attendance?.CheckOutTime,
            ApproverId = approverId.Value,
            Status = SupervisorFeedbackStatus.PendingSupervisor,
            Note = dto.Note,
            CreatedBy = UserInfo.UserId,
            CreatedAt = DateTime.Now
        };

        _db.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<SupervisorFeedbackDto>.Ok(await MapAsync(entity, ct), "Đã gửi phản hồi cấp trên."));
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        var rows = await _db.Set<F03SupervisorFeedback>().AsNoTracking()
            .Where(x => x.EmployeeCode == (UserInfo.EmployeeCode ?? string.Empty))
            .OrderByDescending(x => x.WorkDate).ThenByDescending(x => x.CreatedAt)
            .Take(100).ToListAsync(ct);
        return Ok(ApiResponse<List<SupervisorFeedbackDto>>.Ok(await MapManyAsync(rows, ct, withOtMinutes: false)));
    }

    [HttpGet("pending")]
    public async Task<IActionResult> Pending(CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        var rows = await LoadHandleablePendingAsync(ct);
        return Ok(ApiResponse<List<SupervisorFeedbackDto>>.Ok(await MapManyAsync(rows, ct)));
    }

    /// <summary>Số yêu cầu đang chờ user này xử lý (badge dashboard). 0 nếu không phải registrar.</summary>
    [HttpGet("pending-count")]
    public async Task<IActionResult> PendingCount(CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        var rows = await LoadHandleablePendingAsync(ct);
        return Ok(ApiResponse<int>.Ok(rows.Count));
    }

    /// <summary>Yêu cầu chờ xử lý mà user là người được chỉ định (ApproverId) HOẶC là registrar của phòng ban đó.</summary>
    private async Task<List<F03SupervisorFeedback>> LoadHandleablePendingAsync(CancellationToken ct)
    {
        var pending = await _db.Set<F03SupervisorFeedback>().AsNoTracking()
            .Where(x => x.Status == SupervisorFeedbackStatus.PendingSupervisor)
            .OrderBy(x => x.WorkDate).Take(500).ToListAsync(ct);

        var result = new List<F03SupervisorFeedback>();
        var deptCache = new Dictionary<int, bool>();
        foreach (var row in pending)
        {
            if (row.ApproverId == UserInfo!.UserId) { result.Add(row); continue; }
            var dept = row.DepartmentCode ?? 0;
            if (!deptCache.TryGetValue(dept, out var ok))
                deptCache[dept] = ok = await _policy.IsRegistrarAsync(UserInfo, dept, "OT", ct);
            if (ok) result.Add(row);
        }
        return result;
    }

    /// <summary>Từ chối yêu cầu: bắt buộc có lý do.</summary>
    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] SupervisorFeedbackDecisionDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Note)) return BadRequest(ApiResponse<object>.Fail("Vui lòng nhập lý do từ chối."));
        var entity = await GetOwnedPendingAsync(id, ct);
        if (entity == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy yêu cầu hoặc yêu cầu không thuộc quyền xử lý."));
        entity.Status = SupervisorFeedbackStatus.Rejected;
        entity.ResponseNote = dto.Note.Trim();
        Touch(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<SupervisorFeedbackDto>.Ok(await MapAsync(entity, ct), "Đã từ chối yêu cầu."));
    }

    /// <summary>
    /// Đã có đăng ký OT mà lịch không hiển thị (hoặc lỗi dữ liệu): chuyển HR xử lý.
    /// Cố gắng tạo hồ sơ đối soát HR; nếu hệ thống từ chối (vd đã có đơn OT duyệt) thì vẫn ghi nhận chuyển HR.
    /// </summary>
    [HttpPost("{id:int}/forward-hr")]
    public async Task<IActionResult> ForwardToHr(int id, [FromBody] SupervisorFeedbackDecisionDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Note)) return BadRequest(ApiResponse<object>.Fail("Vui lòng ghi rõ nội dung cần HR kiểm tra."));
        var entity = await GetOwnedPendingAsync(id, ct);
        if (entity == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy yêu cầu hoặc yêu cầu không thuộc quyền xử lý."));

        try { await _execution.EnsureAttendanceFeedbackAsync(entity.EmployeeCode, DateOnly.FromDateTime(entity.WorkDate), ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "[SUPERVISOR_FEEDBACK] Không tạo được hồ sơ đối soát HR cho feedback {Id}", id); }

        entity.Status = SupervisorFeedbackStatus.ForwardedToHr;
        entity.ResponseNote = dto.Note.Trim();
        Touch(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<SupervisorFeedbackDto>.Ok(await MapAsync(entity, ct), "Đã chuyển HR xử lý."));
    }

    /// <summary>Đăng ký bổ sung OT cho nhân viên. Giờ OT lấy từ OT thực tế (hoặc giờ người xử lý chỉnh).</summary>
    [HttpPost("{id:int}/register-ot")]
    public async Task<IActionResult> RegisterOt(int id, [FromBody] SupervisorFeedbackDecisionDto dto, CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        var entity = await GetOwnedPendingAsync(id, ct);
        if (entity == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy yêu cầu hoặc yêu cầu không thuộc quyền xử lý."));
        if (!await _policy.IsRegistrarAsync(UserInfo, entity.DepartmentCode ?? 0, "OT", ct))
            return StatusCode(403, ApiResponse<object>.Fail("Bạn không có quyền đăng ký OT cho phòng ban này."));

        var otMinutes = (await LoadOtMinutesAsync(new List<F03SupervisorFeedback> { entity }, ct)).GetValueOrDefault((entity.EmployeeCode, entity.WorkDate.Date));
        TimeSpan start, end;
        if (dto.StartTime.HasValue && dto.EndTime.HasValue)
        {
            start = dto.StartTime.Value;
            end = dto.EndTime.Value;
        }
        else if (entity.ActualOut.HasValue && otMinutes > 0)
        {
            end = entity.ActualOut.Value.TimeOfDay;
            start = end - TimeSpan.FromMinutes(otMinutes);
        }
        else if (entity.ActualIn.HasValue && entity.ActualOut.HasValue)
        {
            start = entity.ActualIn.Value.TimeOfDay;
            end = entity.ActualOut.Value.TimeOfDay;
        }
        else
        {
            return BadRequest(ApiResponse<object>.Fail("Chưa có giờ OT thực tế; hãy nhập giờ bắt đầu/kết thúc OT."));
        }

        if (end <= start || start < TimeSpan.Zero)
            return BadRequest(ApiResponse<object>.Fail("Giờ OT không hợp lệ."));

        var hours = Math.Max(0m, Math.Round((decimal)(end - start).TotalHours, 1));
        // Always derive OT pay category from synchronized HRM holiday data, not a stale UI value.
        var otType = await ResolveOtTypeCodeAsync(entity.WorkDate, ct);
        var reason = string.IsNullOrWhiteSpace(dto.Note) ? $"Đăng ký OT bổ sung theo phản hồi ngày {entity.WorkDate:dd/MM/yyyy}" : dto.Note.Trim();
        var model = new OTRequestUpsertDto
        {
            EmployeeCode = entity.EmployeeCode,
            DeptCode = entity.DepartmentCode ?? 0,
            OTDate = entity.WorkDate,
            StartTime = start,
            EndTime = end,
            OTTypeCode = otType,
            OTReasonSummary = reason,
            ScopeType = "SELECTED",
            Employees = new List<OTEmployeeDto>
            {
                new()
                {
                    EmployeeCode = entity.EmployeeCode,
                    OTHours = hours,
                    StartTime = start,
                    EndTime = end,
                    OTTypeCode = otType,
                    OTRateMultiplier = otType switch { "HOL" => 3.0m, "RESTDAY" => 2.0m, _ => 1.5m },
                    OTReasonDetail = reason
                }
            },
            ApprovalSelections = dto.ApprovalSelections ?? new List<FVN_REGISTER.Contract.Requests.Approvals.ApprovalSelectionDto>()
        };

        var result = await _otService.CreateAsync(model, UserInfo, ct);
        if (!result.IsSuccess || result.Data <= 0)
            return BadRequest(ApiResponse<object>.Fail(result.Message ?? "Không thể tạo đơn OT."));

        entity.OTRequestId = result.Data;
        entity.Status = SupervisorFeedbackStatus.RegisteredPendingApprove;
        entity.ResponseNote = string.IsNullOrWhiteSpace(dto.Note) ? entity.ResponseNote : dto.Note.Trim();
        Touch(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<SupervisorFeedbackDto>.Ok(await MapAsync(entity, ct), "Đã đăng ký OT, chờ cấp duyệt tiếp theo."));
    }

    private void Touch(F03SupervisorFeedback entity)
    {
        entity.ResolvedAt = DateTime.Now;
        entity.ModifiedBy = UserInfo!.UserId;
        entity.ModifiedAt = DateTime.Now;
    }

    private async Task<F03SupervisorFeedback?> GetOwnedPendingAsync(int id, CancellationToken ct)
    {
        if (UserInfo == null) return null;
        var entity = await _db.Set<F03SupervisorFeedback>().FirstOrDefaultAsync(x => x.Id == id && x.Status == SupervisorFeedbackStatus.PendingSupervisor, ct);
        if (entity == null) return null;
        if (entity.ApproverId == UserInfo.UserId) return entity;
        return await _policy.IsRegistrarAsync(UserInfo, entity.DepartmentCode ?? 0, "OT", ct) ? entity : null;
    }

    private async Task<int?> ResolveFeatureOperatorApproverIdAsync(int deptCode, CancellationToken ct)
    {
        var block = await _db.Set<FVN_REGISTER.Core.Entities.HR.F03Department>().AsNoTracking()
            .Where(d => d.DeptCode == deptCode).Select(d => d.BlockCode).FirstOrDefaultAsync(ct);
        var assignments = await _db.Set<FVN_REGISTER.Core.Entities.Security.F03FeatureOperatorAssignment>().AsNoTracking()
            .Where(x => x.IsActive == true
                        && x.FunctionCode == SecurityFunctionCodes.OTRegistrarManage
                        && x.ResourceType == FeatureOperatorCatalog.OtRegistrar)
            .Select(x => new { x.EmployeeCode, x.ResourceId, x.ScopeCode }).ToListAsync(ct);
        var codes = assignments
            .Where(x => FeatureOperatorAuthorizationPolicy.OtRegistrarCovers(x.ResourceId, x.ScopeCode, deptCode, block))
            .Select(x => x.EmployeeCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        if (codes.Count == 0) return null;
        return await _db.Users.AsNoTracking()
            .Where(u => codes.Contains(u.EmployeeCode) && u.IsActive == true)
            .OrderByDescending(u => u.DeptCode == deptCode)
            .Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);
    }

    private async Task<bool> CanRegisterForOthersAsync(F03OTRegistrationRule? rule, CancellationToken ct)
    {
        if (UserInfo == null || rule == null) return false;
        return rule.Mode switch
        {
            RegistrationRuleMode.Everyone => true,
            RegistrationRuleMode.AssignedUsers => await _db.Set<F03OTRegistrar>().AnyAsync(x => x.RegistrationRuleId == rule.Id && x.UserId == UserInfo.UserId && x.RegisterType == rule.RegisterType, ct),
            RegistrationRuleMode.AssignedPositions => !string.IsNullOrWhiteSpace(UserInfo.PositionCode) && await _db.Set<F03OTRegistrarPosition>().AnyAsync(x => x.RegistrationRuleId == rule.Id && x.PositionCode == UserInfo.PositionCode && x.RegisterType == rule.RegisterType, ct),
            _ => false
        };
    }

    private async Task<int?> ResolveApproverIdAsync(F03OTRegistrationRule? rule, int deptCode, CancellationToken ct)
    {
        if (rule == null || rule.Mode == RegistrationRuleMode.Everyone) return null;
        if (rule.Mode == RegistrationRuleMode.AssignedUsers)
            return await _db.Users.AsNoTracking().Where(x => rule.Registrars.Select(r => r.UserId).Contains(x.Id) && x.DeptCode == deptCode && x.IsActive == true).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);

        var positions = rule.RegistrarPositions.Select(x => x.PositionCode).ToList();
        return await (from u in _db.Users.AsNoTracking()
                      join e in _db.Employees.AsNoTracking() on u.EmployeeCode equals e.EmployeeCode
                      where u.DeptCode == deptCode && u.IsActive == true && e.IsActive == true && positions.Contains(e.PositionCode)
                      select (int?)u.Id).FirstOrDefaultAsync(ct);
    }

    private async Task<F03OTRegistrationRule?> ResolveRuleAsync(int deptCode, string type, CancellationToken ct)
        => await _db.Set<F03OTRegistrationRule>().AsNoTracking().Include(x => x.Registrars).Include(x => x.RegistrarPositions)
            .Where(x => x.IsActive == true && x.RegisterType == type && (x.DepartmentCode == deptCode || x.DepartmentCode == 0))
            .OrderByDescending(x => x.DepartmentCode == deptCode).FirstOrDefaultAsync(ct);

    private async Task<SupervisorFeedbackDto> MapAsync(F03SupervisorFeedback x, CancellationToken ct)
        => (await MapManyAsync(new List<F03SupervisorFeedback> { x }, ct))[0];

    private sealed class OtMinutesRow
    {
        public int OTMinutesDay { get; set; }
        public int OTMinutesNight { get; set; }
        public int OTMinutesDayTC { get; set; }
        public int OTMinutesNightTC { get; set; }
    }

    /// <summary>
    /// OT thực tế (phút) theo chấm công HRM, cùng cách tính với đối soát (OTMinutes Day/Night/DayTC/NightTC).
    /// Các cột này chưa có trong entity nên đọc bằng SQL; chỉ gọi cho danh sách ngắn (Pending / từng bản ghi).
    /// </summary>
    private async Task<Dictionary<(string Code, DateTime Date), int>> LoadOtMinutesAsync(List<F03SupervisorFeedback> rows, CancellationToken ct)
    {
        var result = new Dictionary<(string, DateTime), int>();
        foreach (var key in rows.Select(x => (x.EmployeeCode, Date: x.WorkDate.Date)).Distinct().Take(100))
        {
            var found = await _db.Database.SqlQuery<OtMinutesRow>($@"
                SELECT TOP (1) a.OTMinutesDay, a.OTMinutesNight, a.OTMinutesDayTC, a.OTMinutesNightTC
                FROM dbo.F03HrmAttendanceCalculated AS a
                WHERE a.EmployeeCode = {key.EmployeeCode} AND a.WorkDate = {key.Date}
                  AND a.CheckInTime IS NOT NULL AND a.CheckOutTime IS NOT NULL
                ORDER BY a.Id DESC").ToListAsync(ct);
            var row = found.FirstOrDefault();
            if (row != null)
                result[(key.EmployeeCode, key.Date)] = Math.Max(0, row.OTMinutesDay) + Math.Max(0, row.OTMinutesNight) + Math.Max(0, row.OTMinutesDayTC) + Math.Max(0, row.OTMinutesNightTC);
        }
        return result;
    }

    private sealed class HolidayClassificationRow
    {
        public DateTime HolidayDate { get; set; }
        public string? DayType { get; set; }
        public string? HrmTypeCode { get; set; }
    }

    private static string OtTypeForHoliday(string? dayType, string? hrmTypeCode, DateTime workDate)
    {
        var normalizedDayType = (dayType ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedHrmCode = (hrmTypeCode ?? string.Empty).Trim().TrimStart('0');
        if (normalizedDayType == "NATIONAL_HOLIDAY" || normalizedHrmCode == "3")
            return "HOL";
        if (normalizedDayType is "COMPANY_HOLIDAY" or "SUNDAY" || normalizedHrmCode is "1" or "2" || workDate.DayOfWeek == DayOfWeek.Sunday)
            return "RESTDAY";
        return "WD";
    }

    private async Task<string> ResolveOtTypeCodeAsync(DateTime workDate, CancellationToken ct)
    {
        var date = workDate.Date;
        var rows = await _db.Database.SqlQuery<HolidayClassificationRow>($@"
            SELECT TOP (1)
                   CAST(h.HolidayDate AS date) AS HolidayDate,
                   h.DayType,
                   h.HrmTypeCode
            FROM dbo.F03CompanyHolidays AS h
            WHERE CAST(h.HolidayDate AS date) = {date}
              AND h.IsActive = 1
              AND h.Source = N'HRM'
            ORDER BY CASE
                WHEN h.DayType = N'NATIONAL_HOLIDAY' OR h.HrmTypeCode IN ('3','03','003','0003') THEN 1
                WHEN h.DayType IN (N'COMPANY_HOLIDAY', N'SUNDAY') OR h.HrmTypeCode IN ('1','01','001','0001','2','02','002','0002') THEN 2
                ELSE 3 END").ToListAsync(ct);
        var holiday = rows.FirstOrDefault();
        return holiday == null
            ? (date.DayOfWeek == DayOfWeek.Sunday ? "RESTDAY" : "WD")
            : OtTypeForHoliday(holiday.DayType, holiday.HrmTypeCode, date);
    }

    private async Task<Dictionary<DateTime, string>> LoadSuggestedOtTypesAsync(List<F03SupervisorFeedback> rows, CancellationToken ct)
    {
        var dates = rows.Select(x => x.WorkDate.Date).Distinct().ToList();
        if (dates.Count == 0) return new Dictionary<DateTime, string>();
        var minDate = dates.Min();
        var maxDate = dates.Max();
        var holidays = await _db.Database.SqlQuery<HolidayClassificationRow>($@"
            SELECT CAST(h.HolidayDate AS date) AS HolidayDate, h.DayType, h.HrmTypeCode
            FROM dbo.F03CompanyHolidays AS h
            WHERE CAST(h.HolidayDate AS date) >= {minDate}
              AND CAST(h.HolidayDate AS date) <= {maxDate}
              AND h.IsActive = 1
              AND h.Source = N'HRM'
            ORDER BY CASE
                WHEN h.DayType = N'NATIONAL_HOLIDAY' OR h.HrmTypeCode IN ('3','03','003','0003') THEN 1
                WHEN h.DayType IN (N'COMPANY_HOLIDAY', N'SUNDAY') OR h.HrmTypeCode IN ('1','01','001','0001','2','02','002','0002') THEN 2
                ELSE 3 END").ToListAsync(ct);
        var byDate = holidays.GroupBy(x => x.HolidayDate.Date)
            .ToDictionary(g => g.Key, g => g.First());
        return dates.ToDictionary(date => date, date =>
            byDate.TryGetValue(date, out var holiday)
                ? OtTypeForHoliday(holiday.DayType, holiday.HrmTypeCode, date)
                : (date.DayOfWeek == DayOfWeek.Sunday ? "RESTDAY" : "WD"));
    }

    private async Task<List<SupervisorFeedbackDto>> MapManyAsync(List<F03SupervisorFeedback> rows, CancellationToken ct, bool withOtMinutes = true)
    {
        var codes = rows.Select(x => x.EmployeeCode).Distinct().ToList();
        var names = await _db.Employees.AsNoTracking().Where(e => codes.Contains(e.EmployeeCode)).Select(e => new { e.EmployeeCode, e.EmployeeName }).ToDictionaryAsync(x => x.EmployeeCode, x => x.EmployeeName, ct);
        var otMinutes = withOtMinutes ? await LoadOtMinutesAsync(rows, ct) : new Dictionary<(string Code, DateTime Date), int>();
        var suggestedTypes = await LoadSuggestedOtTypesAsync(rows, ct);
        return rows.Select(x => new SupervisorFeedbackDto
        {
            Id = x.Id, EmployeeId = x.EmployeeId, EmployeeCode = x.EmployeeCode,
            EmployeeName = names.TryGetValue(x.EmployeeCode, out var n) ? n : null,
            DepartmentCode = x.DepartmentCode, WorkDate = x.WorkDate, ActualIn = x.ActualIn, ActualOut = x.ActualOut,
            Status = x.Status, ApproverId = x.ApproverId, Note = x.Note, ResponseNote = x.ResponseNote,
            ActualOtMinutes = otMinutes.GetValueOrDefault((x.EmployeeCode, x.WorkDate.Date)),
            SuggestedOTTypeCode = suggestedTypes.GetValueOrDefault(x.WorkDate.Date, x.WorkDate.DayOfWeek == DayOfWeek.Sunday ? "RESTDAY" : "WD"),
            OTRequestId = x.OTRequestId
        }).ToList();
    }
}
