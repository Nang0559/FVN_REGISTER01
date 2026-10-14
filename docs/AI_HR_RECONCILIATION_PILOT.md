# AI Pilot — HR feedback evidence analysis and reconciliation assistant

## Goal

## Related HR knowledge assistant

General questions about approved HR procedures (for example, how to register OT) belong to the separate [HR Policy Assistant](AI_HR_POLICY_ASSISTANT.md). The reconciliation assistant in this document handles an individual feedback case and compares its evidence with authorized official records. Do not mix personal case attachments into the shared policy knowledge base.

Turn a user's HR feedback (for example, "the OT hours on my payslip/calendar are wrong") and attached evidence into a traceable case summary and a suggested HR response. The assistant is decision support; it does not approve requests, change attendance, change payroll, or write to operational tables.

## Current code increment — implemented vertical slice

The branch now contains the first end-to-end HR Case Assistant workflow:

- FVN_REGISTER.AI.Application contracts define OT, attendance, leave facts, analysis result, case inbox and review.
- FVN_REGISTER.API/Controllers/AIHRCaseController.cs is the authenticated gateway. It uses the current JWT identity, checks function permission and employee managed scope, then obtains official data via IReportDispatcher.
- OT facts are built from OTDetail, OTSummaryByEmployee and AttendanceDetail. Attendance facts use AttendanceDetail. Leave facts use LeaveDetail.
- Report OT now includes ActualRecordCount so an absent actual value is not mistaken for zero.
- HRReconciliationService performs deterministic comparisons for reported/planned/actual/attendance OT, attendance intervals, leave type/days and evidence employee/date identity.
- Evidence is accepted only from the same tenant and signed uploader after a clean malware scan and completed extraction. Unique candidates can populate hours, leave days/type, attendance times, employee code and work date; ambiguous values are not guessed.
- PDF text extraction via PdfPig preserves page markers. TXT/CSV and basic XLSX extraction remain supported. Scanned PDF OCR and DOCX are not implemented.
- EvidenceCandidateJson is persisted with the HR case and shown in the Blazor review page with page/line provenance.
- Optional Ollama provider drafts the Vietnamese reply from the official facts and deterministic comparisons. It cannot modify the comparison outcome. When Ollama is unavailable, a safe deterministic template is used.
- HR case inbox/detail/review APIs and Blazor route /ai/hr-assistant support case creation, evidence upload/status, comparisons, draft editing, approve/edit/reject.
- SQL stores ai.AiHRCases and immutable ai.AiHRCaseReviews. RowVersion protects concurrent review.
- FVN API performs authorization before each detail/review; AI API reviewer authorization is enabled only when TrustFvnRegisterGatewayForHRReview is explicitly true behind the private gateway.
- Unit tests cover OT discrepancy, evidence ownership/extraction, evidence employee/date mismatch, authorized facts and review audit/authorization. Ollama provider has a mocked HTTP contract test.

**Deployment gates:** apply migrations 001–004 on FVN_AI; configure API key/HMAC secrets identically in FVN API and AI API; restrict AI API to the private gateway; configure ClamAV; configure Ollama if an LLM draft is required; validate report fields and function codes against the FCC production schema. No build/test success is claimed until the solution is run in the target environment.

## Existing FVN_REGISTER sources identified

The pilot must reuse existing application services and authorization rather than introduce a second source of truth:

- F03HrmAttendanceCalculated is the attendance facts source exposed through AttendanceDetail: check-in/out, shift, total hours, OT hours and holiday context. F03AttendanceStaging is an upstream/staging source and is not queried directly by the AI service.
- F03LeaveDays contains underlying leave fields; the current gateway reads the authorized LeaveDetail report (VF03LeaveRequest) for StartDate, EndDate, TotalDay, LeaveTypeCode and request status.
- OTDetail exposes OT request identifiers, date, planned total hours and status from VF03OTRequest.
- OTSummaryByEmployee aggregates planned and actual values from VF03OTRequestDetail; ActualRecordCount identifies whether any actual value is present.
- ReportController already exposes authenticated report operations through IReportDispatcher, including report lookup and export, with managed-scope checks for employee/department requests.

## Case workflow

1. Open case — user submits feedback text, employee/date context and supporting PDF/XLSX/CSV/TXT evidence.
2. Ingest — AI platform validates file type/content, scans with configured ClamAV, stores original and queues a job.
3. Extract — Worker creates candidate fields and evidence references. Scanned PDF/OCR is a later provider gate; unsupported input stays unprocessed rather than being guessed.
4. Load official facts — trusted FVN_REGISTER API checks caller identity and RBAC, then obtains only the employee/date/report facts the caller is allowed to see. The AI service never connects to the operational DbContext.
5. Reconcile deterministically — compare employee code, work date, request state, planned/actual times, hours, shift and holiday context using versioned rules. A mismatch is a fact to explain, not a value to auto-correct.
6. Compose answer — deterministic rules produce comparisons and internal notes; the model receives only authorized facts and those comparisons to draft a Vietnamese reply. The HR workspace displays the extracted evidence candidate with page/line references and the official report reference.
7. Human review — HR accepts, edits or rejects the proposed response. Feedback is persisted for evaluation.
8. Business action — if HR chooses a correction, the trusted FVN_REGISTER application re-runs authorization and domain validation and uses its existing command/workflow. AI does not write to business tables.

## Initial pilot scope

### A. Overtime reconciliation
- Compare reported/evidence hours with OTDetail planned hours, OTSummaryByEmployee actual hours and AttendanceDetail OT hours.
- Compare actual OT with attendance OT; preserve the distinction between missing actual and a real zero.
- Flag missing values, duration mismatch, evidence employee/date mismatch and request status requiring HR review.
- OT planned start/end and detailed employee-level actual intervals are not yet compared by this first vertical slice.

### B. Attendance / leave feedback
- Compare attendance check-in/out with AttendanceDetail facts; two unique evidence time candidates can be used as the reported interval.
- Compare leave evidence day/type candidates with LeaveDetail request facts when the source exposes the values.
- Identify missing registration and evidence employee/date mismatch.
- Leave balance/payroll consequences and multi-day per-date allocation remain NeedsHumanDecision until the authoritative rule/source is added.

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