using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FVN_REGISTER.Core.Entities.OT;

[Table("F03OTRegistrars")]
public sealed class F03OTRegistrar : BaseAuditEntity
{
    public int RegistrationRuleId { get; set; }
    public int DepartmentCode { get; set; }
    public int UserId { get; set; }

    [StringLength(20)]
    public string RegisterType { get; set; } = "OT";

    public F03OTRegistrationRule RegistrationRule { get; set; } = null!;
}
