using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FVN_REGISTER.Core.Entities.OT;

[Table("F03OTRegistrarPositions")]
public sealed class F03OTRegistrarPosition : BaseAuditEntity
{
    public int RegistrationRuleId { get; set; }
    public int DepartmentCode { get; set; }

    [Required, StringLength(20)]
    public string PositionCode { get; set; } = string.Empty;

    [StringLength(20)]
    public string RegisterType { get; set; } = "OT";

    public F03OTRegistrationRule RegistrationRule { get; set; } = null!;
}
