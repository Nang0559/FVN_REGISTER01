namespace FVN_REGISTER.Contract.Dtos.Execution;

public sealed record ExecutionHrResolutionSummaryDto(
    long Id,
    string Decision,
    string Reason,
    string CalendarAction,
    DateTime ResolvedAt);

public sealed record ExecutionEmployeeDecisionSummaryDto(
    string Status,
    string? Comment,
    byte AppealRound,
    DateTime? DecidedAt,
    bool CanRespond,
    bool CanAppeal,
    byte MaxAppealRounds);

public sealed record ExecutionReconciliationDetailDto(
    ExecutionReconciliationDto Reconciliation,
    ExecutionConfirmationDto? Confirmation,
    IReadOnlyList<ExecutionEvidenceDto> Evidence,
    ExecutionHrResolutionSummaryDto? HrResolution,
    ExecutionEmployeeDecisionSummaryDto? EmployeeDecision);
