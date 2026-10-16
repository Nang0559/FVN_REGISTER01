using AppAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;
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

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/supervisor-feedback")]
public sealed class SupervisorFeedbackController : BaseApiController
{
    private readonly FVNWEBAPPContext _db;
    private readonly AppAuthorizationService _authorization;
    private readonly IOTService _otService;

    public SupervisorFeedbackController(
        FVNWEBAPPContext db,
        AppAuthorizationService authorization,
        IOTService otService,
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<SupervisorFeedbackController> logger,
        Microsoft.Extensions.Options.IOptionsMonitor<Application.Configuration.AuthDebugOptions> options)
        : base(currentUser, userLog, logger, options)
    {
        _db = db;
        _authorization = authorization;
        _otService = otService;
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

        var rule = await ResolveRuleAsync(employee.DeptCode ?? 0, "OT", ct);
        if (await CanRegisterForOthersAsync(rule, ct))
            return BadRequest(ApiResponse<object>.Fail("Tài khoản này đã có quyền đăng ký OT cho người khác; hãy dùng luồng đăng ký OT trực tiếp."));

        var attendance = await _db.HrmAttendanceCalculated.AsNoTracking()
            .Where(x => x.EmployeeCode == dto.EmployeeCode && x.WorkDate.Date == dto.WorkDate.Date)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.CheckInTime, x.CheckOutTime })
            .FirstOrDefaultAsync(ct);

        var approverId = await ResolveApproverIdAsync(rule, employee.DeptCode ?? 0, ct);
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
            .Where(x => x.EmployeeCode == UserInfo.EmployeeCode)
            .OrderByDescending(x => x.WorkDate).ThenByDescending(x => x.CreatedAt)
            .Take(100).ToListAsync(ct);
        return Ok(ApiResponse<List<SupervisorFeedbackDto>>.Ok(await MapManyAsync(rows, ct)));
    }

    [HttpGet("pending")]
    public async Task<IActionResult> Pending(CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        var rows = await _db.Set<F03SupervisorFeedback>().AsNoTracking()
            .Where(x => x.ApproverId == UserInfo.UserId && x.Status == SupervisorFeedbackStatus.PendingSupervisor)
            .OrderBy(x => x.WorkDate).ToListAsync(ct);
        return Ok(ApiResponse<List<SupervisorFeedbackDto>>.Ok(await MapManyAsync(rows, ct)));
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] SupervisorFeedbackDecisionDto dto, CancellationToken ct)
    {
        var entity = await GetOwnedPendingAsync(id, ct);
        if (entity == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy yêu cầu hoặc yêu cầu không thuộc quyền xử lý."));
        entity.Status = SupervisorFeedbackStatus.Rejected;
        entity.Note = string.IsNullOrWhiteSpace(dto.Note) ? entity.Note : dto.Note.Trim();
        entity.ResolvedAt = DateTime.Now;
        entity.ModifiedBy = UserInfo!.UserId;
        entity.ModifiedAt = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<SupervisorFeedbackDto>.Ok(await MapAsync(entity, ct), "Đã từ chối yêu cầu."));
    }

    [HttpPost("{id:int}/register-ot")]
    public async Task<IActionResult> RegisterOt(int id, [FromBody] SupervisorFeedbackDecisionDto dto, CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        var entity = await GetOwnedPendingAsync(id, ct);
        if (entity == null) return NotFound(ApiResponse<object>.Fail("Không tìm thấy yêu cầu hoặc yêu cầu không thuộc quyền xử lý."));
        if (!entity.ActualIn.HasValue || !entity.ActualOut.HasValue || entity.ActualOut <= entity.ActualIn)
            return BadRequest(ApiResponse<object>.Fail("Lịch chưa có đủ giờ thực tế để đăng ký OT."));

        var hours = (decimal)(entity.ActualOut.Value - entity.ActualIn.Value).TotalHours;
        var start = entity.ActualIn.Value.TimeOfDay;
        var end = entity.ActualOut.Value.TimeOfDay;
        var model = new OTRequestUpsertDto
        {
            EmployeeCode = UserInfo.EmployeeCode,
            DeptCode = entity.DepartmentCode,
            OTDate = entity.WorkDate,
            StartTime = start,
            EndTime = end,
            OTTypeCode = string.IsNullOrWhiteSpace(dto.OTTypeCode) ? "WEEKDAY" : dto.OTTypeCode.Trim(),
            OTReasonSummary = string.IsNullOrWhiteSpace(dto.Note) ? $"Đăng ký OT theo phản hồi cấp trên ngày {entity.WorkDate:dd/MM/yyyy}" : dto.Note.Trim(),
            ScopeType = "SELECTED",
            Employees = new List<OTEmployeeDto>
            {
                new()
                {
                    EmployeeCode = entity.EmployeeCode,
                    OTHours = Math.Max(0m, Math.Round(hours, 1)),
                    StartTime = start,
                    EndTime = end,
                    OTTypeCode = string.IsNullOrWhiteSpace(dto.OTTypeCode) ? "WEEKDAY" : dto.OTTypeCode.Trim(),
                    OTReasonDetail = dto.Note
                }
            },
            ApprovalSelections = dto.ApprovalSelections ?? new List<Contract.Dtos.Approvals.ApprovalSelectionDto>()
        };

        var result = await _otService.CreateAsync(model, UserInfo, ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<object>.Fail(result.Message ?? "Không thể tạo đơn OT."));

        entity.OTRequestId = result.Data;
        entity.Status = SupervisorFeedbackStatus.RegisteredPendingApprove;
        entity.Note = dto.Note ?? entity.Note;
        entity.ResolvedAt = DateTime.Now;
        entity.ModifiedBy = UserInfo.UserId;
        entity.ModifiedAt = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<SupervisorFeedbackDto>.Ok(await MapAsync(entity, ct), "Đã đăng ký OT, chờ cấp duyệt tiếp theo."));
    }

    private async Task<F03SupervisorFeedback?> GetOwnedPendingAsync(int id, CancellationToken ct)
    {
        if (UserInfo == null) return null;
        return await _db.Set<F03SupervisorFeedback>().FirstOrDefaultAsync(x => x.Id == id && x.ApproverId == UserInfo.UserId && x.Status == SupervisorFeedbackStatus.PendingSupervisor, ct);
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
            return await _db.Users.AsNoTracking().Where(x => rule.Registrars.Select(r => r.UserId).Contains(x.Id) && x.DeptCode == deptCode && x.LockoutEnable).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);

        var positions = rule.RegistrarPositions.Select(x => x.PositionCode).ToList();
        return await (from u in _db.Users.AsNoTracking()
                      join e in _db.Employees.AsNoTracking() on u.EmployeeCode equals e.EmployeeCode
                      where u.DeptCode == deptCode && u.LockoutEnable && positions.Contains(e.PositionCode)
                      select (int?)u.Id).FirstOrDefaultAsync(ct);
    }

    private async Task<F03OTRegistrationRule?> ResolveRuleAsync(int deptCode, string type, CancellationToken ct)
        => await _db.Set<F03OTRegistrationRule>().AsNoTracking().Include(x => x.Registrars).Include(x => x.RegistrarPositions)
            .Where(x => x.IsActive && x.RegisterType == type && (x.DepartmentCode == deptCode || x.DepartmentCode == 0))
            .OrderByDescending(x => x.DepartmentCode == deptCode).FirstOrDefaultAsync(ct);

    private async Task<SupervisorFeedbackDto> MapAsync(F03SupervisorFeedback x, CancellationToken ct)
    {
        var name = await _db.Employees.AsNoTracking().Where(e => e.EmployeeCode == x.EmployeeCode).Select(e => e.EmployeeName).FirstOrDefaultAsync(ct);
        return new SupervisorFeedbackDto { Id = x.Id, EmployeeId = x.EmployeeId, EmployeeCode = x.EmployeeCode, EmployeeName = name, DepartmentCode = x.DepartmentCode, WorkDate = x.WorkDate, ActualIn = x.ActualIn, ActualOut = x.ActualOut, Status = x.Status, ApproverId = x.ApproverId, Note = x.Note };
    }

    private async Task<List<SupervisorFeedbackDto>> MapManyAsync(List<F03SupervisorFeedback> rows, CancellationToken ct)
    {
        var codes = rows.Select(x => x.EmployeeCode).Distinct().ToList();
        var names = await _db.Employees.AsNoTracking().Where(e => codes.Contains(e.EmployeeCode)).Select(e => new { e.EmployeeCode, e.EmployeeName }).ToDictionaryAsync(x => x.EmployeeCode, x => x.EmployeeName, ct);
        return rows.Select(x => new SupervisorFeedbackDto { Id = x.Id, EmployeeId = x.EmployeeId, EmployeeCode = x.EmployeeCode, EmployeeName = names.TryGetValue(x.EmployeeCode, out var n) ? n : null, DepartmentCode = x.DepartmentCode, WorkDate = x.WorkDate, ActualIn = x.ActualIn, ActualOut = x.ActualOut, Status = x.Status, ApproverId = x.ApproverId, Note = x.Note }).ToList();
    }
}
