namespace FVN_REGISTER.Contract.Dtos.Dashboard;

/// <summary>Employee-level read-only detail for approver dashboard cards.</summary>
public sealed class DepartmentDashboardDetailDto
{
    public string Kind { get; set; } = string.Empty;
    public DateTime WorkDate { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal? Hours { get; set; }
}