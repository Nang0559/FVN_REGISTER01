# Approval + email API/SQL E2E runner

This runner exercises the real local API and a dedicated SQL Server test database for one leave request:
login requester and two approvers -> create request -> check approval inbox -> approve level 1 -> approve level 2 -> query final result -> verify SQL snapshot/history/email queue.

## Safety requirements

- Use a **disposable test database**. The script refuses database names that do not contain `Test`, `QA`, `E2E`, or `Automation`.
- The API URL must be localhost for this first version. The runner also refuses to start if an active/default email profile or profile referenced by an active dispatch policy points to a non-local SMTP host, or uses a port other than the capture port (default 1025).
- Set `E2E_ALLOW_RUN=YES` only after checking the connection string and current environment.
- Configure the application's active email profile/policy to point to a local SMTP capture tool such as Mailpit or smtp4dev. Do not point it at a real SMTP server. The runner verifies queued email rows; it does not send test mail itself.
- Use test accounts with LeaveCreate/LeaveApprove permissions and two configured approval levels. The request's approval selections use the real employee codes resolved from the test database.
- The configured leave type must be active and suitable for a one-day test leave. The requester needs any required entitlement/balance.
- The deployed schema must include `F03LeaveDays`, `F03ApprovalSnapshots`, and `F03EmailQueues`. If the approval-history table name differs, the script reports that assertion as blocked rather than pretending it passed.

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
```

Run from repository root in Windows PowerShell:

```powershell
.\scripts\Test-ApprovalEmailE2E.ps1
```

The script writes a JSON evidence report under `TestResults/` with run ID, request ID, each assertion and overall status. It does not clean up automatically because preserving evidence is useful; delete only records tagged with the run ID after reviewing the report.

## Assertions covered

1. All three users can authenticate.
2. Leave registration returns success and persists a uniquely tagged row in `F03LeaveDays`.
3. Approval snapshot is persisted.
4. Level 1 sees the request; level 1 approves; level 2 then sees the request and approves.
5. The requester can query the final result.
6. SQL approval history has two decisions where the expected history table is present.
7. The email queue contains notifications for the first approver and requester tagged to the run.

## Current limitations

- This is an executable first slice for the leave-request workflow, not yet the entire app's E2E suite.
- It relies on preconfigured test users, approval policy/positions, leave balance/type, templates, dispatch policies, and local SMTP capture.
- Run it against the deployed target branch and review every SQL assertion. A successful build alone is not evidence that this workflow passes.
- If the API's response envelope or deployed SQL table names differ, adapt the assertions to the actual contract/schema instead of weakening them to unconditional PASS.
