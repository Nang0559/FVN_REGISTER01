using System.Globalization;
using System.Data;
using Microsoft.Data.SqlClient;
using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Models.Calendar;
using FVN_REGISTER.Application.Policies;
using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Core.Entities.WorkCalendar;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Calendar;

/// <summary>
/// Reads the official HRM attendance calculation result directly.
/// Attendance is not an execution-reconciliation projection: normal
/// attendance rows must be visible in Calendar even when no mismatch exists.
/// </summary>
public sealed class AttendanceCalendarModuleProvider : ICalendarModuleProvider
{
    private readonly FVNWEBAPPContext _db;

    public AttendanceCalendarModuleProvider(FVNWEBAPPContext db)
    {
        _db = db;
    }

    public string ModuleCode => "ATTENDANCE";

    public async Task<IReadOnlyList<CalendarItemDto>> GetItemsAsync(
        CalendarContext context,
        CancellationToken cancellationToken = default)
    {
        // Both attendance stores define EmployeeCode as nvarchar(50). Use an explicitly
        // typed parameter so SQL Server can seek IX_*_EmployeeCode_WorkDate.
        // The current rows are read once; archived rows are suppressed by a correlated
        // anti-semi join. Avoid a multiply-referenced CTE and SELECT * across UNION ALL.
        var employeeCode = new SqlParameter("@code", SqlDbType.NVarChar, 50)
        {
            Value = (object?)context.EmployeeCode ?? DBNull.Value
        };
        var from = new SqlParameter("@from", SqlDbType.Date)
        {
            Value = context.From.ToDateTime(TimeOnly.MinValue)
        };
        var to = new SqlParameter("@to", SqlDbType.Date)
        {
            Value = context.To.ToDateTime(TimeOnly.MinValue)
        };

        const string attendanceSql = """
            SELECT
                a.WorkDate, a.HrmEmployeeId, a.EmployeeCode, a.ShiftId, a.ShiftAbbr,
                a.AttendanceDisplayValue, a.CheckInTime, a.CheckOutTime,
                a.WorkMinutesDay, a.WorkMinutesNight, a.OTMinutesDay, a.OTMinutesNight,
                a.OTMinutesDayTC, a.OTMinutesNightTC,
                a.OTRecognizedMinutesDay, a.OTRecognizedMinutesNight,
                a.RequiredMinutes, a.LateMinutesDay, a.LateMinutesNight,
                a.EarlyLeaveMinutesDay, a.EarlyLeaveMinutesNight, a.LeaveTotal,
                a.HrmHoliday, a.HrmEmployeeHoliday, a.HrmBCLyDoNghi, a.HrmBCGhiChu,
                a.DayType, a.WorkSymbol, a.OtSymbol, a.SymbolRuleCode
            FROM dbo.F03HrmAttendanceCalculated AS a
            WHERE a.EmployeeCode = @code
              AND a.WorkDate >= @from
              AND a.WorkDate <= @to

            UNION ALL

            SELECT
                h.WorkDate, h.HrmEmployeeId, h.EmployeeCode, h.ShiftId, h.ShiftAbbr,
                h.AttendanceDisplayValue, h.CheckInTime, h.CheckOutTime,
                h.WorkMinutesDay, h.WorkMinutesNight, h.OTMinutesDay, h.OTMinutesNight,
                h.OTMinutesDayTC, h.OTMinutesNightTC,
                h.OTRecognizedMinutesDay, h.OTRecognizedMinutesNight,
                h.RequiredMinutes, h.LateMinutesDay, h.LateMinutesNight,
                h.EarlyLeaveMinutesDay, h.EarlyLeaveMinutesNight, h.LeaveTotal,
                h.HrmHoliday, h.HrmEmployeeHoliday, h.HrmBCLyDoNghi, h.HrmBCGhiChu,
                h.DayType, h.WorkSymbol, h.OtSymbol, h.SymbolRuleCode
            FROM dbo.F03HrmAttendanceHistory AS h
            WHERE h.EmployeeCode = @code
              AND h.WorkDate >= @from
              AND h.WorkDate <= @to
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.F03HrmAttendanceCalculated AS c
                  WHERE c.EmployeeCode = @code
                    AND c.WorkDate = h.WorkDate
              )
            ORDER BY WorkDate;
            """;

        var rows = await _db.Database
            .SqlQueryRaw<AttendanceCalendarRow>(attendanceSql, employeeCode, from, to)
            .ToListAsync(cancellationToken);

        var reconciliationByDate = await _db.ExecutionReconciliations
            .AsNoTracking()
            .Where(x => x.IsActive != false
                && x.ModuleCode == "ATTENDANCE"
                && x.EmployeeId == context.EmployeeId
                && x.WorkDate >= context.From
                && x.WorkDate <= context.To)
            .ToDictionaryAsync(x => x.WorkDate, cancellationToken);

        // Latest employee feedback (confirmation) per reconciliation: drives the "?" / "-" / "!" marker and label.
        var reconciliationIds = reconciliationByDate.Values.Select(x => x.Id).ToList();
        var confirmationByReconciliation = reconciliationIds.Count == 0
            ? new Dictionary<long, FeedbackConfirmation>()
            : (await _db.ExecutionConfirmations
                .AsNoTracking()
                .Where(x => x.IsActive != false && reconciliationIds.Contains(x.ReconciliationId))
                .Select(x => new FeedbackConfirmation(x.Id, x.ReconciliationId, x.Status, x.SubmittedAt, x.ReviewedAt))
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.ReconciliationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        return rows.Select(row =>
        {
            reconciliationByDate.TryGetValue(row.WorkDate, out var reconciliation);
            FeedbackConfirmation? confirmation = null;
            if (reconciliation is not null
                && confirmationByReconciliation.TryGetValue(reconciliation.Id, out var found))
                confirmation = found;
            return Map(row, reconciliation, confirmation);
        }).ToArray();
    }

    private static CalendarItemDto Map(
        AttendanceCalendarRow row,
        F03ExecutionReconciliation? reconciliation,
        FeedbackConfirmation? confirmation = null)
    {
        var isLeave = (row.LeaveTotal ?? 0m) > 0m;
        var isHoliday = row.HrmHoliday == true || row.HrmEmployeeHoliday == true;
        var hasCheckIn = row.CheckInTime.HasValue;
        var hasCheckOut = row.CheckOutTime.HasValue;
        var lateMinutes = row.LateMinutesDay + row.LateMinutesNight;
        var earlyMinutes = row.EarlyLeaveMinutesDay + row.EarlyLeaveMinutesNight;
        var requiredMinutes = Math.Max(row.RequiredMinutes, 0);
        var workedMinutes = Math.Max(0, row.WorkMinutesDay) + Math.Max(0, row.WorkMinutesNight);
        var actualOtMinutes = Math.Max(0, row.OTMinutesDay)
            + Math.Max(0, row.OTMinutesNight)
            + Math.Max(0, row.OTMinutesDayTC)
            + Math.Max(0, row.OTMinutesNightTC);
        var recognizedOtMinutes = Math.Max(0, row.OTRecognizedMinutesDay)
            + Math.Max(0, row.OTRecognizedMinutesNight);

        byte severity = 0;
        bool requiresAction = false;
        string status;

        if (isLeave)
        {
            status = "LEAVE";
        }
        else if (isHoliday)
        {
            status = "HOLIDAY";
        }
        else if (requiredMinutes > 0 && (!hasCheckIn || !hasCheckOut))
        {
            status = "MISSING";
            severity = 3;
            requiresAction = true;
        }
        else if (lateMinutes > 0 || earlyMinutes > 0)
        {
            status = "EXCEPTION";
            severity = 2;
            requiresAction = true;
        }
        else if (workedMinutes > 0 || hasCheckIn || hasCheckOut)
        {
            status = "PRESENT";
        }
        else
        {
            status = "NO_ATTENDANCE";
        }

        var summary = BuildSummary(
            status,
            row,
            lateMinutes,
            earlyMinutes,
            workedMinutes);

        ExecutionFeedbackStatus? feedback = null;

        if (reconciliation is not null)
        {
            status = reconciliation.ReconciliationStatus;
            severity = status switch
            {
                "Mismatch" => (byte)3,
                "AwaitingConfirmation" => (byte)2,
                _ => (byte)0
            };
            requiresAction = reconciliation.RequiresConfirmation
                && !string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase);
            summary = BuildReconciliationSummary(row, reconciliation, lateMinutes, earlyMinutes, workedMinutes);

            feedback = ExecutionFeedbackStatusPolicy.Resolve(
                reconciliation.ReconciliationStatus,
                confirmation?.Status,
                confirmation?.SubmittedAt,
                confirmation?.ReviewedAt);

            if (feedback is { } fb)
            {
                severity = fb.Severity;
                requiresAction = requiresAction && fb.RequiresEmployeeAction;
                summary = $"{fb.Label} · {summary}";
            }
        }

        return new CalendarItemDto
        {
            WorkDate = row.WorkDate,
            ModuleCode = "ATTENDANCE",
            SourceType = reconciliation?.SourceType ?? "ATTENDANCE",
            StatusCode = status,
            Marker = feedback is { } fbMarker ? fbMarker.Marker : reconciliation?.ReconciliationStatus switch
            {
                "Mismatch" => "?",
                "AwaitingConfirmation" => "?",
                "Resolved" => "OK",
                _ => string.IsNullOrWhiteSpace(row.AttendanceDisplayValue) ? null : row.AttendanceDisplayValue
            },
            Summary = summary,
            Severity = severity,
            RequiresAction = requiresAction,
            InteractionType = requiresAction && reconciliation?.ActionId != null ? "CONFIRMATION" : reconciliation != null ? "DETAIL" : "INFO",
            ActionId = reconciliation?.ActionId,
            DetailRoute = reconciliation is null ? null : $"/execution?reconciliationId={reconciliation.Id}",
            SourceId = reconciliation?.Id.ToString() ?? $"{row.HrmEmployeeId}:{row.WorkDate:yyyyMMdd}",
            ShiftId = row.ShiftId,
            ShiftAbbr = row.ShiftAbbr,
            DayType = row.DayType,
            WorkSymbol = row.WorkSymbol,
            OtMarker = row.OtSymbol,
            SymbolRuleCode = row.SymbolRuleCode,
            CheckIn = row.CheckInTime,
            CheckOut = row.CheckOutTime,
            WorkMinutes = workedMinutes,
            RequiredMinutes = requiredMinutes,
            ActualOtMinutes = actualOtMinutes,
            RecognizedOtMinutes = recognizedOtMinutes,
            ActualHours = workedMinutes / 60m,
            RequiredHours = requiredMinutes > 0 ? requiredMinutes / 60m : null,
            HasActual = hasCheckIn || hasCheckOut || workedMinutes > 0,
            HasActualOt = actualOtMinutes > 0 || recognizedOtMinutes > 0,
            FeedbackTone = feedback?.Tone,
            FeedbackMarker = feedback?.Marker,
            FeedbackLabel = feedback?.Label,
            IsNumericVariance = decimal.TryParse(
                row.AttendanceDisplayValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out _)
                && requiredMinutes > 0
                && hasCheckIn
                && hasCheckOut
        };
    }

