using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Interfaces.HrmSync;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Logging;
using FVN_REGISTER.Application.Models.Calendar;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.WorkCalendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.Calendar;

public sealed class SharedWorkCalendarService : ISharedWorkCalendarService
{
    private readonly FVNWEBAPPContext _db;
    private readonly ICalendarModuleRegistry _registry;
    private readonly IWorkCalendarService _workCalendar;
    private readonly ICalendarDayRuleEngine _ruleEngine;
    private readonly IAuthorizationService _authorization;
    private readonly IAttendanceBackfillCoordinator _attendanceBackfill;
    private readonly ILogger<SharedWorkCalendarService> _logger;

    public SharedWorkCalendarService(
        FVNWEBAPPContext db,
        ICalendarModuleRegistry registry,
        IWorkCalendarService workCalendar,
        ICalendarDayRuleEngine ruleEngine,
        IAuthorizationService authorization,
        IAttendanceBackfillCoordinator attendanceBackfill,
        ILogger<SharedWorkCalendarService> logger)
    {
        _db = db;
        _registry = registry;
        _workCalendar = workCalendar;
        _ruleEngine = ruleEngine;
        _authorization = authorization;
        _attendanceBackfill = attendanceBackfill;
        _logger = logger;
    }

    private async Task<CalendarMonthDto> GetMonthCoreAsync(
        string employeeCode,
        int userId,
        DateOnly from,
        DateOnly to,
        IReadOnlySet<string>? allowedModules = null,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
            throw new ArgumentException("Calendar period is invalid.", nameof(from));

        var actor = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new UserIdentityDto
            {
                UserId = x.Id,
                Permission = x.PermissionCode,
                EmployeeCode = x.EmployeeCode,
                FullName = x.FullName,
                DeptCode = x.DeptCode,
                PositionCode = x.Cvcode
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Không xác định được tài khoản hiện tại.");

        var normalizedEmployeeCode = employeeCode.Trim();
        if (normalizedEmployeeCode.Length == 0)
            throw new ArgumentException("Mã nhân viên là bắt buộc.", nameof(employeeCode));

        var employee = await _db.Employees
            .AsNoTracking()
            .Where(x => x.IsActive != false
                && x.EmployeeCode != null
                && x.EmployeeCode.Trim() == normalizedEmployeeCode)
            .Select(x => new
            {
                x.Id,
                x.EmployeeCode,
                x.DeptCode,
                x.PositionCode
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhân viên '{normalizedEmployeeCode}'.");

        var resolvedEmployeeCode = employee.EmployeeCode?.Trim() ?? normalizedEmployeeCode;

        if (!string.Equals(actor.EmployeeCode?.Trim(), resolvedEmployeeCode, StringComparison.OrdinalIgnoreCase)
            && !await _authorization.CanAccessAsync(
                actor,
                SecurityFunctionCodes.CalendarView,
                resolvedEmployeeCode,
                null,
                cancellationToken))
        {
            throw new UnauthorizedAccessException("Bạn không có Calendar.View hoặc ManagedScope tới nhân viên này.");
        }

        var policies = await _db.CalendarModulePolicies
            .AsNoTracking()
            .Where(x => x.IsActive != false && x.IsEnabled)
            .ToDictionaryAsync(x => x.ModuleCode, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Attendance is a current-state projection, not the source of calendar availability.
        // Historical months are lazy-backfilled per employee when requested, but the calendar never
        // waits long for it: the coordinator waits a few seconds at most, then the calendar renders the
        // data that exists now while the backfill finishes in the background. No calendar request
        // triggers a company-wide calculation.
        CalendarAlertItemDto? backfillAlert = null;
        if (allowedModules is null || allowedModules.Contains("ATTENDANCE"))
        {
            var coverage = await _attendanceBackfill.EnsureAsync(
                resolvedEmployeeCode,
                employee.DeptCode,
                from,
                to,
                cancellationToken);

            if (coverage.Status != AttendanceBackfillStatus.Ready)
            {
                backfillAlert = new CalendarAlertItemDto
                {
                    WorkDate = from,
                    ModuleCode = "ATTENDANCE",
                    Severity = coverage.Status == AttendanceBackfillStatus.Failed ? "Warning" : "Info",
                    Summary = coverage.Status == AttendanceBackfillStatus.Failed
                        ? coverage.Message ?? "Không thể chuẩn bị dữ liệu chấm công cho khoảng thời gian đã chọn."
                        : "Dữ liệu chấm công đang được cập nhật, vui lòng tải lại sau ít phút.",
                    RequiresAction = false
                };
            }
        }

        var context = new CalendarContext(
            EmployeeId: employee.Id,
            UserId: userId,
            From: from,
            To: to,
            AllowedModules: allowedModules,
            DeptCode: employee.DeptCode,
            PositionCode: employee.PositionCode,
            EmployeeCode: resolvedEmployeeCode);

        var workCalendarResult = await _workCalendar.GetAsync(
            resolvedEmployeeCode,
            employee.DeptCode,
            employee.PositionCode,
            from.ToDateTime(TimeOnly.MinValue),
            to.ToDateTime(TimeOnly.MinValue),
            cancellationToken);
        if (!workCalendarResult.IsSuccess || workCalendarResult.Data is null)
            throw new InvalidOperationException(workCalendarResult.Message ?? "Không thể tải lịch làm việc.");
        var workCalendar = workCalendarResult.Data;

        var results = new List<IReadOnlyList<CalendarItemDto>>();

        foreach (var provider in _registry.Providers
            .Where(x => policies.ContainsKey(x.ModuleCode)
                && (allowedModules is null || allowedModules.Contains(x.ModuleCode))))
        {
            try
            {
                results.Add(await provider.GetItemsAsync(context, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Calendar provider failed. ModuleCode={ModuleCode}, EmployeeCode={EmployeeCode}, EmployeeId={EmployeeId}, From={From}, To={To}",
                    provider.ModuleCode,
                    context.EmployeeCode,
                    context.EmployeeId,
                    context.From,
                    context.To);
            }
        }

        var items = results
            .SelectMany(x => x)
            .OrderBy(x => x.WorkDate)
            .ThenByDescending(x => x.RequiresAction)
            .ThenByDescending(x => x.Severity)
            .ThenBy(x => policies.TryGetValue(x.ModuleCode, out var policy) ? policy.Priority : 100)
            .ThenBy(x => x.ModuleCode)
            .ToArray();

        var itemsByDate = items
            .GroupBy(x => x.WorkDate)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<CalendarItemDto>)x.ToArray());

        var days = new List<CalendarDayDto>();
        var dayTypeColors = await LoadDayTypeColorsAsync(cancellationToken);

        // Phản hồi cấp trên gần nhất của từng ngày (để ô "?" hiện đúng: chờ xử lý / bị từ chối / đã đăng ký OT).
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MinValue);
        var feedbackRows = await _db.Set<F03SupervisorFeedback>()
            .AsNoTracking()
            .Where(x => x.EmployeeCode == normalizedEmployeeCode && x.WorkDate >= fromDt && x.WorkDate <= toDt)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var feedbackByDate = feedbackRows
            .GroupBy(x => DateOnly.FromDateTime(x.WorkDate.Date))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var baseDay in workCalendar.Days.OrderBy(x => x.Date))
        {
            feedbackByDate.TryGetValue(DateOnly.FromDateTime(baseDay.Date.Date), out var supervisorFeedback);
            var date = DateOnly.FromDateTime(baseDay.Date.Date);
            var dateItems = itemsByDate.TryGetValue(date, out var sourceItems)
                ? sourceItems
                : Array.Empty<CalendarItemDto>();

            var registrations = workCalendar.Events
                .Where(x => x.ModuleCode is "LEAVE" or "Overtime" or "TRIP"
                    && CalendarEventDateMatcher.CoversDate(x, baseDay.Date))
                .Where(x => allowedModules is null || allowedModules.Contains(x.ModuleCode))
                .OrderBy(x => x.ModuleCode)
                .ThenBy(x => x.Start)
                .Select(x =>
                {
                    var projection = dateItems
                        .Where(i => string.Equals(i.ModuleCode, x.ModuleCode, StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault(i =>
                            string.Equals(i.SourceId, x.SourceId, StringComparison.OrdinalIgnoreCase))
                        ?? GetSingleModuleProjection(dateItems, x.ModuleCode);

                    return new CalendarRegistrationDto
                    {
                        ModuleCode = x.ModuleCode,
                        RequestId = x.RequestId,
                        SubTypeCode = x.SubTypeCode,
                        Title = x.Title,
                        Status = x.Status,
                        Start = x.Start,
                        End = x.End,
                        IsHalfDay = x.IsHalfDay,
                        DayValue = x.DayValue <= 0 ? 1m : x.DayValue,
                        ActionId = projection?.ActionId ?? x.ActionId,
                        DetailRoute = projection?.DetailRoute ?? x.DetailRoute,
                        ApprovalLevel = x.ApprovalLevel,
                        ApprovalLevelName = x.ApprovalLevelName,
                        CurrentApproverName = x.CurrentApproverName,
                        ApprovalStatus = x.ApprovalStatus,
                        IsApproved = x.IsApproved
                    };
                })
                .ToArray();

            var attendanceItem = dateItems.FirstOrDefault(x =>
                string.Equals(x.ModuleCode, "ATTENDANCE", StringComparison.OrdinalIgnoreCase));

            CalendarAttendanceInfoDto? attendance = null;
            CalendarShiftInfoDto? shift = null;

            if (attendanceItem is not null)
            {
                shift = new CalendarShiftInfoDto
                {
                    ShiftId = attendanceItem.ShiftId,
                    ShiftAbbr = attendanceItem.ShiftAbbr,
                    RequiredMinutes = attendanceItem.RequiredMinutes
                };

                attendance = new CalendarAttendanceInfoDto
                {
                    CheckIn = attendanceItem.CheckIn,
                    CheckOut = attendanceItem.CheckOut,
                    WorkMinutes = attendanceItem.WorkMinutes,
                    RequiredMinutes = attendanceItem.RequiredMinutes,
                    ActualHours = attendanceItem.ActualHours,
                    RequiredHours = attendanceItem.RequiredHours,
                    ActualOtMinutes = attendanceItem.ActualOtMinutes,
                    RecognizedOtMinutes = attendanceItem.RecognizedOtMinutes,
                    HasActual = attendanceItem.HasActual,
                    HasActualOt = attendanceItem.HasActualOt,
                    IsNumericVariance = attendanceItem.IsNumericVariance,
                    DisplayValue = attendanceItem.Marker,
                    RuleWorkSymbol = attendanceItem.WorkSymbol,
                    RuleOtSymbol = attendanceItem.OtMarker,
                    RuleDayType = attendanceItem.DayType,
                    RuleCode = attendanceItem.SymbolRuleCode,
                    SourceId = attendanceItem.SourceId,
                    ActionId = attendanceItem.ActionId,
                    DetailRoute = attendanceItem.DetailRoute,
                    FeedbackTone = attendanceItem.FeedbackTone,
                    FeedbackMarker = attendanceItem.FeedbackMarker,
                    FeedbackLabel = attendanceItem.FeedbackLabel
                };
            }

            var provisional = new CalendarDayDto
            {
                Date = date,
                WorkYear = baseDay.WorkYear,
                IsToday = !baseDay.IsFuture && !baseDay.IsPast,
                IsFuture = baseDay.IsFuture,
                IsPast = baseDay.IsPast,
                IsWeekend = baseDay.IsWeekend,
                IsWorkingDay = baseDay.IsWorkingDay,
                Holiday = string.IsNullOrWhiteSpace(baseDay.HolidayName)
                    ? null
                    : new CalendarHolidayInfoDto
                    {
                        Code = baseDay.HolidayCode,
                        Name = baseDay.HolidayName,
                        Symbol = baseDay.HolidaySymbol,
                        TypeName = baseDay.HolidayTypeName,
                        Color = baseDay.HolidayColor
                    },
                // Màu ô: ngày nghỉ HRM / Chủ nhật (WorkCalendarService) > màu loại ngày ghi trên dòng chấm công
                // (vd NT = ngày nghỉ riêng của nhân viên, không có bản ghi ngày nghỉ công ty).
                DayColor = baseDay.DayColor ?? ResolveAttendanceDayColor(date, attendance, dayTypeColors),
                Shift = shift,
                Attendance = attendance,
                Registrations = registrations,
                CanRegisterLeave = baseDay.CanRegisterLeave
                    && !registrations.Any(x => x.ModuleCode == "LEAVE"),
                CanRegisterOT = baseDay.CanRegisterOT
                    && !registrations.Any(x => x.ModuleCode == "Overtime"),
                CanRegisterTrip = baseDay.CanRegisterTrip
                    && !registrations.Any(x => x.ModuleCode == "TRIP"),
                MustReportOtToSupervisor = baseDay.MustReportOtToSupervisor,
                SupervisorFeedbackStatus = supervisorFeedback is null ? 0 : (int)supervisorFeedback.Status,
                SupervisorFeedbackNote = supervisorFeedback?.Note,
                AvailabilityNote = baseDay.AvailabilityNote
            };

            var issues = ApplyExecutionFeedback(
                _ruleEngine.Evaluate(new CalendarDayRuleContext(provisional, dateItems, baseDay.IsOtRegistrar)),
                dateItems);

            days.Add(new CalendarDayDto
            {
                Date = provisional.Date,
                WorkYear = provisional.WorkYear,
                IsToday = provisional.IsToday,
                IsFuture = provisional.IsFuture,
                IsPast = provisional.IsPast,
                IsWeekend = provisional.IsWeekend,
                IsWorkingDay = provisional.IsWorkingDay,
                Holiday = provisional.Holiday,
                DayColor = provisional.DayColor,
                Shift = provisional.Shift,
                Attendance = provisional.Attendance,
                Registrations = provisional.Registrations,
                Issues = issues,
                CanRegisterLeave = provisional.CanRegisterLeave,
                CanRegisterOT = provisional.CanRegisterOT,
                CanRegisterTrip = provisional.CanRegisterTrip,
                MustReportOtToSupervisor = provisional.MustReportOtToSupervisor,
                SupervisorFeedbackStatus = provisional.SupervisorFeedbackStatus,
                SupervisorFeedbackNote = provisional.SupervisorFeedbackNote,
                AvailabilityNote = BuildAvailabilityNote(provisional)
            });
        }

        var opportunities = days
            .SelectMany(day => BuildRegistrationOpportunities(day, allowedModules))
            .ToArray();

        var alerts = days
            .SelectMany(day => day.Issues.Select(issue => new CalendarAlertItemDto
            {
                WorkDate = day.Date,
                ModuleCode = string.IsNullOrWhiteSpace(issue.ModuleCode) ? issue.Code : issue.ModuleCode,
                Severity = issue.Severity >= 3
                    ? "Critical"
                    : issue.Severity == 2
                        ? "Warning"
                        : "Attention",
                Summary = string.IsNullOrWhiteSpace(issue.Summary)
                    ? issue.Title
                    : issue.Summary!,
                RequiresAction = issue.Actions.Count > 0,
                ActionId = issue.ActionId,
                DetailRoute = issue.DetailRoute
                    ?? issue.Actions.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.DetailRoute))?.DetailRoute,
                SourceId = issue.SourceId ?? day.Date.ToString("yyyy-MM-dd")
            }))
            .OrderByDescending(x => x.WorkDate)
            .ThenByDescending(x => x.Severity)
            .ToArray();

        if (backfillAlert is not null)
            alerts = [backfillAlert, .. alerts];

        return new CalendarMonthDto
        {
            From = from,
            To = to,
            Items = items,
            Days = days,
            Alerts = alerts,
            RegistrationOpportunities = opportunities
        };
    }

    public async Task<ServiceResult<CalendarMonthDto>> GetMonthAsync(
        string employeeCode,
        int userId,
        DateOnly from,
        DateOnly to,
        IReadOnlySet<string>? allowedModules = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return ServiceResult<CalendarMonthDto>.Ok(
                await GetMonthCoreAsync(employeeCode, userId, from, to, allowedModules, cancellationToken));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException or KeyNotFoundException or InvalidOperationException)
        {
            return ServiceResult<CalendarMonthDto>.Fail(ex.Message);
        }
    }

