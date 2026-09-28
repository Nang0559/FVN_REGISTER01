using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FVN_REGISTER.Core.Entities.PublicForms;

namespace FVN_REGISTER.Core.Entities.Equipment;

/// <summary>
/// Maps a reusable public form to an equipment type/schema key.
/// The form definition remains owned by the generic PublicForms module; this table only
/// declares where the form is applicable. Employees only see forms assigned to equipment
/// that is actually assigned to them.
/// </summary>
[Table("F03EquipmentFormAssignments")]
public sealed class F03EquipmentFormAssignment : BaseAuditEntity
{
    [Required, StringLength(64)] public string EquipmentSchemaKey { get; set; } = string.Empty;
    public int FormId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActiveAssignment { get; set; } = true;

    public F03PublicForm? Form { get; set; }
}
