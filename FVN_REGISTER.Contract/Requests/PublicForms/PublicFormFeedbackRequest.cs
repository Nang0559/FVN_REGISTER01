using System.ComponentModel.DataAnnotations;

namespace FVN_REGISTER.Contract.Requests.PublicForms;

public sealed class PublicFormFeedbackRequest
{
    public int? SubmissionId { get; set; }

    [Required, StringLength(4000)]
    public string Content { get; set; } = string.Empty;
}