# Approval + email API/SQL E2E runner

This runner reads `Tests/fixtures/approval-email-e2e-cases.json` and accepts a case selector. It exercises the real local API and a dedicated SQL Server test database for one leave request:
login requester and two approvers -> create request -> check approval inbox -> approve level 1 -> approve level 2 -> query final result -> verify SQL snapshot/history/email queue.

## Safety requirements

- Use a **disposable test database**. The script refuses database names that do not contain `Test`, `QA`, `E2E`, or `Automation`.
- The API URL must be localhost for this first version. The runner also refuses to start if an active/default email profile or profile referenced by an active dispatch policy points to a non-local SMTP host, or uses a port other than the capture port (default 1025).
- Set `E2E_ALLOW_RUN=YES` only after checking the connection string and current environment.
- Configure the application's active email profile/policy to point to a local SMTP capture tool such as Mailpit or smtp4dev. Do not point it at a real SMTP server. The runner verifies queued email rows; it does not send test mail itself.
- Use test accounts with LeaveCreate/LeaveApprove permissions and two configured approval levels. The request's approval selections use the real employee codes resolved from the test database.
- The configured leave type must be active and suitable for a one-day test leave. The requester needs any required entitlement/balance.
- The deployed schema must include `F03LeaveDays`, `F03ApprovalSnapshots`, and `F03EmailQueues`. If the approval-history table name differs, the script reports that assertion as blocked rather than pretending it passed.

## Scenario selection

Run one case per invocation so each run has an independent marker and report:

```powershell
.\scripts\Test-ApprovalEmailE2E.ps1 -CaseId FLOW-APPROVE-2-LEVEL
.\scripts\Test-ApprovalEmailE2E.ps1 -CaseId FLOW-REJECT-L1
.\scripts\Test-ApprovalEmailE2E.ps1 -CaseId FLOW-DENY-WRONG-APPROVER
.\scripts\Test-ApprovalEmailE2E.ps1 -CaseId FLOW-DUPLICATE-SUBMIT
```

The wrong-approver case additionally requires `E2E_PEER_USERNAME` and `E2E_PEER_PASSWORD` for an ordinary peer who is not an approver on the request. Set them to test-only credentials before selecting that case. The duplicate-submit case deliberately fails if two request rows are persisted for the same run marker. The mail-retry case currently returns **BLOCKED** because the application does not yet expose a deterministic injectable mail-transport fault; do not simulate this by interrupting a shared SMTP service. A fake transport seam must be added before that scenario can be automated.

## Environment variables (PowerShell)

```powershell
$env:E2E_API_BASE_URL = "http://localhost:5000"
$env:E2E_SQL_CONNECTION_STRING = "Server=localhost;Database=FVN_E2E_Test;Integrated Security=True;TrustServerCertificate=True"
$env:E2E_ALLOW_RUN = "YES"
$env:E2E_FAKE_SMTP_PORT = "1025" # Mailpit/smtp4dev capture port

$env:E2E_LEAVE_TYPE_CODE = "ANNUAL" # replace with an active code from the test DB
$env:E2E_REQUESTER_USERNAME = "..." # test-only account
$env:E2E_REQUESTER_PASSWORD = "..."
$env:E2E_REQUESTER_EMPLOYEE_CODE = "..."
$env:E2E_REQUESTER_EMAIL = "..."

$env:E2E_APPROVER1_USERNAME = "..."
$env:E2E_APPROVER1_PASSWORD = "..."
$env:E2E_APPROVER1_EMPLOYEE_CODE = "..."
$env:E2E_APPROVER1_EMAIL = "..."

$env:E2E_APPROVER2_USERNAME = "..."
$env:E2E_APPROVER2_PASSWORD = "..."
$env:E2E_APPROVER2_EMPLOYEE_CODE = "..."

# Only for FLOW-DENY-WRONG-APPROVER
$env:E2E_PEER_USERNAME = "..."
$env:E2E_PEER_PASSWORD = "..."
```

Run from repository root in Windows PowerShell:

```powershell
.\scripts\Test-ApprovalEmailE2E.ps1
```

The script writes a JSON evidence report under `TestResults/` with run ID, case ID, request ID, each assertion and overall status. A run with blocked assertions is not reported as a full PASS. It does not clean up automatically because preserving evidence is useful; delete only records tagged with the run ID after reviewing the report.

## Assertions covered

1. All three users can authenticate.
2. Leave registration returns success and persists a uniquely tagged row in `F03LeaveDays`.
3. Approval snapshot is persisted.
4. Level 2 does not see the request before level 1 acts; exact request IDs (not substring matches) are checked. Level 1 sees and approves; level 2 then sees and approves.
5. The requester can query the final result.
6. SQL approval history has exactly two decisions where the mapped history table is present; a missing table is recorded as BLOCKED and later assertions still run.
7. The email queue has exactly one correlated submission notification to the first approver and one final-result notification to the requester; correlation uses request-ID token boundaries to avoid matching ID 12 to ID 112.

## Current limitations

- This is an executable first slice for the leave-request workflow, not yet the entire app's E2E suite.
- It relies on preconfigured test users, approval policy/positions, leave balance/type, templates, dispatch policies, and local SMTP capture.
- Run it against the deployed target branch and review every SQL assertion. A successful build alone is not evidence that this workflow passes.
- If the API's response envelope or deployed SQL table names differ, adapt the assertions to the actual contract/schema instead of weakening them to unconditional PASS.
