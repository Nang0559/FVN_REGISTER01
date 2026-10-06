namespace FVN_REGISTER.Contract.Dtos.Equipment;

public sealed class EquipmentAssetDto
{
    public int Id { get; set; }
    public string EquipmentCode { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string? SerialNumber { get; set; }
    public string? AssetCode { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime ExpectedDepreciationDate { get; set; }
    public int DeptCode { get; set; }
    public string? Location { get; set; }
    public string QrToken { get; set; } = string.Empty;
    public string QrUrl { get; set; } = string.Empty;
    public bool IsQrActive { get; set; }
    public string? Note { get; set; }
    public bool EndpointAgentEligible { get; set; }
    public string? EndpointOsFamily { get; set; }
    public long? EndpointDeviceId { get; set; }
    public string? EndpointDeviceKey { get; set; }
    public string? EndpointAgentStatus { get; set; }
    public string? EndpointAgentVersion { get; set; }
    public DateTime? EndpointLastSeenUtc { get; set; }
    public bool EndpointHasActiveCredential { get; set; }
    public DateTimeOffset? EndpointCredentialExpiresAtUtc { get; set; }
    public int? ResponsibleDeptCode { get; set; }
    public string? ResponsibleEmployeeCode { get; set; }
    public string? ResponsibleEmployeeName { get; set; }
    public string? ResponsibleApproverEmployeeCode { get; set; }
    public string? ResponsibleApproverEmployeeName { get; set; }
    public DateTime? ResponsibleAssignedAt { get; set; }
    public int? OperatingResponsibleDeptCode { get; set; }
    public string? OperatingResponsibleDeptName { get; set; }
    public string? OperatingResponsibleEmployeeCode { get; set; }
    public string? OperatingResponsibleEmployeeName { get; set; }
    public DateTime? OperatingResponsibleAssignedAt { get; set; }
    public List<EquipmentRepairHistoryDto> RepairHistory { get; set; } = new();
    public List<EquipmentHandoverHistoryDto> HandoverHistory { get; set; } = new();
}

public sealed class EquipmentRepairHistoryDto
{
    public int Id { get; set; }
    public DateTime RepairDate { get; set; }
    public int OperatorUserId { get; set; }
    public string? OperatorName { get; set; }
    public decimal? RepairCost { get; set; }
    public string RepairContent { get; set; } = string.Empty;
    public string? RepairVendor { get; set; }
    public string? RepairResult { get; set; }
    public string? Note { get; set; }
    public int? ResponsibleDeptCode { get; set; }
    public string? RepairerEmployeeCode { get; set; }
    public string? RepairFeedback { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class EquipmentHandoverHistoryDto
{
    public int Id { get; set; }
    public DateTime HandoverAt { get; set; }
    public string? PreviousResponsibleEmployeeCode { get; set; }
    public string? NewResponsibleEmployeeCode { get; set; }
    public string? PreviousApproverEmployeeCode { get; set; }
    public string? NewApproverEmployeeCode { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int HandoverByUserId { get; set; }
}
