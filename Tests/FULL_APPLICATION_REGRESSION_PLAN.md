# Full Application Regression Test Plan

> Baseline: `182ca845f80e901152533366c838f0b9039521a5`
> Working branch: `test/full-application-regression-from-182ca84`
> Status: test plan and reusable scenario catalogue; execution evidence must be recorded per environment.
>
> This file does not claim that unexecuted tests pass. A feature is PASS only when its assertions have been run against the target build and test database.

## 1. Test gates

1. **Gate A — Restore/build:** restore the solution and build each backend project and test project. Build UI/MAUI projects separately if the installed workload is missing.
2. **Gate B — Static/model integrity:** verify EF entity mappings, required DbSets/configurations, SQL objects, foreign keys, nullability, indexes, and seed prerequisites.
3. **Gate C — Unit tests:** validation, calculations, state transitions, permission decisions, routing, and projection rules without a live database.
4. **Gate D — Integration tests:** API + SQL Server test database, transaction cleanup, EF queries, stored procedures, and background job boundaries.
5. **Gate E — End-to-end regression:** role-specific user journeys from UI/API through persistence and notification.
6. **Gate F — Security/data integrity:** negative authorization tests, cross-department isolation, duplicate submissions, concurrency, and audit trail.
7. **Gate G — HRM boundary:** verify HRM source tables are read-only; FVN calculations and reconciliation use FVN-owned tables only.

Never point destructive integration tests at production. Use a dedicated test database and explicit test connection string. Secrets must come from environment variables/User Secrets, never committed settings.

## 2. Shared deterministic test data

Use an isolated test database and unique test-run prefix (for example `TST_<run-id>`) for newly created records. Reuse existing seed accounts only in a disposable database. The historical seed may include E0001–E0005 with password Test@123; confirm the seed and login implementation at the target commit before relying on those credentials.

### Organizational fixtures

| Fixture | Value | Purpose |
|---|---|---|
| Departments | IT (57), General Affairs (13), Finance (14), Production Control (12), Quality Control (10) | Department filters and isolation |
| Approval positions | Sub-Leader, Chief, Manager, Director, Employee/Staff | Multi-level approval and non-approver checks |
| Employees | E0001–E0005 (only if present in the isolated seed) | Distinct requester/approver/peer roles |
| Roles | Employee, Sub Leader, Chief, Manager, GM | Positive and negative RBAC |
| Dates | Fixed dates injected by test clock; include weekday, Sunday, company holiday, national holiday, month-end and payroll-period boundary | Avoid time-dependent flaky tests |
| Test records | Prefix all external IDs/comments with the test run ID | Cleanup and test traceability |

Do not assume numeric database IDs are stable across fresh databases. Resolve records by unique business keys and capture generated IDs at runtime.

## 3. Business scenario catalogue

Each scenario must assert HTTP/result status, persisted state, audit history, side effects, and data isolation where applicable.

### AUTH / SESSION / RBAC

- AUTH-001: valid credentials return a usable token and expected identity claims.
- AUTH-002: wrong password, unknown account, inactive account, malformed request, and empty fields are rejected.
- AUTH-003: expired/invalid token is rejected; protected endpoints do not accept anonymous access.
- AUTH-004: employee cannot access manager/admin endpoints by changing UI routes or calling API directly.
- AUTH-005: employee from Department A cannot read or modify Department B data without an explicit capability.
- AUTH-006: logout/revocation/session expiry behaves consistently; refresh token rotation/rejection is verified if enabled.
- AUTH-007: secrets and token material are absent from API responses and logs.

### APPROVAL

- APR-001: policy matches request type + required department, with optional position specificity as defined by the contract.
- APR-002: inactive policy/position and missing required approver are rejected or safely reported.
- APR-003: create request snapshots the intended route; later policy edits do not silently rewrite an in-flight route unless explicitly designed.
- APR-004: each approval level can act only when it is the current actionable level.
- APR-005: approve advances the route; final approval changes the business state once.
- APR-006: reject/cancel paths record actor, time, reason, and final state.
- APR-007: duplicate clicks/retries do not duplicate approval history or advance multiple levels.
- APR-008: approver is not eligible to approve their own request unless an explicit policy permits it.

### LEAVE

