namespace FVN_REGISTER.Contract.Dtos.Dashboard;

/// <summary>
/// Read-only dashboard projection of official calculated attendance.
/// Counts describe calculated rows only; they must not be interpreted as the full expected roster.
/// </summary>
public sealed class AttendanceShiftDashboardDto
{
    public DateTime WorkDate { get; set; }
    public int DepartmentCode { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string ShiftAbbr { get; set; } = string.Empty;
    public int CalculatedEmployees { get; set; }
    public int CheckedInEmployees { get; set; }
    public int CheckedOutEmployees { get; set; }
    public int MissingCheckInOrOut { get; set; }
    public DateTime? LatestCalculatedAt { get; set; }
    public List<AttendanceShiftEmployeeDto> Employees { get; set; } = new();
}

public sealed class AttendanceShiftEmployeeDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? AttendanceDisplayValue { get; set; }
    public string? OtDisplayValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CalculatedAt { get; set; }
}
