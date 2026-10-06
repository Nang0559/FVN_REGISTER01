using FVN_REGISTER.Contract.Requests.Approvals;
using System.ComponentModel.DataAnnotations;

namespace FVN_REGISTER.Contract.Dtos.Equipment;

public sealed class CreateEquipmentRegistrationDto
{
    [Required, StringLength(250)] public string EquipmentName { get; set; } = string.Empty;
    [StringLength(1000)] public string? Specification { get; set; }
    [StringLength(100)] public string? SerialNumber { get; set; }
    [StringLength(50)] public string? AssetCode { get; set; }
    [Range(0, 999999999999)] public decimal PurchasePrice { get; set; }
    [Required] public DateTime PurchaseDate { get; set; }
    [Required] public DateTime ExpectedDepreciationDate { get; set; }
    public int DeptCode { get; set; }
    [StringLength(250)] public string? Location { get; set; }
    /// <summary>Optional initial asset custodian. Must resolve to an active HR employee when supplied.</summary>
    [StringLength(50)] public string? ResponsibleEmployeeCode { get; set; }
    [StringLength(50)] public string? SelectedApproverCode { get; set; }
    public List<ApprovalSelectionDto> ApprovalSelections { get; set; } = new();
    [StringLength(1000)] public string? Note { get; set; }
    /// <summary>Whether the approved asset may participate in the Endpoint Agent lifecycle.</summary>
    public bool EndpointAgentEligible { get; set; }
    [StringLength(30)] public string? EndpointOsFamily { get; set; }
}
