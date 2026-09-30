using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/calendar")]
public sealed class WorkCalendarController : BaseApiController
{
    private readonly ISharedWorkCalendarService _calendar;
    private readonly IWorkCalendarService _workCalendar;
    private readonly IAuthorizationService _authorization;

    public WorkCalendarController(
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<WorkCalendarController> logger,
        IOptionsMonitor<AuthDebugOptions> options,
        ISharedWorkCalendarService calendar,
        IWorkCalendarService workCalendar,
        IAuthorizationService authorization)
        : base(currentUser, userLog, logger, options)
    {
        _calendar = calendar;
        _workCalendar = workCalendar;
        _authorization = authorization;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        if (UserInfo?.UserId is not int userId || string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không có định danh nhân viên hợp lệ."));

        var employeeCode = UserInfo.EmployeeCode.Trim();
        var modules = await GetAuthorizedModulesAsync(UserInfo, includeOwnData: true, ct);
        if (!await CanViewOwnCalendarAsync(UserInfo, modules, ct))
            return Forbid();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = from ?? new DateOnly(today.Year, today.Month, 1);
        var last = to ?? new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

        if (last < first)
            return BadRequest(ApiResponse<object>.Fail("Khoảng ngày không hợp lệ."));

        if (last.DayNumber - first.DayNumber > 93)
            return BadRequest(ApiResponse<object>.Fail("Lịch chỉ cho phép tối đa 94 ngày mỗi lần tải."));

        var result = await _calendar.GetMonthAsync(employeeCode, userId, first, last, modules, ct);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("employee/{employeeCode}")]
    public async Task<IActionResult> GetEmployee(
        string employeeCode,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        if (UserInfo?.UserId is not int userId || string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không có định danh nhân viên hợp lệ."));

        employeeCode = employeeCode?.Trim() ?? string.Empty;
        if (employeeCode.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Mã nhân viên không được để trống."));

        var isOwnEmployee = string.Equals(
            UserInfo.EmployeeCode.Trim(),
            employeeCode,
            StringComparison.OrdinalIgnoreCase);
        var modules = await GetAuthorizedModulesAsync(UserInfo, includeOwnData: isOwnEmployee, ct);

        if (isOwnEmployee)
        {
            if (!await CanViewOwnCalendarAsync(UserInfo, modules, ct))
                return Forbid();
        }
        else
        {
            if (!await _authorization.HasAsync(UserInfo, SecurityFunctionCodes.CalendarView, ct))
                return Forbid();

            if (modules.Count == 0)
                return Forbid();
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = from ?? new DateOnly(today.Year, today.Month, 1);
        var last = to ?? new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        if (last < first || last.DayNumber - first.DayNumber > 93)
            return BadRequest(ApiResponse<object>.Fail("Khoảng ngày không hợp lệ hoặc vượt quá 94 ngày."));

        var result = await _calendar.GetMonthAsync(employeeCode, userId, first, last, modules, ct);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("me/alerts")]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        if (UserInfo?.UserId is not int userId || string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không có định danh nhân viên hợp lệ."));

        var employeeCode = UserInfo.EmployeeCode.Trim();
        var modules = await GetAuthorizedModulesAsync(UserInfo, includeOwnData: true, ct);
        if (!await CanViewOwnCalendarAsync(UserInfo, modules, ct))
            return Forbid();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = from ?? today.AddDays(-30);
        var last = to ?? today.AddDays(30);

        if (last < first)
            return BadRequest(ApiResponse<object>.Fail("Khoảng ngày không hợp lệ."));

        var result = await _calendar.GetAlertsAsync(employeeCode, userId, first, last, modules, ct);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("employee/{employeeCode}/alerts")]
    public async Task<IActionResult> GetEmployeeAlerts(
        string employeeCode,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        if (UserInfo?.UserId is not int userId || string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không có định danh nhân viên hợp lệ."));

        employeeCode = employeeCode?.Trim() ?? string.Empty;
        if (employeeCode.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Mã nhân viên không được để trống."));

        var isOwnEmployee = string.Equals(
            UserInfo.EmployeeCode.Trim(),
            employeeCode,
            StringComparison.OrdinalIgnoreCase);
        var modules = await GetAuthorizedModulesAsync(UserInfo, includeOwnData: isOwnEmployee, ct);

        if (isOwnEmployee)
        {
            if (!await CanViewOwnCalendarAsync(UserInfo, modules, ct))
                return Forbid();
        }
        else
        {
            if (!await _authorization.HasAsync(UserInfo, SecurityFunctionCodes.CalendarView, ct))
                return Forbid();

            if (modules.Count == 0)
                return Forbid();
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = from ?? today.AddDays(-30);
        var last = to ?? today.AddDays(30);
        if (last < first)
            return BadRequest(ApiResponse<object>.Fail("Khoảng ngày không hợp lệ."));

        var result = await _calendar.GetAlertsAsync(employeeCode, userId, first, last, modules, ct);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("me/registration-opportunities")]
    public async Task<IActionResult> GetRegistrationOpportunities(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(UserInfo?.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không có định danh nhân viên hợp lệ."));

        var employeeCode = UserInfo.EmployeeCode.Trim();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = from ?? today;
        var last = to ?? today.AddDays(31);

        if (last < first)
            return BadRequest(ApiResponse<object>.Fail("Khoảng ngày không hợp lệ."));

        if (last.DayNumber - first.DayNumber > 93)
            return BadRequest(ApiResponse<object>.Fail("Lịch chỉ cho phép tối đa 94 ngày mỗi lần tải."));

        var result = await _workCalendar.GetRegistrationOpportunitiesAsync(
            employeeCode,
            first.ToDateTime(TimeOnly.MinValue),
            last.ToDateTime(TimeOnly.MinValue),
            ct);

        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("me/availability")]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] DateOnly date,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(UserInfo?.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không có định danh nhân viên hợp lệ."));

        var employeeCode = UserInfo.EmployeeCode.Trim();
        var result = await _workCalendar.GetAvailabilityAsync(
            employeeCode,
            date.ToDateTime(TimeOnly.MinValue),
            ct);

        return Ok(ApiResponse<object>.Ok(result));
    }

    private async Task<bool> CanViewOwnCalendarAsync(
        FVN_REGISTER.Contract.Dtos.Authentication.UserIdentityDto user,
        IReadOnlySet<string> modules,
        CancellationToken ct)
    {
        if (await _authorization.HasAsync(user, SecurityFunctionCodes.CalendarView, ct))
            return true;

        // Own calendar is self-service data: modules already contains the caller's own modules.
        // Self-service calendar access can come from any module view capability. This never expands
        // the caller's data scope to another employee; cross-employee access remains Calendar.View + ManagedScope.
        return modules.Count > 0;
    }

    private async Task<HashSet<string>> GetAuthorizedModulesAsync(
        FVN_REGISTER.Contract.Dtos.Authentication.UserIdentityDto user,
        bool includeOwnData,
        CancellationToken ct)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Self-service: every authenticated employee can see their own OT / leave /
        // trip / attendance on "Lịch của tôi". This does not depend on department-scoped
        // module View capabilities and never widens access to another employee's data.
        if (includeOwnData)
        {
            result.Add("OT");
            result.Add("LEAVE");
            result.Add("TRIP");
            result.Add("ATTENDANCE");
            return result;
        }

        if (await _authorization.HasAsync(user, SecurityFunctionCodes.OTView, ct))
            result.Add("OT");

        if (await _authorization.HasAsync(user, SecurityFunctionCodes.LeaveView, ct))
            result.Add("LEAVE");

        if (await _authorization.HasAsync(user, SecurityFunctionCodes.TripView, ct))
            result.Add("TRIP");

        if (await _authorization.HasAsync(user, SecurityFunctionCodes.AttendanceView, ct))
            result.Add("ATTENDANCE");

        return result;
    }
}
