using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Contract.Dtos.Security;

public sealed record EndpointGovernanceExcelPreviewDto(
    string FileName,
    EndpointGovernanceItemType ItemType,
    EndpointTargetType TargetType,
    string SuggestedPolicyName,
    int RowCount,
    IReadOnlyList<EndpointGovernanceExcelRowDto> Rows,
    IReadOnlyList<string> Errors);

public sealed record EndpointGovernanceExcelRowDto(
    int RowNumber,
    string Name,
    string? Publisher,
    string? VersionConstraint,
    bool IsAllowed,
    string? Remark,
    string? Error);

public sealed record EndpointGovernanceExcelImportRequest(
    string PolicyCode,
    string PolicyName,
    EndpointGovernanceItemType ItemType,
    EndpointTargetType TargetType,
    string? Remark);