- LEAVE-001: valid leave request within balance is submitted and routed for approval.
- LEAVE-002: request exceeding balance, invalid date range, invalid type, or missing required data is rejected.
- LEAVE-003: overlapping requests and duplicate submission are detected.
- LEAVE-004: approval/rejection/cancellation updates balance exactly once according to the agreed balance policy.
- LEAVE-005: cross-employee and cross-department access is denied.
- LEAVE-006: calendar and dashboard reflect the request state without becoming the source of truth.

### OVERTIME

- OT-001: valid OT request validates date, time range, employee, and configured limits.
- OT-002: employee/department/block limits are tested at below, exactly-at, and above-limit boundaries.
- OT-003: overnight shifts, zero/negative duration, duplicate intervals, and invalid minute values are covered.
- OT-004: approval/rejection/cancellation and participant-level isolation are verified.
- OT-005: approved OT is treated as planned work; actual OT comes from the official FVN attendance read model.
- OT-006: actual OT without an approved request creates the expected reconciliation/confirmation action.
- OT-007: recalculation is idempotent and does not duplicate actual rows, reconciliation, actions, or notifications.
- OT-008: exported Excel uses the correct template, employee ordering, date range, and HRM-compatible symbols.

### TRIP

- TRIP-001: create and submit a trip with valid participants, dates, destination, and purpose.
- TRIP-002: invalid dates, empty participants, duplicate participants, and unauthorized participant changes are rejected.
- TRIP-003: Pending → Approved/Rejected/Cancelled transitions preserve history and actor identity.
- TRIP-004: multi-participant trip data and actual/reconciliation are isolated per employee.
- TRIP-005: calendar, action list, dashboard, and notifications agree with the persisted business state.

### EQUIPMENT / IMPORT

- EQ-001: create/update equipment with valid identifiers and required fields.
- EQ-002: duplicate serial/asset code and invalid owner/user reference are rejected.
- EQ-003: import preview reports row number, validation errors, duplicates, and accepted rows before persistence.
- EQ-004: mixed-validity spreadsheet does not partially commit unless partial-import behavior is explicitly specified.
- EQ-005: missing optional UserId can be completed later if the import contract permits it; invalid supplied UserId is rejected.
- EQ-006: re-uploading the same file is idempotent or clearly reports duplicates.
- EQ-007: unauthorized user cannot import, assign, or export equipment outside their scope.

### HRM SYNC / ATTENDANCE

- HRM-001: employee/department/position sync handles new, changed, inactive, and missing HRM rows.
- HRM-002: HRM source tables are never mutated by FVN; assert no INSERT/UPDATE/DELETE/MERGE is issued against HRM-owned objects.
- HRM-003: missing gate events, missing shift, overnight shift, holidays, Sunday, and invalid event order have explicit expected outcomes.
- HRM-004: test employee + work date matching and duplicate event handling.
- HRM-005: compare output by EmployeeCode + WorkDate against an approved HRM reference workbook/result set.
- HRM-006: payroll period boundary (21st–20th), month-end, and year-end are tested.
- HRM-007: recalculation can be repeated without duplicate or drift in persisted calculations.
- HRM-008: unknown/missing HRM references are reported without corrupting valid rows.

### WORK CALENDAR / ACTION / RECONCILIATION

- CAL-001: calendar aggregates relevant events for the selected employee, date range, and permissions.
- CAL-002: availability respects policy, holiday, shift, existing requests, and date boundaries.
- CAL-003: future dates without actual attendance are treated as expected, not as missing actual.
- ACT-001: one logical action is created per reconciliation/action owner key; retries update rather than duplicate.
- ACT-002: only authorized assignee/reviewer can submit confirmation or evidence.
- ACT-003: rejected/missing evidence cannot automatically resolve a reconciliation.
- ACT-004: when planned and actual match, reconciliation resolves according to policy; mismatches require the configured confirmation.
- ACT-005: participant A's changes do not alter participant B/C states.

### NOTIFICATION / EMAIL / BACKGROUND JOBS

- NOT-001: business transition enqueues the intended notification exactly once.
- NOT-002: unread/read state is per recipient and badge/count agrees with unread items.
- NOT-003: retryable delivery failure is retried with bounded attempts; poison messages are observable.
- NOT-004: email-disabled/missing configuration does not break the primary business transaction.
- NOT-005: SignalR delivery is a best-effort transport; persisted notification remains retrievable after reconnect.
- JOB-001: recurring worker can restart and resume without duplicate side effects.
- JOB-002: cancellation, timeout, transient SQL failure, and concurrent execution are handled.

