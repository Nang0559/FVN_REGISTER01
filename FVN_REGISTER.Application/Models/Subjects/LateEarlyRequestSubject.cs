using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Core.Entities.LateEarly;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Application.Models.Subjects;

/// <summary>Approval snapshot for late-arrival / early-leave requests.</summary>
public sealed class LateEarlyRequestSubject : IApprovalSubject
{
    public int RequestId { get; set; }
    public RequestModule Module => RequestModule.LateEarly;
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int? DeptCode { get; set; }
    public string? PositionCode { get; set; }
    public ApprovalStatus OverallStatus { get; set; }
    public LateEarlyType LateEarlyType { get; set; }
    public DateTime WorkDate { get; set; }
    public TimeSpan PlannedTime { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? ReasonNote { get; set; }
    public bool IsRetroactive { get; set; }

    public static LateEarlyRequestSubject From(
        F03LateEarlyRequest entity,
        string? employeeName = null,
        string? positionCode = null) => new()
    {
        RequestId = entity.Id,
        EmployeeCode = entity.EmployeeCode,
        EmployeeName = employeeName,
        DeptCode = entity.DeptCode,
        PositionCode = positionCode,
        OverallStatus = entity.RequestStatus,
        LateEarlyType = entity.LateEarlyType,
        WorkDate = entity.WorkDate,
        PlannedTime = entity.PlannedTime,
        ReasonCode = entity.ReasonCode,
        ReasonNote = entity.ReasonNote,
        IsRetroactive = entity.IsRetroactive
    };
}