    private static string BuildReconciliationSummary(
        AttendanceCalendarRow row,
        F03ExecutionReconciliation reconciliation,
        int lateMinutes,
        int earlyMinutes,
        int workedMinutes)
    {
        if (string.Equals(reconciliation.ReconciliationStatus, "Mismatch", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(reconciliation.PlannedState, "NONE", StringComparison.OrdinalIgnoreCase)
                && (row.CheckInTime.HasValue || row.CheckOutTime.HasValue))
                return $"Có chấm công ngoài kế hoạch{BuildTimeSuffix(row, workedMinutes)}";

            if (row.RequiredMinutes > 0 && (!row.CheckInTime.HasValue || !row.CheckOutTime.HasValue))
                return BuildSummary("MISSING", row, lateMinutes, earlyMinutes, workedMinutes);

            if (lateMinutes > 0 || earlyMinutes > 0)
                return BuildSummary("EXCEPTION", row, lateMinutes, earlyMinutes, workedMinutes);
        }

        if (string.Equals(reconciliation.ReconciliationStatus, "AwaitingConfirmation", StringComparison.OrdinalIgnoreCase))
            return $"Đang chờ xác nhận{BuildTimeSuffix(row, workedMinutes)}";

        if (string.Equals(reconciliation.ReconciliationStatus, "Resolved", StringComparison.OrdinalIgnoreCase))
            return $"Đã giải quyết{BuildTimeSuffix(row, workedMinutes)}";

        return BuildSummary("PRESENT", row, lateMinutes, earlyMinutes, workedMinutes);
    }

