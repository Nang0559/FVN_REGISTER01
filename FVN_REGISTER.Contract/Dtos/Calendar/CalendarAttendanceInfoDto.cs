namespace FVN_REGISTER.Contract.Dtos.Calendar;
public sealed class CalendarAttendanceInfoDto
{
 public DateTime? CheckIn{get;init;} public DateTime? CheckOut{get;init;} public int WorkMinutes{get;init;} public int RequiredMinutes{get;init;} public decimal? ActualHours{get;init;} public decimal? RequiredHours{get;init;} public int ActualOtMinutes{get;init;} public int RecognizedOtMinutes{get;init;} public bool HasActual{get;init;} public bool HasActualOt{get;init;} public bool IsNumericVariance{get;init;} public string? DisplayValue{get;init;}
 public string? Symbol=>Decode(0)??DisplayValue; public string? OtSymbol=>Decode(1); public string? DayType=>Decode(2); public string? BackgroundColor=>Decode(3); public string? SymbolRuleCode=>Decode(4);
 public string? SourceId{get;init;} public Guid? ActionId{get;init;} public string? DetailRoute{get;init;}
 private string? Decode(int index){if(string.IsNullOrWhiteSpace(DisplayValue)||!DisplayValue.StartsWith("@@ATT-SYMBOL@@",StringComparison.Ordinal))return null;var p=DisplayValue[14..].Split('§');return index<p.Length&&!string.IsNullOrWhiteSpace(p[index])?p[index]:null;}
}
