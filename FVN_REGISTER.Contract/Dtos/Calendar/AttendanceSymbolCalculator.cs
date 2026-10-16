namespace FVN_REGISTER.Contract.Dtos.Calendar;

/// <summary>Ký hiệu công / ký hiệu làm thêm (OT) của một ngày trên lịch.</summary>
public readonly record struct AttendanceSymbols(string? WorkSymbol, string? OtSymbol);

/// <summary>
/// KHÔNG tự tính ký hiệu. Ký hiệu công/OT chỉ do rule F03AttendanceSymbolRules quyết định lúc tính công
/// (HrmAttendanceCalculationService.ApplySymbolRulesAsync, hoặc AttendanceSymbolRuleService khi lưu rule)
/// và được ghi vào F03HrmAttendanceCalculated.WorkSymbol/OtSymbol/SymbolRuleCode.
/// Lịch và xuất chấm công/OT chỉ đọc lại giá trị đã ghi; ngày không khớp rule nào thì không có ký hiệu OT
/// và hiển thị đúng giá trị HRM (cần bổ sung rule ở /admin/attendance-symbol-rules).
/// </summary>
public static class AttendanceSymbolCalculator
{
    public static AttendanceSymbols Resolve(CalendarDayDto day)
    {
        var attendance = day.Attendance;
        return attendance is null
            ? default
            : new AttendanceSymbols(attendance.Symbol, attendance.OtSymbol);
    }
}
