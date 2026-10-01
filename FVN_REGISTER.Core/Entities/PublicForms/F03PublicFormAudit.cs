using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FVN_REGISTER.Core.Entities;

namespace FVN_REGISTER.Core.Entities.PublicForms;

[Table("F03PublicFormAudits")]
public sealed class F03PublicFormAudit : BaseAuditEntity
{
    public int FormId { get; set; }

    [Required, StringLength(40)]
    public string ActionCode { get; set; } = string.Empty;

    [StringLength(50)]
    public string? ActorEmployeeCode { get; set; }

    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}