    /// <summary>Màu HRM theo DayType: NORMAL không tô màu; SUNDAY=Navy; COMPANY_HOLIDAY (T2-T7)=LightYellow; NATIONAL_HOLIDAY giữ màu HRM (ví dụ HotPink); OTHER theo cấu hình.</summary>
    private async Task<Dictionary<string, string>> LoadDayTypeColorsAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var rows = await _db.Database
                .SqlQueryRaw<DayTypeColorRow>("SELECT DayType, LNLMauSac AS Color FROM dbo.F03HrmHolidayTypes WHERE ISNULL(DLocked,0)=0 AND LNLMauSac IS NOT NULL AND LNLMauSac<>'' ORDER BY LNLMa")
                .ToListAsync(cancellationToken);
            foreach (var row in rows)
            {
                var color = CalendarColor.Normalize(row.Color);
                if (color is not null && !string.IsNullOrWhiteSpace(row.DayType) && !result.ContainsKey(row.DayType))
                    result[row.DayType] = color;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không đọc được màu loại ngày HRM (F03HrmHolidayTypes); ô lịch giữ nền trắng.");
        }
        return result;
    }

    private static string? ResolveAttendanceDayColor(
        DateOnly date,
        CalendarAttendanceInfoDto? attendance,
        IReadOnlyDictionary<string, string> dayTypeColors)
    {
        var dayType = attendance?.DayType;
        if (string.IsNullOrWhiteSpace(dayType))
            return null;

        dayTypeColors.TryGetValue(dayType, out var configuredColor);
        dayTypeColors.TryGetValue("SUNDAY", out var sundayColor);

        // Reuse the same policy as company-holiday calendar data.
        // In particular, NORMAL must never inherit HRM catalog 0001's display color.
        return CalendarHolidayColorPolicy.Resolve(
            date.ToDateTime(TimeOnly.MinValue),
            dayType,
            configuredColor,
            sundayColor);
    }

    private sealed class DayTypeColorRow
    {
        public string? DayType { get; set; }
        public string? Color { get; set; }
    }

    private static CalendarItemDto? GetSingleModuleProjection(
        IReadOnlyList<CalendarItemDto> items,
        string moduleCode)
    {
        var matches = items
            .Where(i => string.Equals(i.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matches.Length == 1 ? matches[0] : null;
    }

    public async Task<ServiceResult<IReadOnlyList<CalendarAlertItemDto>>> GetAlertsAsync(
        string employeeCode,
        int userId,
        DateOnly from,
        DateOnly to,
        IReadOnlySet<string>? allowedModules = null,
        CancellationToken cancellationToken = default)
    {
        var result = await GetMonthAsync(
            employeeCode,
            userId,
            from,
            to,
            allowedModules,
            cancellationToken);

        return result.IsSuccess
            ? ServiceResult<IReadOnlyList<CalendarAlertItemDto>>.Ok(result.Data?.Alerts ?? Array.Empty<CalendarAlertItemDto>())
            : ServiceResult<IReadOnlyList<CalendarAlertItemDto>>.Fail(result.Message ?? "Không thể tải cảnh báo lịch.");
    }

    /// <summary>
    /// Phản hồi nhân viên/HR (OT, Trip, Leave) nằm ở CalendarItemDto.Feedback*; đưa nó lên issue cùng module
    /// để dấu "?" đổi thành Pending / HR request / Complete / Rejected thay vì luôn đỏ.
    /// </summary>
    private static IReadOnlyList<CalendarIssueDto> ApplyExecutionFeedback(
        IReadOnlyList<CalendarIssueDto> issues,
        IReadOnlyList<CalendarItemDto> dateItems)
    {
        if (issues.Count == 0)
            return issues;

        var feedbackByModule = dateItems
            .Where(x => !string.IsNullOrWhiteSpace(x.FeedbackTone)
                && !string.Equals(x.ModuleCode, "ATTENDANCE", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.ModuleCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Severity).First(), StringComparer.OrdinalIgnoreCase);

        if (feedbackByModule.Count == 0)
            return issues;

        return issues.Select(issue =>
        {
            if (!feedbackByModule.TryGetValue(issue.ModuleCode ?? string.Empty, out var item))
                return issue;

            var complete = string.Equals(item.FeedbackTone, "complete", StringComparison.OrdinalIgnoreCase);
            return new CalendarIssueDto
            {
                Code = issue.Code,
                ModuleCode = issue.ModuleCode,
                Severity = item.Severity,
                Marker = item.FeedbackMarker ?? issue.Marker,
                Title = issue.Title,
                Summary = string.IsNullOrWhiteSpace(item.FeedbackLabel)
                    ? issue.Summary
                    : $"{item.FeedbackLabel} · {issue.Summary}",
                SourceId = issue.SourceId,
                ActionId = issue.ActionId,
                DetailRoute = issue.DetailRoute,
                RequestId = issue.RequestId,
                // Complete: đã xong, không mời phản hồi lại. HR processed: dẫn tới trang kết quả để xác nhận/khiếu nại.
                Actions = ResolveFeedbackActions(issue, item, complete),
                FeedbackTone = item.FeedbackTone,
                FeedbackMarker = item.FeedbackMarker,
                FeedbackLabel = item.FeedbackLabel
            };
        }).ToArray();
    }

    private static IReadOnlyList<CalendarActionOptionDto> ResolveFeedbackActions(
        CalendarIssueDto issue,
        CalendarItemDto item,
        bool complete)
    {
        if (complete)
            return Array.Empty<CalendarActionOptionDto>();

        var route = string.IsNullOrWhiteSpace(item.DetailRoute) ? issue.DetailRoute : item.DetailRoute;
        if (string.Equals(item.FeedbackTone, "hr-resolved", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(route))
        {
            return new[]
            {
                new CalendarActionOptionDto
                {
                    Code = "OPEN_EXECUTION_RESULT",
                    Title = "Xem kết quả HR và xác nhận/khiếu nại",
                    Description = "HR đã xử lý phản hồi của bạn. Mở kết quả để xác nhận hoặc khiếu nại.",
                    Kind = "NAVIGATION",
                    DetailRoute = route
                }
            };
        }

        return issue.Actions;
    }

    private static string? BuildAvailabilityNote(CalendarDayDto day)
    {
        if (day.Holiday is not null)
            return "Ngày nghỉ công ty — chỉ đăng ký OT.";

        if (day.IsToday)
            return "Hôm nay không mở đăng ký nghỉ/OT/công tác; có thể đăng ký đi muộn/về sớm theo chính sách.";

        if (day.IsPast)
            return "Ngày đã qua — chỉ xem và xử lý chênh lệch.";

        if (day.WorkYear is null)
            return "Chưa cấu hình năm làm việc.";

        if (!day.IsWorkingDay)
            return "Không phải ngày làm việc theo lịch công ty.";

        if (!day.CanRegisterLeave && !day.CanRegisterOT && !day.CanRegisterTrip && day.Registrations.Count > 0)
            return "Đã có đăng ký trong ngày.";

        return day.AvailabilityNote;
    }

    private static IEnumerable<CalendarRegistrationOpportunityDto> BuildRegistrationOpportunities(
        CalendarDayDto day,
        IReadOnlySet<string>? allowedModules)
    {
        if (!day.IsFuture && !day.IsToday)
            yield break;

        if (day.IsFuture && day.CanRegisterLeave && IsAllowed("LEAVE", allowedModules))
        {
            yield return new CalendarRegistrationOpportunityDto
            {
                WorkDate = day.Date,
                ModuleCode = "LEAVE",
                Title = "Đăng ký nghỉ",
                Route = $"/leave/create?date={day.Date:yyyy-MM-dd}",
                IsEnabled = true
            };
        }

        if (day.IsFuture && day.CanRegisterOT && IsAllowed("Overtime", allowedModules))
        {
            yield return new CalendarRegistrationOpportunityDto
            {
                WorkDate = day.Date,
                ModuleCode = "Overtime",
                Title = "Đăng ký OT",
                Route = $"/ot/create?date={day.Date:yyyy-MM-dd}",
                IsEnabled = true
            };
        }

        if (day.IsFuture && day.CanRegisterTrip && IsAllowed("TRIP", allowedModules))
        {
            yield return new CalendarRegistrationOpportunityDto
            {
                WorkDate = day.Date,
                ModuleCode = "TRIP",
                Title = "Đăng ký công tác",
                Route = $"/trip/create?date={day.Date:yyyy-MM-dd}",
                IsEnabled = true
            };
        }

        // LateEarly is allowed on the current day as well as future work dates.
        // It is an explanatory registration overlay and never changes HRM attendance symbols.
        if ((day.IsToday || day.IsFuture) && day.IsWorkingDay && day.Holiday is null
            && IsAllowed("LATE_EARLY", allowedModules))
        {
            yield return new CalendarRegistrationOpportunityDto
            {
                WorkDate = day.Date,
                ModuleCode = "LATE_EARLY",
                Title = "Đăng ký đi muộn / về sớm",
                Route = $"/late-early/create?date={day.Date:yyyy-MM-dd}",
                IsEnabled = true
            };
        }
    }

    private static bool IsAllowed(string moduleCode, IReadOnlySet<string>? allowedModules) =>
        allowedModules is null || allowedModules.Contains(moduleCode);
}