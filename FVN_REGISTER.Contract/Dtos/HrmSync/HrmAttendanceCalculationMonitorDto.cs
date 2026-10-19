namespace FVN_REGISTER.Contract.Dtos.HrmSync;

public sealed class HrmAttendanceCalculationMonitorDto
{
    public Guid CalculationBatchId { get; set; }
    public int? DeptCode { get; set; }
    public string? EmployeeCode { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string RunStatus { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? TriggeredBy { get; set; }
    public string CalculationVersion { get; set; } = string.Empty;
    public int TotalDateCount { get; set; }
    public int CompletedDateCount { get; set; }
    public DateTime? CurrentWorkDate { get; set; }
    public DateTime? LastProgressAt { get; set; }
    public int? SqlSessionId { get; set; }
    public DateTime? SqlSessionLoginTime { get; set; }
    public Guid? SqlConnectionId { get; set; }
    public string? SqlStatus { get; set; }
    public string? WaitType { get; set; }
    public int? BlockingSessionId { get; set; }
    public int? WaitTimeMs { get; set; }
    public int? CpuTimeMs { get; set; }
    public long? LogicalReads { get; set; }
    public long? Reads { get; set; }
    public long? Writes { get; set; }
    public string? CurrentStatement { get; set; }
    public int ElapsedSeconds { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsStalled { get; set; }
    public string DisplayStatus { get; set; } = string.Empty;
}
