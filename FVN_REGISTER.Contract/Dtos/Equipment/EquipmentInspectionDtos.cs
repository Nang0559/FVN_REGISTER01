namespace FVN_REGISTER.Contract.Dtos.Equipment;

public sealed class EquipmentInspectionTemplateDto
{
    public int Id { get; set; }
    public string TemplateCode { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public int DeptCode { get; set; }
    public string Frequency { get; set; } = "Daily";
    public int Version { get; set; }
    public string Status { get; set; } = "Draft";
    public string? Description { get; set; }
    public List<EquipmentInspectionItemDto> Items { get; set; } = new();
}
public sealed class EquipmentInspectionItemDto
{
    public int Id { get; set; }
    public string ItemCode { get; set; } = "";
    public string ItemLabel { get; set; } = "";
    public string InputType { get; set; } = "PassFail";
    public bool IsRequired { get; set; }
    public bool RequireImage { get; set; }
    public int MinImages { get; set; }
    public int MaxImages { get; set; } = 3;
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? Unit { get; set; }
    public string? OptionsJson { get; set; }
    public int DisplayOrder { get; set; }
}
public sealed class EquipmentInspectionTemplateUpsertRequest
{
    public int? Id { get; set; }
    public string TemplateCode { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public int DeptCode { get; set; }
    public string Frequency { get; set; } = "Daily";
    public string Status { get; set; } = "Draft";
    public string? Description { get; set; }
    public List<EquipmentInspectionItemDto> Items { get; set; } = new();
}
public sealed class EquipmentInspectionTemplateCloneRequest
{
    public string? TemplateName { get; set; }
}
public sealed class EquipmentInspectionAssignmentRequest
{
    public int EquipmentId { get; set; }
    public int TemplateId { get; set; }
    public string Frequency { get; set; } = "Daily";
    public TimeSpan DueTime { get; set; } = new(8,0,0);
    public int ReminderHoursBefore { get; set; } = 24;
    public int? ScheduleDayOfWeek { get; set; }
    public int? ScheduleDayOfMonth { get; set; }
    public int? ScheduleMonth { get; set; }
    public string InspectorEmployeeCode { get; set; } = "";
    public string ApproverEmployeeCode { get; set; } = "";
    public DateTime EffectiveFrom { get; set; } = DateTime.Today;
    public DateTime? EffectiveTo { get; set; }
}
public sealed class EquipmentInspectionAssignmentDto
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public int TemplateId { get; set; }
    public string Frequency { get; set; } = "Daily";
    public TimeSpan DueTime { get; set; } = new(8,0,0);
    public int ReminderHoursBefore { get; set; } = 24;
    public int? ScheduleDayOfWeek { get; set; }
    public int? ScheduleDayOfMonth { get; set; }
    public int? ScheduleMonth { get; set; }
    public string InspectorEmployeeCode { get; set; } = "";
    public string ApproverEmployeeCode { get; set; } = "";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string EquipmentCode { get; set; } = "";
    public string EquipmentName { get; set; } = "";
    public string TemplateName { get; set; } = "";
}
public sealed class EquipmentInspectionTaskDto
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentCode { get; set; } = "";
    public string EquipmentName { get; set; } = "";
    public string AssetCode { get; set; } = "";
    public int DeptCode { get; set; }
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = "";
    public int TemplateVersion { get; set; }
    public string Status { get; set; } = "";
    public string? Result { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime DueAt { get; set; }
    public string InspectorEmployeeCode { get; set; } = "";
    public string ApproverEmployeeCode { get; set; } = "";
    public List<EquipmentInspectionItemResultDto> Items { get; set; } = new();
    public List<EquipmentInspectionEvidenceDto> Evidence { get; set; } = new();
}
public sealed class EquipmentInspectionItemResultDto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = "";
    public string ItemLabel { get; set; } = "";
    public string InputType { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool RequireImage { get; set; }
    public int MinImages { get; set; }
    public int MaxImages { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? Unit { get; set; }
    public string? OptionsJson { get; set; }
    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
    public bool? Passed { get; set; }
    public string? Note { get; set; }
    public List<EquipmentInspectionEvidenceDto> Evidence { get; set; } = new();
}
public sealed class EquipmentInspectionEvidenceDto
{
    public int Id { get; set; }
    public int? ItemResultId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long FileSize { get; set; }
    public string Url { get; set; } = "";
}
public sealed class EquipmentInspectionSubmitRequest
{
    public int TaskId { get; set; }
    public string? Result { get; set; }
    public List<EquipmentInspectionItemResultDto> Items { get; set; } = new();
}
public sealed class EquipmentInspectionDashboardDto
{
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Pending { get; set; }
    public int Overdue { get; set; }
    public int Failed { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal PassRate { get; set; }
    public List<EquipmentInspectionReportRowDto> Rows { get; set; } = new();
}
public sealed class EquipmentInspectionReportRowDto
{
    public int TaskId { get; set; }
    public int DeptCode { get; set; }
    public string EquipmentCode { get; set; } = "";
    public string EquipmentName { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string InspectorEmployeeCode { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Result { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
public sealed class EquipmentInspectionTemplateImportResultDto
{
    public int TemplateId { get; set; }
    public string TemplateCode { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public int ItemCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>Who may be assigned in one go: a whole import batch ("Cha"), a type group, or hand-picked equipment.</summary>
public static class EquipmentInspectionAssignScope
{
    public const string Batch = "Batch";
    public const string Group = "Group";
    public const string Assets = "Assets";
}

/// <summary>Import batch (the "Cha" Excel list) with how many of its equipment are already approved and active.</summary>
public sealed class EquipmentInspectionBatchGroupDto
{
    public long BatchId { get; set; }
    public string FileName { get; set; } = "";
    public int AssetCount { get; set; }
}

public sealed class EquipmentInspectionTypeGroupDto
{
    public string SchemaKey { get; set; } = "";
    public int AssetCount { get; set; }
}

public sealed class EquipmentInspectionAssetGroupsDto
{
    public List<EquipmentInspectionBatchGroupDto> Batches { get; set; } = new();
    public List<EquipmentInspectionTypeGroupDto> Types { get; set; } = new();
}

public sealed class EquipmentInspectionBulkAssignmentRequest
{
    public int TemplateId { get; set; }
    /// <summary>Batch | Group | Assets (see <see cref="EquipmentInspectionAssignScope"/>).</summary>
    public string Scope { get; set; } = EquipmentInspectionAssignScope.Assets;
    public long? ImportBatchId { get; set; }
    public string? SchemaKey { get; set; }
    public List<int> EquipmentIds { get; set; } = new();
    /// <summary>Optional override. Empty = use the checklist's own frequency.</summary>
    public string? Frequency { get; set; }
    public TimeSpan DueTime { get; set; } = new(8, 0, 0);
    public int ReminderHoursBefore { get; set; } = 24;
    public int? ScheduleDayOfWeek { get; set; }
    public int? ScheduleDayOfMonth { get; set; }
    public int? ScheduleMonth { get; set; }
    public string InspectorEmployeeCode { get; set; } = "";
    public string ApproverEmployeeCode { get; set; } = "";
    public DateTime EffectiveFrom { get; set; } = DateTime.Today;
    public DateTime? EffectiveTo { get; set; }
}

public sealed class EquipmentInspectionBulkAssignmentRowDto
{
    public int EquipmentId { get; set; }
    public string EquipmentCode { get; set; } = "";
    public string EquipmentName { get; set; } = "";
    /// <summary>Created | Skipped | Failed</summary>
    public string Status { get; set; } = "";
    public string? Message { get; set; }
}

public sealed class EquipmentInspectionBulkAssignmentResultDto
{
    public int Requested { get; set; }
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<EquipmentInspectionBulkAssignmentRowDto> Rows { get; set; } = new();
}
