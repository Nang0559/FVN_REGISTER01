using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Core.Entities.WorkCalendar;

[Table("F03SupervisorFeedbacks")]
public sealed class F03SupervisorFeedback : BaseAuditEntity
{
    public int EmployeeId { get; set; }

    [Required, StringLength(50)]
    public string EmployeeCode { get; set; } = string.Empty;

    public DateTime WorkDate { get; set; }
    public DateTime? ActualIn { get; set; }
    public DateTime? ActualOut { get; set; }
    public int? DepartmentCode { get; set; }
    public int? ApproverId { get; set; }
    public SupervisorFeedbackStatus Status { get; set; } = SupervisorFeedbackStatus.PendingSupervisor;

    [StringLength(1000)]
    public string? Note { get; set; }

    /// <summary>Lý do từ chối / ghi chú của người xử lý (Note là nội dung nhân viên gửi).</summary>
    [StringLength(1000)]
    public string? ResponseNote { get; set; }

    public int? OTRequestId { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
