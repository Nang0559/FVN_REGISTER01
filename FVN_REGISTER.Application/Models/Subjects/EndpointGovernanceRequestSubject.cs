using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Core.Entities.Security;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Application.Models.Subjects;

public sealed class EndpointGovernanceRequestSubject : IApprovalSubject
{
    public int RequestId { get; set; }
    public RequestModule Module => RequestModule.Endpoint;
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string? DeptCode { get; set; }
    public string? PositionCode { get; set; }
    public ApprovalStatus OverallStatus { get; set; }
    public EndpointGovernanceRequestType RequestType { get; set; }
    public long? EndpointDeviceId { get; set; }
    public int? PolicyId { get; set; }
    public int? PolicyVersion { get; set; }
    public int? PolicyItemId { get; set; }
    public EndpointGovernanceItemType? PolicyItemType { get; set; }
    public string? ItemName { get; set; }
    public string? Publisher { get; set; }
    public string? RequestedVersion { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string SecurityReviewStatus { get; set; } = "Pending";
    public string WorkflowStatus { get; set; } = "Draft";

    public static EndpointGovernanceRequestSubject From(
        F03EndpointGovernanceRequest entity,
        string? employeeName,
        string? positionCode) => new()
    {
        RequestId = entity.Id,
        EmployeeCode = entity.EmployeeCode,
        EmployeeName = employeeName,
        DeptCode = entity.DeptCode?.ToString(),
        PositionCode = positionCode,
        OverallStatus = entity.RequestStatus,
        RequestType = entity.RequestType,
        EndpointDeviceId = entity.EndpointDeviceId,
        PolicyId = entity.PolicyId,
        PolicyVersion = entity.PolicyVersion,
        PolicyItemId = entity.PolicyItemId,
        PolicyItemType = entity.PolicyItemType,
        ItemName = entity.ItemName,
        Publisher = entity.Publisher,
        RequestedVersion = entity.RequestedVersion,
        Reason = entity.Reason,
        SecurityReviewStatus = entity.SecurityReviewStatus,
        WorkflowStatus = entity.WorkflowStatus
    };
}
