namespace FVN_REGISTER.Contract.Dtos.Jobs;

public sealed class BackgroundJobScheduleDto
{
    public string JobKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int IntervalMinutes { get; set; }
    public int DefaultIntervalMinutes { get; set; }
    public bool AllowDisable { get; set; } = true;
    public bool RunRequested { get; set; }
    public DateTime? LastStartedAt { get; set; }
    public DateTime? LastFinishedAt { get; set; }
    public string? LastStatus { get; set; }
    public int? LastDurationMs { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? ModifiedAt { get; set; }

    /// <summary>Estimate: last finish + interval (workers wait AFTER finishing). Null when unknown/disabled.</summary>
    public DateTime? NextRunAt { get; set; }
}

public sealed class UpdateBackgroundJobScheduleRequest
{
    public bool IsEnabled { get; set; } = true;
    public int IntervalMinutes { get; set; }
}
