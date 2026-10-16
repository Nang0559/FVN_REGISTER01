using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Core.Entities.OT;

/// <summary>
/// Department-level policy for who may create OT/Trip registrations for other employees.
/// DepartmentCode = 0 is the fallback rule.
/// </summary>
[Table("F03OTRegistrationRules")]
public sealed class F03OTRegistrationRule : BaseAuditEntity
{
    public int DepartmentCode { get; set; }

    [Required]
    public RegistrationRuleMode Mode { get; set; } = RegistrationRuleMode.AssignedPositions;

    [Required, StringLength(20)]
    public string RegisterType { get; set; } = "OT";

    public bool IsActive { get; set; } = true;

    public ICollection<F03OTRegistrar> Registrars { get; set; } = new List<F03OTRegistrar>();
    public ICollection<F03OTRegistrarPosition> RegistrarPositions { get; set; } = new List<F03OTRegistrarPosition>();
}
