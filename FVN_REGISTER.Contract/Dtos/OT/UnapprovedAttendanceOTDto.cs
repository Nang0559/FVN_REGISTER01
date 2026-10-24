namespace FVN_REGISTER.Contract.Dtos.OT;

public sealed class UnapprovedAttendanceOTDto
{
    public DateTime WorkDate { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? ShiftAbbr { get; set; }
    public string? OtSymbol { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public decimal OTHours { get; set; }
}
