using System.Globalization;

namespace FVN_REGISTER.Contract.Dtos.Calendar;

/// <summary>Ký hiệu công / ký hiệu làm thêm (OT) của một ngày trên lịch.</summary>
public readonly record struct AttendanceSymbols(string? WorkSymbol, string? OtSymbol);

/// <summary>
/// Nguồn duy nhất để tính ký hiệu công / OT cho lịch.
/// Ô ngày (workCalendar.js) và popup (CalendarDayDialog) đều dùng class này nên luôn khớp nhau.
/// </summary>
public static class AttendanceSymbolCalculator
{
    /// <summary>
    /// Ưu tiên giá trị do rule đã mã hóa trong DTO (khi có SymbolRuleCode),
    /// ngược lại tính theo ca / ngày lễ / T7 / CN rồi mới fallback về DTO.
    /// </summary>
    public static AttendanceSymbols Resolve(CalendarDayDto day)
    {
        var attendance = day.Attendance;
        if (attendance is null)
            return default;

        if (!string.IsNullOrWhiteSpace(attendance.SymbolRuleCode))
            return new AttendanceSymbols(attendance.Symbol, attendance.OtSymbol);

        var calculated = Calculate(day);
        return new AttendanceSymbols(
            calculated.WorkSymbol ?? attendance.Symbol,
            calculated.OtSymbol ?? attendance.OtSymbol);
    }

    public static AttendanceSymbols Calculate(CalendarDayDto day)
    {
        if (day.Attendance is null)
            return default;

        return new AttendanceSymbols(BuildWorkSymbol(day), BuildOtSymbol(day));
    }

    private static string? BuildWorkSymbol(CalendarDayDto day)
    {
        var attendance = day.Attendance!;
        var shift = NormalizeShift(day.Shift?.ShiftAbbr, attendance.CheckIn);
        var isHoliday = day.Holiday is not null;
        var isSaturday = day.Date.DayOfWeek == DayOfWeek.Saturday;
        var isSunday = day.Date.DayOfWeek == DayOfWeek.Sunday;

        if (!isHoliday && !isSaturday && !isSunday)
            return shift;

        var prefix = isHoliday ? "NL" : isSunday ? "CN" : "T";
        var elapsed = ElapsedMinutes(attendance.CheckIn, attendance.CheckOut);
        if (elapsed <= 0)
            elapsed = Math.Max(0, attendance.WorkMinutes);
        if (elapsed <= 0)
            return null;

        if (elapsed >= 11 * 60 + 45)
            elapsed = Math.Min(8 * 60, elapsed - 4 * 60);

        elapsed = RoundDown15(elapsed);
        var hours = FormatHours(elapsed);

        return shift switch
        {
            "C2" => prefix + (prefix == "T" ? "2" : "C2") + hours,
            "C3" => prefix + (prefix == "T" ? "3" : "C3") + hours,
            _ => prefix + hours
        };
    }

    private static string? BuildOtSymbol(CalendarDayDto day)
    {
        var attendance = day.Attendance!;
        var shift = NormalizeShift(day.Shift?.ShiftAbbr, attendance.CheckIn);
        var isHoliday = day.Holiday is not null;
        var isSaturday = day.Date.DayOfWeek == DayOfWeek.Saturday;
        var isSunday = day.Date.DayOfWeek == DayOfWeek.Sunday;
        var isOffDay = isHoliday || isSaturday || isSunday;

        var minutes = Math.Max(attendance.ActualOtMinutes, attendance.RecognizedOtMinutes);
        var elapsed = ElapsedMinutes(attendance.CheckIn, attendance.CheckOut);

        if (isOffDay && elapsed >= 11 * 60 + 45 && minutes <= 0)
            minutes = 4 * 60;

        if (minutes <= 0)
            return null;

        minutes = Math.Min(4 * 60, RoundDown15(minutes));
        if (minutes <= 0)
            return null;

        var hours = FormatHours(minutes);
        if (isOffDay)
        {
            var prefix = isHoliday ? "NL" : isSunday ? "CN" : "T";
            var k = shift is "C1" or "C3";
            return k ? prefix + "K" + hours : prefix + hours;
        }

        var kNormal = shift is "C1" or "C2" or "C3";
        if (shift == "HC")
            kNormal = attendance.CheckOut?.TimeOfDay >= new TimeSpan(16, 45, 0);

        return kNormal ? "K" + hours : hours;
    }

    internal static string NormalizeShift(string? shiftAbbr, DateTime? checkIn)
    {
        var text = (shiftAbbr ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);
        if (text.Contains("C1") || text.Contains("CA1")) return "C1";
        if (text.Contains("C2") || text.Contains("CA2")) return "C2";
        if (text.Contains("C3") || text.Contains("CA3")) return "C3";
        if (text.Contains("HC")) return "HC";

        if (checkIn.HasValue)
        {
            var time = checkIn.Value.TimeOfDay;
            if (time >= new TimeSpan(5, 0, 0) && time < new TimeSpan(9, 0, 0)) return "C1";
            if (time >= new TimeSpan(13, 0, 0) && time < new TimeSpan(17, 0, 0)) return "C2";
            if (time >= new TimeSpan(21, 0, 0) || time < new TimeSpan(2, 0, 0)) return "C3";
            if (time >= new TimeSpan(8, 0, 0) && time < new TimeSpan(13, 0, 0)) return "HC";
        }

        return "HC";
    }

    private static int ElapsedMinutes(DateTime? checkIn, DateTime? checkOut)
    {
        if (!checkIn.HasValue || !checkOut.HasValue || checkOut.Value <= checkIn.Value)
            return 0;
        return (int)Math.Round((checkOut.Value - checkIn.Value).TotalMinutes, MidpointRounding.AwayFromZero);
    }

    private static int RoundDown15(int minutes) => Math.Max(0, minutes / 15 * 15);

    private static string FormatHours(int minutes) =>
        (minutes / 60m).ToString("0.##", CultureInfo.InvariantCulture);
}
