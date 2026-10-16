using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FVN_REGISTER.Core.Entities.Common;

/// <summary>
/// Signed / stamped documents (PDF, images, Office files) attached to a public information item.
/// Kept separate from F03Attachment on purpose: F03Attachment.Module is the RequestModule enum, which also
/// drives the approval-policy screens, so a "PublicInformation" member would leak into those lists.
/// </summary>
[Table("F03PublicInformationAttachment")]
public sealed class F03PublicInformationAttachment : BaseAuditEntity
{
    public int PublicInformationId { get; set; }
    [Required, StringLength(260)] public string FileName { get; set; } = string.Empty;
    /// <summary>Path relative to ContentRootPath. Never returned to clients.</summary>
    [Required, StringLength(500)] public string FilePath { get; set; } = string.Empty;
    [StringLength(20)] public string? FileExtension { get; set; }
    public long FileSize { get; set; }
}
