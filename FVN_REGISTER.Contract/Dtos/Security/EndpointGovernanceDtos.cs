using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Contract.Dtos.Security;

public sealed record EndpointGovernancePolicyItemDto(
    int Id,
    EndpointGovernanceItemType ItemType,
    string NormalizedName,
    string? DisplayName,
    string? Publisher,
    string? VersionConstraint,
    bool IsAllowed,
    string? Remark,
    string? AliasNames = null);

public sealed record EndpointGovernancePolicyDto(
    int Id,
    string PolicyCode,
    string PolicyName,
    EndpointGovernanceItemType ItemType,
    EndpointTargetType TargetType,
    int Version,
    bool IsPublished,
    string WorkflowStatus,
    int? ApprovalRequestId,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    IReadOnlyList<EndpointGovernancePolicyItemDto> Items,
    string? Remark,
    bool ChecklistCompleted = false,
    DateTime? ChecklistCompletedAtUtc = null,
    int? ChecklistCompletedBy = null,
    string? ChecklistNote = null);

public sealed record EndpointGovernancePolicyUpsertRequest(
    string PolicyCode,
    string PolicyName,
    EndpointGovernanceItemType ItemType,
    EndpointTargetType TargetType,
    string? Remark,
    IReadOnlyList<EndpointGovernancePolicyItemDto> Items,
    bool ChecklistCompleted = false,
    string? ChecklistNote = null);

public sealed record EndpointGovernanceRequestCreateDto(
    EndpointGovernanceRequestType RequestType,
    long? EndpointDeviceId,
    int? PolicyId,
    int? PolicyItemId,
    string? ItemName,
    string? Publisher,
    string? RequestedVersion,
    string Reason);

public sealed record EndpointGovernanceRequestDto(
    int Id,
    EndpointGovernanceRequestType RequestType,
    long? EndpointDeviceId,
    int? PolicyId,
    int? PolicyVersion,
    int? PolicyItemId,
    EndpointGovernanceItemType? PolicyItemType,
    string? EmployeeCode,
    string? DeptCode,
    string? ItemName,
    string? Publisher,
    string? RequestedVersion,
    string Reason,
    string SecurityReviewStatus,
    string WorkflowStatus,
    int RequestStatus,
    int? ApprovalSnapshotId,
    DateTime CreatedAt,
    DateTime? ReviewedAtUtc);

public sealed record EndpointComplianceFindingDto(
    long Id,
    long EndpointDeviceId,
    int? PolicyId,
    int PolicyVersion,
    int? PolicyItemId,
    EndpointGovernanceItemType ItemType,
    long? InventoryItemId,
    EndpointComplianceResult Result,
    string ObservedName,
    string? ObservedVersion,
    string FindingCode,
    string? FindingMessage,
    DateTime EvaluatedAtUtc,
    DateTime? ResolvedAtUtc);
