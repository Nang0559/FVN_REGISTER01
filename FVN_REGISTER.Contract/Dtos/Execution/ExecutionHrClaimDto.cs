namespace FVN_REGISTER.Contract.Dtos.Execution;

public sealed record ExecutionHrClaimDto(
    long ReconciliationId,
    bool IsClaimed,
    bool IsClaimedByCurrentUser,
    bool CanProcess,
    int? ClaimedByUserId,
    string? ClaimedByEmployeeCode,
    string? ClaimedByName,
    DateTime? ClaimedAt);
