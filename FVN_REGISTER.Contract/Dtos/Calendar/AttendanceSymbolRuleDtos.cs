namespace FVN_REGISTER.Contract.Dtos.Calendar;

public sealed class AttendanceSymbolRuleDto
{
    public int Id { get; init; }
    public string RuleCode { get; init; } = string.Empty;
    public string RuleName { get; init; } = string.Empty;
    public string DayType { get; init; } = "NORMAL";
    public string? ShiftCode { get; init; }
    public int? ShiftId { get; init; }
    public int Priority { get; init; }
    public int? MinActualMinutes { get; init; }
    public int? MaxActualMinutes { get; init; }
    public string? CheckInFrom { get; init; }
    public string? CheckInTo { get; init; }
    public bool IsActive { get; init; }
    public string? EffectiveFrom { get; init; }
    public string? EffectiveTo { get; init; }
    public List<AttendanceSymbolRuleSegmentDto> Segments { get; init; } = new();
}

public sealed class AttendanceSymbolRuleSegmentDto
{
    public string SegmentType { get; set; } = "WORK";
    public string Start { get; set; } = "00:00";
    public string End { get; set; } = "00:00";
    public string SymbolTemplate { get; set; } = string.Empty;
    public int? FixedMinutes { get; set; }
}

public sealed class AttendanceSymbolRuleUpsertRequest
{
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string DayType { get; set; } = "NORMAL";
    public string? ShiftCode { get; set; }
    public int? ShiftId { get; set; }
    public int Priority { get; set; } = 100;
    public int? MinActualMinutes { get; set; }
    public int? MaxActualMinutes { get; set; }
    public string? CheckInFrom { get; set; }
    public string? CheckInTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public int BlockMinutes { get; set; } = 15;
    public string RoundingMode { get; set; } = "FLOOR";
    public string AllocationMode { get; set; } = "STANDARD";
    public int? FixedOtMinutes { get; set; }
    public List<AttendanceSymbolRuleSegmentDto> Segments { get; set; } = new();
}

public sealed class AttendanceSymbolRuleTestRequest
{
    public DateOnly WorkDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string DayType { get; set; } = "NORMAL";
    public string? ShiftCode { get; set; }
    public int? ShiftId { get; set; }
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public int? ActualOtMinutes { get; set; }
}

public sealed class AttendanceSymbolRuleTestResultDto
{
    public bool Matched { get; init; }
    public string? RuleCode { get; init; }
    public string? DayType { get; init; }
    public string? WorkSymbol { get; init; }
    public string? OtSymbol { get; init; }
    public int WorkMinutesForSymbol { get; init; }
    public int OtMinutesForSymbol { get; init; }
    public int RawElapsedMinutes { get; init; }
    public int RoundedOtMinutes { get; init; }
    public string? Explanation { get; init; }
}
