namespace FVN_REGISTER.AI.Application.Contracts;

public enum HRCaseType { Overtime, Attendance, Leave }
public enum HRComparisonOutcome { Matched, Discrepancy, InsufficientEvidence, NeedsHumanDecision }

/// <summary>Only case facts supplied by a trusted FVN_REGISTER backend are authoritative.</summary>
public sealed record HRCaseAnalysisRequest(
    HRCaseType CaseType,
    string? EmployeeCode,
    DateOnly WorkDate,
    string UserStatement,
    string? EvidenceText,
    string? EvidenceReference,
    decimal? ReportedHours = null,
    DateTimeOffset? ReportedStart = null,
    DateTimeOffset? ReportedEnd = null,
    decimal? ReportedLeaveDays = null,
    string? ReportedLeaveType = null,
    Guid? EvidenceDocumentId = null);

public sealed record OfficialHRFacts(
    string EmployeeCode,
    DateOnly WorkDate,
    string? RequestStatus,
    DateTimeOffset? PlannedStart,
    DateTimeOffset? PlannedEnd,
    decimal? PlannedHours,
    DateTimeOffset? ActualStart,
    DateTimeOffset? ActualEnd,
    decimal? ActualHours,
    decimal? AttendanceOvertimeHours,
    string? ShiftCode,
    bool? IsHoliday,
    string? LeaveType,
    decimal? LeaveDays,
    string SourceSystem,
    string SourceRecordReference,
    DateTimeOffset RetrievedAtUtc);

public sealed record HRFieldComparison(
    string Field,
    string? UserReported,
    string? OfficialValue,
    string RuleCode,
    HRComparisonOutcome Outcome,
    string Explanation);

public sealed record HRReconciliationAnalysis(
    Guid CaseId,
    HRCaseType CaseType,
    string EmployeeCode,
    DateOnly WorkDate,
    HRComparisonOutcome Outcome,
    IReadOnlyList<HRFieldComparison> Comparisons,
    string SuggestedReply,
    string InternalHRNote,
    IReadOnlyList<string> EvidenceReferences,
    string OfficialSource,
    string OfficialRecordReference,
    DateTimeOffset FactsRetrievedAtUtc,
    bool RequiresHumanReview);

public interface IAuthorizedHRFactsProvider
{
    /// <summary>Must re-check the signed actor's FVN_REGISTER permission and managed scope for this employee/date.</summary>
    Task<OfficialHRFacts?> GetFactsAsync(string tenantCode, string actorId, HRCaseType caseType,
        string? requestedEmployeeCode, DateOnly workDate, CancellationToken cancellationToken = default);
}

public interface IHRReconciliationService
{
    Task<HRReconciliationAnalysis> AnalyzeAsync(string tenantCode, string actorId,
        HRCaseAnalysisRequest request, CancellationToken cancellationToken = default);
}

public sealed record HRCaseReviewRequest(string Decision, string? FinalReply, string? Comment);
public sealed record HRCaseReviewReceipt(Guid CaseId, string Status, string Decision, DateTimeOffset ReviewedAtUtc);

public interface IHRCaseReviewerAuthorization
{
    Task<bool> CanReviewCaseAsync(string tenantCode, string actorId, string employeeCode, HRCaseType caseType, CancellationToken cancellationToken = default);
}

public interface IHRCaseReviewService
{
    Task<HRCaseReviewReceipt> ReviewAsync(string tenantCode, string actorId, Guid caseId,
        HRCaseReviewRequest request, CancellationToken cancellationToken = default);
}

public sealed record HRCaseInboxItem(Guid CaseId, string EmployeeCode, string CaseType, DateOnly WorkDate,
    string Outcome, string Status, string UserStatement, string SuggestedReply, string InternalHRNote,
    string AnalysisJson, string? EvidenceReference, string OfficialSource, string OfficialRecordReference,
    DateTimeOffset CreatedAtUtc, string CreatedBy);

public interface IHRCaseQueryService
{
    Task<IReadOnlyList<HRCaseInboxItem>> GetPendingAsync(string tenantCode, string actorId, int limit,
        CancellationToken cancellationToken = default);
    Task<HRCaseInboxItem?> GetAsync(string tenantCode, string actorId, Guid caseId,
        CancellationToken cancellationToken = default);
}