### AI ASSISTANT / GUIDE / DOCUMENT ACCESS

- AI-001: guide/help question returns an answer grounded in the allowed guide corpus and identifies uncertainty when no source supports an answer.
- AI-002: user can retrieve only documents permitted by their role, department, and document-level policy; guessed file IDs do not bypass authorization.
- AI-003: download/open endpoints re-check authorization server-side rather than trusting UI visibility.
- AI-004: unsupported, missing, stale, or conflicting guide content is reported as such; the assistant does not fabricate policy details.
- AI-005: prompt injection embedded in uploaded/connected documents is treated as untrusted content and cannot override system policy or disclose other users' documents.
- AI-006: upload validates file type/size, rejects unsafe or unsupported files, and records owner, access scope, and audit metadata.
- AI-007: retrieval, citation/source attribution, and answer output are tested with deterministic fake document fixtures; tests do not require a live LLM unless explicitly marked as integration tests.
- AI-008: errors/timeouts from embedding, retrieval, or model providers fail safely and do not leak secrets, internal prompts, or unauthorized document excerpts.

### DASHBOARD / REPORTS / ADMIN / PUBLIC FORMS

- RPT-001: dashboard totals match a direct query over the same authorized scope.
- RPT-002: filters, pagination, sorting, empty result, and date boundary are correct.
- RPT-003: export permission is tested separately from view permission.
- RPT-004: exported data respects row-level scope and does not leak hidden columns.
- ADM-001: permission changes take effect at the documented token/session boundary.
- ADM-002: audit records include actor, operation, entity key, and timestamp for sensitive changes.
- PUB-001: public registration form is visible only to selected departments/users.
- PUB-002: unauthorized user cannot read or submit a targeted form by guessing its identifier.
- PUB-003: required fields, duplicate submissions, close date, and export are validated.

## 4. Cross-cutting assertions

For every write workflow, verify:
- the response contract and validation error shape;
- persisted business state and foreign keys;
- exactly-once or idempotent behavior on retry;
- audit/history actor and timestamp;
- notification/action/calendar projections where relevant;
- unauthorized and cross-scope attempts fail without side effects;
- no sensitive data is returned or logged;
- rollback leaves no partial state after a failed transaction.

## 5. Test evidence format

Record each run with: test ID, commit SHA, environment, database version, command/API request, fixture keys, expected result, actual result, PASS/FAIL/BLOCKED, failure evidence, and cleanup status. A scenario is BLOCKED if required DB/HRM fixture or runtime configuration is unavailable; do not mark it PASS based on code inspection.

## 6. Suggested execution commands

From repository root (Windows PowerShell or terminal with .NET 9 SDK):

```powershell
dotnet restore .\FVN_REGISTER.sln
dotnet build .\FVN_REGISTER.Core\FVN_REGISTER.Core.csproj --no-restore
dotnet build .\FVN_REGISTER.Contract\FVN_REGISTER.Contract.csproj --no-restore
dotnet build .\FVN_REGISTER.Application\FVN_REGISTER.Application.csproj --no-restore
dotnet build .\FVN_REGISTER.Infrastructure\FVN_REGISTER.Infrastructure.csproj --no-restore
dotnet test .\Tests\FVN_REGISTER.Infrastructure.Tests\FVN_REGISTER.Infrastructure.Tests.csproj --no-restore
dotnet test .\FVN_REGISTER.sln --no-restore
```

Run each command and capture exit code/output. The solution test command may require platform-specific workloads for UI/MAUI projects; report those separately from backend test failures.

## 7. Test traceability and execution status

The canonical scenario-to-test matrix is maintained in [TEST_TRACEABILITY.md](TEST_TRACEABILITY.md). Each row records the expected automated test or manual evidence, execution state, and known blocker. Status meanings:

- **IMPLEMENTED**: an automated test exists; it still must run successfully in the target environment.
- **PARTIAL**: some related assertions exist, but the full scenario is not covered.
- **GAP**: no matching automated test has been verified in this branch.
- **BLOCKED**: test requires SQL/API/HRM/SMTP or another runtime not available to the current run.

Do not infer scenario coverage from a project build or from a test with a similar name. Update the traceability row with a commit SHA and TRX/E2E evidence after execution. The Test-BackendRegression.ps1 runner reports skipped tests as PASS_WITH_SKIPS, not as a full PASS.
