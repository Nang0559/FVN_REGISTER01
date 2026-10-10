using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Orchestrators;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Contract.Dtos.Dashboard;
using FVN_REGISTER.Core.Entities.Approvers;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Entities.Leaves;
using FVN_REGISTER.Core.Entities.OT;
using FVN_REGISTER.Core.Entities.Trips;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : BaseApiController
    {
        private readonly IDashboardOrchestrator _dashboardOrchestrator;
        private readonly FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService _authorization;
        private readonly FVNWEBAPPContext _db;

        public DashboardController(
            ICurrentUserService currentUser,
            IUserLogService userLog,
            ILogger<DashboardController> logger,
            IOptionsMonitor<AuthDebugOptions> options,
            IDashboardOrchestrator dashboardOrchestrator,
            FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService authorization,
            FVNWEBAPPContext db)
            : base(currentUser, userLog, logger, options)
        {
            _dashboardOrchestrator = dashboardOrchestrator;
            _authorization = authorization;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData(CancellationToken ct)
        {
            if (UserInfo == null)
                return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không hợp lệ hoặc đã hết hạn."));
            // Approvers provisioned from HRM may need the Home dashboard even when
            // their role does not carry the standalone DashboardView capability.
            // This only permits entering the dashboard endpoint; each provider must
            // still enforce its own module capability and department/policy scope.
            var canViewDashboard = await _authorization.HasAsync(
                UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.DashboardView, ct);
            var canApproveLeave = await _authorization.HasAsync(
                UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.LeaveApprove, ct);
            var canApproveOt = await _authorization.HasAsync(
                UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.OTApprove, ct);
            var canApproveTrip = await _authorization.HasAsync(
                UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.TripApprove, ct);

            if (!canViewDashboard && !canApproveLeave && !canApproveOt && !canApproveTrip)
                return Forbid();

            var result = await _dashboardOrchestrator.BuildAsync(UserInfo, ct);
            await LogActionAsync("Xem dữ liệu Dashboard");
            return HandleResult(result);
        }

        /// <summary>
        /// Read-only attendance summary for departments explicitly covered by the
        /// authenticated approver's active position/level policies. This endpoint
        /// intentionally reports calculated rows only; it does not infer absence
        /// from a missing calculated row and never triggers an HRM calculation.
        /// </summary>
        [HttpGet("attendance-shifts")]
        public async Task<IActionResult> GetAttendanceShiftDashboard(
            [FromQuery] DateOnly? workDate,
            CancellationToken ct)
        {
            if (UserInfo == null)
                return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không hợp lệ hoặc đã hết hạn."));

            var canViewDashboard = await _authorization.HasAsync(
                UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.DashboardView, ct);
            var canApproveLeave = await _authorization.HasAsync(
                UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.LeaveApprove, ct);
            var canApproveOt = await _authorization.HasAsync(
                UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.OTApprove, ct);
            var canApproveTrip = await _authorization.HasAsync(
                UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.TripApprove, ct);

            if (!canViewDashboard && !canApproveLeave && !canApproveOt && !canApproveTrip)
                return Forbid();

            // No active approval-policy match means no department-level attendance data.
            // Generic DashboardView permission must not widen the approver's department scope.
            // DepartmentCode on F03ApprovalPolicies represents the requester's
            // department scope. For this dashboard, the approver must only see
            // attendance belonging to their own HRM department. Do not union other
            // policy departments here: cross-department approval is configured
            // separately and must not widen the attendance dashboard.
            if (!UserInfo.DeptCode.HasValue)
                return Ok(ApiResponse<IReadOnlyList<AttendanceShiftDashboardDto>>.Ok(
                    Array.Empty<AttendanceShiftDashboardDto>()));

            var policyQuery = _db.ApprovalPolicies.AsNoTracking()
                .Where(p => p.IsActive == true
                    && p.DeptCode == UserInfo.DeptCode.Value
                    && p.ApprovalPositionCode == UserInfo.PositionCode
                    && p.Level == UserInfo.LevelApprove);

            var allowedDepartments = new HashSet<int>();
            if (canApproveLeave)
                allowedDepartments.UnionWith(await policyQuery.Where(p => p.RequestType == RequestModule.Leave)
                    .Select(p => p.DeptCode).Distinct().ToListAsync(ct));
            if (canApproveOt)
                allowedDepartments.UnionWith(await policyQuery.Where(p => p.RequestType == RequestModule.Overtime)
                    .Select(p => p.DeptCode).Distinct().ToListAsync(ct));
            if (canApproveTrip)
                allowedDepartments.UnionWith(await policyQuery.Where(p => p.RequestType == RequestModule.Trip)
                    .Select(p => p.DeptCode).Distinct().ToListAsync(ct));

            if (allowedDepartments.Count == 0)
                return Ok(ApiResponse<IReadOnlyList<AttendanceShiftDashboardDto>>.Ok(
                    Array.Empty<AttendanceShiftDashboardDto>()));

            var day = (workDate ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
            var nextDay = day.AddDays(1);
            var rows = await _db.HrmAttendanceCalculated.AsNoTracking()
                .Where(a => a.WorkDate >= day && a.WorkDate < nextDay
                    && a.DeptCode.HasValue && allowedDepartments.Contains(a.DeptCode.Value))
                .Join(_db.Departments.AsNoTracking(),
                    a => a.DeptCode,
                    d => (int?)d.DeptCode,
                    (a, d) => new { Attendance = a, DepartmentName = d.DeptName })
                .OrderBy(x => x.Attendance.DeptCode)
                .ThenBy(x => x.Attendance.ShiftAbbr)
                .ThenBy(x => x.Attendance.EmployeeCode)
                .Select(x => new
                {
                    x.Attendance.WorkDate,
                    DepartmentCode = x.Attendance.DeptCode!.Value,
                    DepartmentName = x.DepartmentName,
                    ShiftAbbr = x.Attendance.ShiftAbbr ?? "",
                    EmployeeCode = x.Attendance.EmployeeCode ?? "",
                    EmployeeName = x.Attendance.FullName ?? "",
                    x.Attendance.CheckInTime,
                    x.Attendance.CheckOutTime,
                    x.Attendance.AttendanceDisplayValue,
                    x.Attendance.OtDisplayValue
                })
                .ToListAsync(ct);

            var result = rows
                .GroupBy(x => new { x.WorkDate, x.DepartmentCode, x.DepartmentName, x.ShiftAbbr })
                .Select(g => new AttendanceShiftDashboardDto
                {
                    WorkDate = g.Key.WorkDate,
                    DepartmentCode = g.Key.DepartmentCode,
                    DepartmentName = g.Key.DepartmentName ?? g.Key.DepartmentCode.ToString(),
                    ShiftAbbr = g.Key.ShiftAbbr,
                    CalculatedEmployees = g.Count(),
                    CheckedInEmployees = g.Count(x => x.CheckInTime.HasValue),
                    CheckedOutEmployees = g.Count(x => x.CheckOutTime.HasValue),
                    MissingCheckInOrOut = g.Count(x => !x.CheckInTime.HasValue || !x.CheckOutTime.HasValue),
                    Employees = g.Select(x => new AttendanceShiftEmployeeDto
                    {
                        EmployeeCode = x.EmployeeCode,
                        EmployeeName = x.EmployeeName,
                        CheckInTime = x.CheckInTime,
                        CheckOutTime = x.CheckOutTime,
                        AttendanceDisplayValue = x.AttendanceDisplayValue,
                        OtDisplayValue = x.OtDisplayValue,
                        Status = x.CheckInTime.HasValue && x.CheckOutTime.HasValue
                            ? "CHECKED_OUT"
                            : x.CheckInTime.HasValue ? "CHECKED_IN"
                            : x.CheckOutTime.HasValue ? "MISSING_CHECK_IN"
                            : "NO_RECORDED_PUNCH"
                    }).ToList()
                })
                .OrderBy(x => x.DepartmentCode)
                .ThenBy(x => x.ShiftAbbr)
                .ToList();

            await LogActionAsync("Xem Dashboard chấm công theo ca");
            return Ok(ApiResponse<IReadOnlyList<AttendanceShiftDashboardDto>>.Ok(result));
        }

        /// <summary>
        /// Employee-level details for the approver's department dashboard cards.
        /// Returns only approved requests and enforces the signed-in approver's active policy scope.
        /// </summary>
        [HttpGet("department-details")]
        public async Task<IActionResult> GetDepartmentDetails(
            [FromQuery] string kind,
            [FromQuery] DateOnly? workDate,
            CancellationToken ct)
        {
            if (UserInfo == null)
                return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không hợp lệ hoặc đã hết hạn."));

            var normalizedKind = (kind ?? string.Empty).Trim().ToUpperInvariant();
            if (normalizedKind is not ("ATTENDANCE" or "LEAVE" or "TRIP" or "OT"))
                return BadRequest(ApiResponse<object>.Fail("Loại thống kê không hợp lệ."));

            var canView = normalizedKind switch
            {
                "LEAVE" => await _authorization.HasAsync(UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.LeaveApprove, ct),
                "TRIP" => await _authorization.HasAsync(UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.TripApprove, ct),
                "OT" => await _authorization.HasAsync(UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.OTApprove, ct),
                _ => await _authorization.HasAsync(UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.DashboardView, ct)
                    || await _authorization.HasAsync(UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.LeaveApprove, ct)
                    || await _authorization.HasAsync(UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.OTApprove, ct)
                    || await _authorization.HasAsync(UserInfo, FVN_REGISTER.Core.Constants.SecurityFunctionCodes.TripApprove, ct)
            };
            if (!canView) return Forbid();

            if (!UserInfo.DeptCode.HasValue)
                return Ok(ApiResponse<IReadOnlyList<DepartmentDashboardDetailDto>>.Ok(
                    Array.Empty<DepartmentDashboardDetailDto>()));

            var requestModule = normalizedKind switch
            {
                "LEAVE" => RequestModule.Leave,
                "TRIP" => RequestModule.Trip,
                "OT" => RequestModule.Overtime,
                _ => RequestModule.Leave
            };
            var allowedDepartments = await _db.ApprovalPolicies.AsNoTracking()
                .Where(p => p.IsActive == true
                    && p.DeptCode == UserInfo.DeptCode.Value
                    && p.ApprovalPositionCode == UserInfo.PositionCode
                    && p.Level == UserInfo.LevelApprove
                    && (normalizedKind == "ATTENDANCE" ||
                        p.RequestType == requestModule))
                .Select(p => p.DeptCode).Distinct().ToListAsync(ct);

            if (allowedDepartments.Count == 0)
                return Ok(ApiResponse<IReadOnlyList<DepartmentDashboardDetailDto>>.Ok(
                    Array.Empty<DepartmentDashboardDetailDto>()));

            var day = (workDate ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
            var nextDay = day.AddDays(1);
            var output = new List<DepartmentDashboardDetailDto>();

            if (normalizedKind == "ATTENDANCE")
            {
                var attendance = await _db.HrmAttendanceCalculated.AsNoTracking()
                    .Where(a => a.WorkDate >= day && a.WorkDate < nextDay
                        && a.DeptCode.HasValue && allowedDepartments.Contains(a.DeptCode.Value))
                    .OrderBy(a => a.DeptCode).ThenBy(a => a.EmployeeCode)
                    .Select(a => new { a.WorkDate, a.EmployeeCode, a.FullName, a.DeptCode,
                        a.CheckInTime, a.CheckOutTime, a.AttendanceDisplayValue })
                    .ToListAsync(ct);
                var deptNames = await _db.Departments.AsNoTracking()
                    .Where(d => allowedDepartments.Contains(d.DeptCode))
                    .ToDictionaryAsync(d => d.DeptCode, d => d.DeptName, ct);
                output.AddRange(attendance.Select(a => new DepartmentDashboardDetailDto
                {
                    Kind = normalizedKind, WorkDate = a.WorkDate,
                    EmployeeCode = a.EmployeeCode ?? string.Empty, EmployeeName = a.FullName ?? string.Empty,
                    DepartmentName = a.DeptCode.HasValue && deptNames.TryGetValue(a.DeptCode.Value, out var name) ? name : string.Empty,
                    Detail = a.AttendanceDisplayValue ?? string.Empty,
                    Status = a.CheckInTime.HasValue ? (a.CheckOutTime.HasValue ? "CHECKED_OUT" : "CHECKED_IN") : "NO_RECORDED_PUNCH",
                    StartTime = a.CheckInTime, EndTime = a.CheckOutTime
                }));
            }
            else if (normalizedKind == "LEAVE")
            {
                var rows = await (
                    from l in _db.LeaveDays.AsNoTracking()
                    join e in _db.Employees.AsNoTracking() on l.EmployeeCode equals e.EmployeeCode
                    join d in _db.Departments.AsNoTracking() on e.DeptCode equals d.DeptCode
                    where l.IsActive == true && e.IsActive == true && l.RequestStatus == ApprovalStatus.Approved
                        && allowedDepartments.Contains(e.DeptCode)
                        && l.StartTime < nextDay && l.EndTime >= day
                    orderby e.DeptCode, e.EmployeeCode
                    select new { l.StartTime, l.EndTime, l.LeaveTypeCode, l.LeaveReason,
                        e.EmployeeCode, e.EmployeeName, e.DeptCode, d.DeptName })
                    .ToListAsync(ct);
                output.AddRange(rows.Select(x => new DepartmentDashboardDetailDto
                {
                    Kind = normalizedKind, WorkDate = day, EmployeeCode = x.EmployeeCode,
                    EmployeeName = x.EmployeeName, DepartmentName = x.DeptName ?? string.Empty,
                    Detail = string.Join(" — ", new[] { x.LeaveTypeCode, x.LeaveReason }.Where(v => !string.IsNullOrWhiteSpace(v))),
                    Status = ApprovalStatus.Approved.ToString(), StartTime = x.StartTime, EndTime = x.EndTime
                }));
            }
            else if (normalizedKind == "OT")
            {
                var rows = await (
                    from r in _db.OvertimeRequests.AsNoTracking()
                    join p in _db.OvertimeEmployees.AsNoTracking() on r.Id equals p.OTRequestId
                    join e in _db.Employees.AsNoTracking() on p.EmployeeCode equals e.EmployeeCode
                    join d in _db.Departments.AsNoTracking() on e.DeptCode equals d.DeptCode
                    where r.IsActive == true && e.IsActive == true && r.RequestStatus == ApprovalStatus.Approved
                        && r.OTDate >= day && r.OTDate < nextDay
                        && allowedDepartments.Contains(e.DeptCode)
                    orderby e.DeptCode, e.EmployeeCode
                    select new { r.OTDate, p.EmployeeCode, p.EmployeeName, p.StartTime, p.EndTime, p.OTHours,
                        p.OTReasonDetail, MasterEmployeeName = e.EmployeeName, e.DeptCode, d.DeptName })
                    .ToListAsync(ct);
                output.AddRange(rows.Select(x => new DepartmentDashboardDetailDto
                {
                    Kind = normalizedKind, WorkDate = x.OTDate, EmployeeCode = x.EmployeeCode,
                    EmployeeName = x.EmployeeName ?? x.MasterEmployeeName ?? string.Empty,
                    DepartmentName = x.DeptName ?? string.Empty, Detail = x.OTReasonDetail ?? string.Empty,
                    Status = ApprovalStatus.Approved.ToString(), StartTime = x.StartTime, EndTime = x.EndTime, Hours = x.OTHours
                }));
            }
            else if (normalizedKind == "TRIP")
            {
                var rows = await (
                    from r in _db.TripRequests.AsNoTracking()
                    join e in _db.Employees.AsNoTracking() on r.EmployeeCode equals e.EmployeeCode
                    join d in _db.Departments.AsNoTracking() on e.DeptCode equals d.DeptCode
                    where r.IsActive == true && e.IsActive == true && r.RequestStatus == ApprovalStatus.Approved
                        && r.StartDate < nextDay && r.EndDate >= day
                        && allowedDepartments.Contains(e.DeptCode)
                    orderby e.DeptCode, e.EmployeeCode
                    select new { r.StartDate, r.EndDate, r.Destination, r.Purpose,
                        e.EmployeeCode, e.EmployeeName, e.DeptCode, d.DeptName })
                    .ToListAsync(ct);
                output.AddRange(rows.Select(x => new DepartmentDashboardDetailDto
                {
                    Kind = normalizedKind, WorkDate = day, EmployeeCode = x.EmployeeCode,
                    EmployeeName = x.EmployeeName, DepartmentName = x.DeptName ?? string.Empty,
                    Detail = string.Join(" — ", new[] { x.Destination, x.Purpose }.Where(v => !string.IsNullOrWhiteSpace(v))),
                    Status = ApprovalStatus.Approved.ToString(), StartTime = x.StartDate, EndTime = x.EndDate
                }));
            }

            await LogActionAsync($"Xem chi tiết Dashboard {normalizedKind}");
            return Ok(ApiResponse<IReadOnlyList<DepartmentDashboardDetailDto>>.Ok(output));
        }

    }
}