    private static string BuildTimeSuffix(AttendanceCalendarRow row, int workedMinutes)
    {
        if (row.CheckInTime.HasValue && row.CheckOutTime.HasValue)
        {
            var range = $" {row.CheckInTime.Value:HH:mm} - {row.CheckOutTime.Value:HH:mm}";
            return workedMinutes > 0 ? $"{range} ({workedMinutes / 60d:0.##} giờ)" : range;
        }

        if (row.CheckInTime.HasValue) return $" Check-in {row.CheckInTime.Value:HH:mm}";
        if (row.CheckOutTime.HasValue) return $" Check-out {row.CheckOutTime.Value:HH:mm}";
        return string.Empty;
    }

    private static string BuildSummary(
        string status,
        AttendanceCalendarRow row,
        int lateMinutes,
        int earlyMinutes,
        int workedMinutes)
    {
        if (status == "LEAVE")
        {
            var reason = row.HrmBCLyDoNghi ?? row.HrmBCGhiChu;
            return string.IsNullOrWhiteSpace(reason)
                ? "Nghỉ phép"
                : $"Nghỉ: {reason}";
        }

        if (status == "HOLIDAY")
            return "Ngày nghỉ";

        if (status == "MISSING")
        {
            if (!row.CheckInTime.HasValue && !row.CheckOutTime.HasValue)
                return "Thiếu Check-in và Check-out";

            return !row.CheckInTime.HasValue
                ? "Thiếu Check-in"
                : "Thiếu Check-out";
        }

        if (status == "EXCEPTION")
        {
            var parts = new List<string>(2);
            if (lateMinutes > 0) parts.Add($"Đi muộn {lateMinutes} phút");
            if (earlyMinutes > 0) parts.Add($"Về sớm {earlyMinutes} phút");
            return string.Join(" • ", parts);
        }

        if (status == "PRESENT")
        {
            if (row.CheckInTime.HasValue && row.CheckOutTime.HasValue)
            {
                var range = $"{row.CheckInTime.Value:HH:mm} - {row.CheckOutTime.Value:HH:mm}";
                return workedMinutes > 0
                    ? $"{range} ({workedMinutes / 60d:0.##} giờ)"
                    : range;
            }

            if (row.CheckInTime.HasValue)
                return $"Check-in {row.CheckInTime.Value:HH:mm}";

            if (row.CheckOutTime.HasValue)
                return $"Check-out {row.CheckOutTime.Value:HH:mm}";

            return "Có chấm công";
        }

        return "Chưa có dữ liệu chấm công";
    }

