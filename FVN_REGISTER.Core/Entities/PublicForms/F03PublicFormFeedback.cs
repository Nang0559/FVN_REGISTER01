using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FVN_REGISTER.Core.Entities;

namespace FVN_REGISTER.Core.Entities.PublicForms;

[Table("F03PublicFormFeedbacks")]
public sealed class F03PublicFormFeedback : BaseAuditEntity
{
    public int FormId { get; set; }
    public int? SubmissionId { get; set; }

    [Required, StringLength(50)]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;
}