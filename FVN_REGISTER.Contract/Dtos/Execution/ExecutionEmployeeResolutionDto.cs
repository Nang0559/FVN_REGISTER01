namespace FVN_REGISTER.Contract.Dtos.Execution;

public sealed record ExecutionEmployeeResolutionDto(
    long ReconciliationId,
    string Status,
    string Decision,
    string? Comment,
    byte AppealRound,
    byte MaxAppealRounds,
    bool CanAccept,
    bool CanAppeal,
    bool IsFinal,
    DateTime? DecidedAt);
public sealed class ExecutionEmployeeDecisionRequest
{
    public string Decision { get; set; } = string.Empty;
    public string? Comment { get; set; }
}