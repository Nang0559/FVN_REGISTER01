using FVN_REGISTER.Contract.Requests.Approvals;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Contract.Dtos.Registration;

public sealed class SupervisorFeedbackDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int? DepartmentCode { get; set; }
    public DateTime WorkDate { get; set; }
    public DateTime? ActualIn { get; set; }
    public DateTime? ActualOut { get; set; }
    public SupervisorFeedbackStatus Status { get; set; }
    public int? ApproverId { get; set; }
    public string? Note { get; set; }
    /// <summary>Lý do từ chối / ghi chú của người xử lý.</summary>
    public string? ResponseNote { get; set; }
    /// <summary>OT thực tế (phút) của ngày đó theo chấm công, để người xử lý đăng ký đúng số giờ.</summary>
    public int ActualOtMinutes { get; set; }
    /// <summary>Loại OT đề xuất theo ngày nghỉ HRM: WD, RESTDAY hoặc HOL.</summary>
    public string SuggestedOTTypeCode { get; set; } = "WD";
    public int? OTRequestId { get; set; }
}

public sealed class SupervisorFeedbackCreateDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime WorkDate { get; set; }
    public string? Note { get; set; }
}

public sealed class SupervisorFeedbackDecisionDto
{
    public string? Note { get; set; }
    public string? OTTypeCode { get; set; }
    /// <summary>Giờ OT bắt đầu/kết thúc người xử lý xác nhận; bỏ trống thì suy ra từ OT thực tế.</summary>
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public List<ApprovalSelectionDto>? ApprovalSelections { get; set; }
}
