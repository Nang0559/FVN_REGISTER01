using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Core.Entities.LateEarly;

/// <summary>
/// Đăng ký đi muộn / về sớm. EmployeeCode = công nhân; RegisteredByEmployeeCode = người đăng ký hộ
/// (tổ trưởng/người phụ trách được chỉ định). Công nhân không cần thiết bị di động.
/// </summary>
[Table("F03LateEarlyRequests")]
public class F03LateEarlyRequest : BaseRequestEntity
{
    [Required, StringLength(30)]
    public string RequestCode { get; set; } = string.Empty;

    public LateEarlyType LateEarlyType { get; set; }

    public DateTime WorkDate { get; set; }

    /// <summary>Giờ dự kiến đến (đi muộn) hoặc rời xưởng (về sớm).</summary>
    public TimeSpan PlannedTime { get; set; }

    [Required, StringLength(30)]
    public string ReasonCode { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ReasonNote { get; set; }

    [Required, StringLength(50)]
    public string RegisteredByEmployeeCode { get; set; } = string.Empty;

    public bool IsOnBehalf { get; set; }

    /// <summary>Đăng ký bổ sung sau thời điểm đã xảy ra (cần lý do + cấp duyệt cao hơn).</summary>
    public bool IsRetroactive { get; set; }

    public TimeSpan? ActualTime { get; set; }
    public int? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    [StringLength(500)]
    public string? CancelReason { get; set; }
}
