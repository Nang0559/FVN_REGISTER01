using FVN_REGISTER.Contract.Dtos.Calendar;

namespace FVN_REGISTER.Application.Models.Calendar;

public sealed class CalendarDayRuleContext
{
    public CalendarDayRuleContext(CalendarDayDto day, IReadOnlyList<CalendarItemDto> sourceItems, bool isOtRegistrar = false)
    {
        IsOtRegistrar = isOtRegistrar;
        Day = day;
        SourceItems = sourceItems;
    }

    /// <summary>true: user hiện tại được đăng ký OT trực tiếp; false: chỉ được phản hồi cấp trên.</summary>
    public bool IsOtRegistrar { get; }
    public CalendarDayDto Day { get; }
    public IReadOnlyList<CalendarItemDto> SourceItems { get; }
}