using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Models.Calendar;
using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Core.Entities.HRM;
using FVN_REGISTER.Core.Entities.LateEarly;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Calendar;

/// <summary>
/// Calendar overlay for approved/pending late-arrival and early-leave registrations.
/// Actual punches are read from HRM calculated/current-history data only; this provider
/// never writes attendance symbols, DayType or calculation rows.
/// </summary>
public sealed class LateEarlyCalendarModuleProvider : ICalendarModuleProvider
{
    private readonly FVNWEBAPPContext _db;

    public LateEarlyCalendarModuleProvider(FVNWEBAPPContext db) => _db = db;

    public string ModuleCode => "LATE_EARLY";

    public async Task<IReadOnlyList<CalendarItemDto>> GetItemsAsync(
        CalendarContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.EmployeeCode))
            return Array.Empty<CalendarItemDto>();

        var requests = await _db.LateEarlyRequests.AsNoTracking()
            .Where(x => x.IsActive == true
                && x.EmployeeCode == context.EmployeeCode
                && x.WorkDate >= context.From.ToDateTime(TimeOnly.MinValue)
                && x.WorkDate <= context.To.ToDateTime(TimeOnly.MinValue)
                && x.RequestStatus != ApprovalStatus.Cancelled)
            .OrderBy(x => x.WorkDate)
            .ThenBy(x => x.LateEarlyType)
            .ToListAsync(cancellationToken);

        if (requests.Count == 0)
            return Array.Empty<CalendarItemDto>();

        var from = context.From.ToDateTime(TimeOnly.MinValue);
        var to = context.To.ToDateTime(TimeOnly.MinValue);
        var punches = await LoadPunchesAsync(context.EmployeeCode, from, to, cancellationToken);
        var punchByDate = punches
            .GroupBy(x => DateOnly.FromDateTime(x.WorkDate))
            .ToDictionary(x => x.Key, x => x.OrderByDescending(p => p.WorkDate).First());

        return requests.Select(request =>
        {
            var date = DateOnly.FromDateTime(request.WorkDate);
            punchByDate.TryGetValue(date, out var punch);
            var isLate = request.LateEarlyType == LateEarlyType.LateArrival;
            var kind = isLate ? "Đi muộn" : "Về sớm";
            var planned = request.PlannedTime.ToString(@"hh\:mm");
            var actual = punch == null
                ? null
                : isLate ? punch.CheckInTime : punch.CheckOutTime;

            var status = request.RequestStatus switch
            {
                ApprovalStatus.Draft => "DRAFT",
                ApprovalStatus.Pending => "PENDING",
                ApprovalStatus.InProgress => "IN_PROGRESS",
                ApprovalStatus.Approved => "APPROVED",
                ApprovalStatus.Rejected => "REJECTED",
                ApprovalStatus.NeedsRevision => "NEEDS_REVISION",
                _ => request.RequestStatus.ToString().ToUpperInvariant()
            };

            var summary = request.RequestStatus == ApprovalStatus.Approved
                ? actual.HasValue
                    ? $"{kind}: dự kiến {planned}, HRM ghi nhận {actual.Value:HH:mm}"
                    : $"{kind}: đã duyệt, đang chờ dữ liệu chấm công HRM"
                : $"{kind}: dự kiến {planned} · {request.RequestStatus.ToDisplayName()}";

            if (request.IsRetroactive)
                summary += " · đăng ký bổ sung";

            return new CalendarItemDto
            {
                WorkDate = date,
                ModuleCode = ModuleCode,
                StatusCode = status,
                Marker = request.RequestStatus == ApprovalStatus.Approved ? (actual.HasValue ? "LE" : "…") : "P",
                Summary = summary,
                Severity = request.RequestStatus == ApprovalStatus.Rejected ? (byte)2 : (byte)0,
                RequiresAction = request.RequestStatus is ApprovalStatus.Draft or ApprovalStatus.NeedsRevision,
                InteractionType = "DETAIL",
                DetailRoute = $"/late-early/history?requestId={request.Id}",
                SourceId = request.Id.ToString(),
                SourceType = "LATE_EARLY",
                HasActual = actual.HasValue,
                CheckIn = punch?.CheckInTime,
                CheckOut = punch?.CheckOutTime,
                ShiftAbbr = punch?.ShiftAbbr
            };
        }).ToArray();
    }

    private async Task<List<AttendancePunchRow>> LoadPunchesAsync(
        string employeeCode, DateTime from, DateTime to, CancellationToken ct)
    {
        // Current calculated data takes precedence over archived history for the same employee/date.
        return await _db.Database.SqlQueryRaw<AttendancePunchRow>(
            """
            SELECT a.WorkDate, a.CheckInTime, a.CheckOutTime, a.ShiftAbbr
            FROM dbo.F03HrmAttendanceCalculated AS a
            WHERE a.EmployeeCode = {0} AND a.WorkDate >= {1} AND a.WorkDate <= {2}
            UNION ALL
            SELECT h.WorkDate, h.CheckInTime, h.CheckOutTime, h.ShiftAbbr
            FROM dbo.F03HrmAttendanceHistory AS h
            WHERE h.EmployeeCode = {0} AND h.WorkDate >= {1} AND h.WorkDate <= {2}
              AND NOT EXISTS
              (
                  SELECT 1 FROM dbo.F03HrmAttendanceCalculated AS c
                  WHERE c.EmployeeCode = h.EmployeeCode AND c.WorkDate = h.WorkDate
              )
            """, employeeCode, from, to).ToListAsync(ct);
    }

    private sealed class AttendancePunchRow
    {
        public DateTime WorkDate { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string? ShiftAbbr { get; set; }
    }
}
