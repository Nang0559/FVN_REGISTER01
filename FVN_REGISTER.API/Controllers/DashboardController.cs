using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Orchestrators;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Contract.Dtos.Dashboard;
using FVN_REGISTER.Core.Entities.Approvers;
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
            var policyQuery = _db.ApprovalPolicies.AsNoTracking()
                .Where(p => p.IsActive == true
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

    }
}
