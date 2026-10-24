# Test traceability matrix

Branch: `test/full-application-regression-from-182ca84`

This matrix records **code-level coverage only** until a run has a TRX/E2E report. No row is marked runtime PASS merely because its test file exists.

Status:
- **PARTIAL** — related test exists but does not cover the full scenario.
- **GAP** — no matching automated test has been verified on this branch.
- **BLOCKED** — requires a configured runtime or external fixture.
- **IMPLEMENTED / NOT RUN** — test source exists; runtime result is not yet recorded.

| Scenario IDs | Test / evidence location | Current status | Missing assertions / execution evidence |
|---|---|---|---|
| AUTH-001, AUTH-002, AUTH-003, AUTH-007 | `Tests/FVN_REGISTER.Infrastructure.Tests` (review auth/security tests); `SecurityFunctionCapabilityMatrixTests.cs`; `FeatureOperatorAuthorizationPolicyTests.cs` | PARTIAL | No verified API login/token expiry/secret-leak E2E coverage; run against API and record evidence |
| AUTH-004, AUTH-005, AUTH-006 | Security policy tests + API E2E | GAP | Direct API denial, cross-department isolation, logout/revocation/refresh behavior |
| APR-001–APR-008 | `Tests/fixtures/approval-email-e2e-cases.json`; `scripts/Test-ApprovalEmailE2E.ps1` | PARTIAL / BLOCKED | Fixture includes 5 cases, but runner currently requires runtime schema/contract verification; no verified ApprovalEngine unit suite |
| LEAVE-001 | `scripts/Test-ApprovalEmailE2E.ps1` | PARTIAL / BLOCKED | Happy-path only; requires disposable SQL DB, active leave type/balance, API, and local SMTP capture |
| LEAVE-002–LEAVE-006 | Future unit/API/SQL tests | GAP | Invalid balance/date/type, overlap, cancellation/balance idempotency, isolation, calendar/dashboard projections |
| OT-001, OT-004–OT-008 | `OTActualOnlyStateMachineTests.cs`; `OTDuplicateRegistrationMessageTests.cs`; `PayrollGateEndToEndTests.cs` | PARTIAL | These are not proof of all limits, transitions, Excel correctness, or live HRM parity |
| OT-002, OT-003 | Future boundary/unit tests | GAP | Below/equal/above limit; overnight, zero/negative duration, duplicate interval, invalid minutes |
| TRIP-001–TRIP-005 | Future API/SQL tests | GAP | Create/validate/transition/participants/projections/notification |
| EQ-001–EQ-007 | Future API/SQL/import tests | GAP | Equipment CRUD, duplicate checks, spreadsheet validation, atomicity, permissions |
| HRM-001, HRM-003–HRM-008 | `HrmAttendanceBackfillIntegrationTests.cs`; `PayrollGateEndToEndTests.cs` | PARTIAL / BLOCKED | SQL fixture and approved HRM comparison dataset required; run HRM boundary audit separately |
| HRM-002 | SQL command audit / integration evidence | GAP | Explicitly prove no writes to HRM-owned tables during sync/recalculation |
| CAL-001–CAL-003 | Existing calendar/action test inventory | PARTIAL | Map exact scenario IDs to specific test methods after inventory; date-boundary and scope execution evidence |
| ACT-001–ACT-005 | `ExecutionLifecycleIntegrationTests.cs`; `ExecutionReviewPolicyTests.cs` | PARTIAL | SQL-backed execution and per-participant isolation evidence required |
| NOT-001–NOT-005, JOB-001–JOB-002 | `NotificationDeduplicationPolicyTests.cs` | PARTIAL | Outbox/transport retry, unread counts, SignalR reconnect, worker restart/concurrency |
| RPT-001–RPT-004, ADM-001–ADM-002, PUB-001–PUB-003 | Future API/SQL/UI tests | GAP | Authorized scope, export leakage, audit completeness, form targeting and duplicate submission |
| AI-001–AI-008 | Planned guide/document fixtures; no GuideScenarioTests source verified on this branch | GAP | Add deterministic fake corpus tests, document-level authorization and prompt-injection cases |
| BUILD / test projects | `scripts/Test-BackendRegression.ps1` | IMPLEMENTED / NOT RUN | Runner writes TRX and summary JSON; execute on Windows with .NET 9 and record the report |
| Full registration → approval → email → requester result | `Tests/APPROVAL_EMAIL_E2E.md`; `scripts/Test-ApprovalEmailE2E.ps1` | BLOCKED | Not executed here; validate route/DTO/schema assumptions and attach TestResults JSON + SMTP capture evidence |

## Run evidence log

Append one row per actual run. Do not paste secrets, tokens, connection strings, or personal email addresses.

| UTC/local timestamp | Commit SHA | Environment | Command / run ID | Passed | Failed | Skipped | Status | Evidence path / URL |
|---|---|---|---|---:|---:|---:|---|---|
| Not run in this change session | — | No connected runtime/SQL/SMTP | — | — | — | — | BLOCKED | Awaiting execution on disposable test environment |

## Completion rule

A scenario moves to runtime PASS only after the matching test was executed against the intended build, all required assertions passed, skipped tests are disclosed, and its evidence is linked here. The full application is not considered covered while any required scenario remains GAP, PARTIAL, or BLOCKED.
