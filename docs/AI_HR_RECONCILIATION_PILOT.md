# AI Pilot — HR feedback evidence analysis and reconciliation assistant

## Goal

Turn a user's HR feedback (for example, "the OT hours on my payslip/calendar are wrong") and attached evidence into a traceable case summary and a suggested HR response. The assistant is decision support; it does not approve requests, change attendance, change payroll, or write to operational tables.

## Existing FVN_REGISTER sources identified

The pilot must reuse existing application services and authorization rather than introduce a second source of truth:

- F03AttendanceStaging: WorkDate, EmployeeCode, CheckIn/CheckOut text and timestamps, ShiftCode/Name/Abbr, OtHours, TotalHours, IsHoliday, HolidayType, ShiftType.
- F03LeaveDays: WorkYear, StartTime, EndTime, TotalDay, RegisterId, LeaveReason, LeaveTypeCode, TotalLeaveDay, inherited request status/audit fields.
- F03OTRequests: OTCode, OTDate, StartTime, EndTime, PlannedHours, TotalOTHours, OTTypeCode, OTReasonSummary, inherited request status/audit fields.
- F03OTEmployees: EmployeeCode, department, planned StartTime/EndTime, OTHours, ActualHours, ActualStartTime/ActualEndTime, ValidationStatus, ValidationMessage.
- ReportController already exposes authenticated report operations through IReportDispatcher, including report lookup and export, with managed-scope checks for employee/department requests.

## Case workflow

1. Open case — user submits feedback text, employee/date context and supporting PDF/XLSX/CSV/TXT evidence.
2. Ingest — AI platform validates file type/content, scans with configured ClamAV, stores original and queues a job.
3. Extract — Worker creates candidate fields and evidence references. Scanned PDF/OCR is a later provider gate; unsupported input stays unprocessed rather than being guessed.
4. Load official facts — trusted FVN_REGISTER API checks caller identity and RBAC, then obtains only the employee/date/report facts the caller is allowed to see. The AI service never connects to the operational DbContext.
5. Reconcile deterministically — compare employee code, work date, request state, planned/actual times, hours, shift and holiday context using versioned rules. A mismatch is a fact to explain, not a value to auto-correct.
6. Compose answer — model receives the minimum authorized facts plus evidence snippets and produces a structured explanation with citations to source file/page/row and official FVN_REGISTER record identifiers.
7. Human review — HR accepts, edits or rejects the proposed response. Feedback is persisted for evaluation.
8. Business action — if HR chooses a correction, the trusted FVN_REGISTER application re-runs authorization and domain validation and uses its existing command/workflow. AI does not write to business tables.

## Initial pilot scope

### A. Overtime reconciliation
- Compare OT request date and planned start/end/hours with F03OTEmployees actual start/end/hours.
- Compare the same employee/date against F03AttendanceStaging OT/total hours and shift/holiday context.
- Highlight missing actual time, mismatched duration, duplicate evidence, employee/date mismatch and request status.
- Explain the calculation and show the evidence used. Do not infer an approval from an uploaded image.

### B. Attendance / leave feedback
- Compare reported work date and check-in/check-out evidence with F03AttendanceStaging.
- Compare leave date range/type/total days with F03LeaveDays and its request status.
- Identify overlaps, missing registration, date-format ambiguity and discrepancies between user evidence and official facts.
- Keep leave-balance/payroll consequences as "needs HR verification" until the authoritative rule and source are confirmed.

## Structured result contract

Every analysis response should contain:

- caseId, tenantCode, employeeCode, period, caseType.
- factsFromUser: explicitly attributed user statements.
- factsFromEvidence: extracted values with file hash, page/row/line and confidence.
- officialFacts: facts returned by FVN_REGISTER with record IDs and source timestamp.
- comparisons: field, expected, observed, difference, ruleCode and outcome.
- assessment: Matched / Discrepancy / InsufficientEvidence / NeedsHumanDecision.
- suggestedReply: Vietnamese response for the user and optional HR internal note.
- limitations, reviewRequired, modelVersion, promptVersion.

Do not use a single opaque confidence score as a substitute for evidence provenance. Low-confidence or conflicting fields must remain unresolved.

## HR chatbot for reports

The report chatbot should be a natural-language front end to existing, authorized report capabilities—not a text-to-SQL tool.

1. Intent parser maps a question to an allowlisted ReportType (attendance, leave, OT, trip or equipment).
2. Parameter parser extracts date range, employee, department and grouping.
3. FVN_REGISTER validates the caller and managed scope before executing the existing report dispatcher.
4. AI explains the returned aggregate/detail rows, calculates simple comparisons and can suggest follow-up questions.
5. Every numeric answer includes period, department/employee scope, filters and a link/reference to the underlying report. If the question is ambiguous, ask one clarification rather than guessing.

Examples:
- Tháng này bộ phận PROD có bao nhiêu giờ OT?
- Top 10 nhân viên OT nhiều nhất trong quý này?
- Có trường hợp OT thực tế khác OT đăng ký không?
- Vì sao ngày công của tôi trong tháng 9 bị thiếu một ngày?

Never let the model generate arbitrary SQL or broaden a user's report scope. Reuse ReportController/IReportDispatcher authorization behavior or a dedicated typed query service behind the same authorization boundary.

## RAG after extraction/review is stable

- Only approved/reviewed policies, work instructions and HR reference documents may be indexed.
- Employee-specific evidence and case attachments are not globally searchable knowledge.
- Every chunk carries tenant, source document, review, classification and access scopes.
- Apply ACL filters before vector retrieval and before constructing the model prompt.
- Treat document text as untrusted input; defend against prompt injection and never execute instructions found in an attachment.
- Return citations to source documents and page/row locations.

## Pilot acceptance criteria

- One upload completes the full state path: Quarantined → Queued → Running → AwaitingHumanReview.
- Scanner Unknown/Infected never produces an extraction candidate.
- HR can view the original evidence and candidate, correct fields, approve/reject, and see the immutable original candidate in the review history.
- Every reconciliation conclusion is reproducible from explicit rules and source facts.
- The assistant cannot access an employee/department outside the caller's managed scope.
- Suggested reply is never sent automatically; HR must explicitly approve it.
- No AI process writes to FVN_REGISTER operational tables.
- Golden test set covers at least 30 anonymized OT/attendance/leave cases, including missing and conflicting evidence.