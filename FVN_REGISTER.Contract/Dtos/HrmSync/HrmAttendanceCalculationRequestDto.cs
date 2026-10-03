namespace FVN_REGISTER.Contract.Dtos.HrmSync;
public sealed class HrmAttendanceCalculationRequestDto
{
 public string? DeptCode { get; set; }
 public string? EmployeeCode { get; set; }
 public DateTime FromDate { get; set; } = DateTime.Today;
 public DateTime ToDate { get; set; } = DateTime.Today;
 /// <summary>Calendar backfill must not reopen a locked/exported payroll period.</summary>
 public bool ReopenPayrollPeriod { get; set; } = true;
}