    private sealed record FeedbackConfirmation(
        long Id,
        long ReconciliationId,
        string Status,
        DateTime? SubmittedAt,
        DateTime? ReviewedAt);

    private sealed class AttendanceCalendarRow
    {
        public DateOnly WorkDate { get; set; }
        public int HrmEmployeeId { get; set; }
        public string? EmployeeCode { get; set; }
        public int? ShiftId { get; set; }
        public string? ShiftAbbr { get; set; }
        public string? AttendanceDisplayValue { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public int WorkMinutesDay { get; set; }
        public int WorkMinutesNight { get; set; }
        public int OTMinutesDay { get; set; }
        public int OTMinutesNight { get; set; }
        public int OTMinutesDayTC { get; set; }
        public int OTMinutesNightTC { get; set; }
        public int OTRecognizedMinutesDay { get; set; }
        public int OTRecognizedMinutesNight { get; set; }
        public int RequiredMinutes { get; set; }
        public int LateMinutesDay { get; set; }
        public int LateMinutesNight { get; set; }
        public int EarlyLeaveMinutesDay { get; set; }
        public int EarlyLeaveMinutesNight { get; set; }
        public decimal? LeaveTotal { get; set; }
        public bool? HrmHoliday { get; set; }
        public bool? HrmEmployeeHoliday { get; set; }
        public string? HrmBCLyDoNghi { get; set; }
        public string? HrmBCGhiChu { get; set; }
        public string? DayType { get; set; }
        public string? WorkSymbol { get; set; }
        public string? OtSymbol { get; set; }
        public string? SymbolRuleCode { get; set; }
    }
}
