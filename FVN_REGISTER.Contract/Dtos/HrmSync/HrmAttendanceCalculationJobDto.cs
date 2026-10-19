namespace FVN_REGISTER.Contract.Dtos.HrmSync;

public sealed class HrmAttendanceCalculationJobDto
{
    public Guid JobId { get; set; }
    public int? DeptCode { get; set; }
    public string? EmployeeCode { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string DisplayStatus { get; set; } = string.Empty;
    public DateTime? CurrentWorkDate { get; set; }
    public int CompletedDateCount { get; set; }
    public int TotalDateCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastProgressAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? CancelRequestedAt { get; set; }
    public Guid? LastCalculationBatchId { get; set; }
    public int? SqlSessionId { get; set; }
    public string? SqlStatus { get; set; }
    public string? WaitType { get; set; }
    public int? BlockingSessionId { get; set; }
    public int? WaitTimeMs { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsStalled { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class HrmAttendanceCalculationJobCommandDto
{
    public int? DeptCode { get; set; }
    public string? EmployeeCode { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public bool ReopenPayrollPeriod { get; set; }
}
