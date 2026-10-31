# HRM User / Role Discovery

This workflow inspects existing FVN_REGISTER users, permission groups, HRM role rules, direct function grants, approver routing and the relevant database schema.

It does not create or modify employees, departments, users, roles, permissions, approval rows or business requests.

## Setup

1. Register the Develop machine as a self-hosted Windows x64 GitHub Actions runner.
2. Create GitHub environments named develop and, if needed, deploy.
3. Add environment secret FVN_SQL_CONNECTION_STRING to each environment. It must point to the intended database.
4. Use a dedicated SQL login with SELECT permissions on the inspected dbo.F03 tables and VIEW DEFINITION. Do not use db_owner or sysadmin.
5. Run Actions → Discover HRM Users and Roles → Run workflow and select the target environment.

The runner must reach SQL Server. Discovery runs on Windows PowerShell 5.1 (`powershell.exe`); the Tester workflow's static/regression jobs use PowerShell 7 (`pwsh`) and the .NET 9 SDK. The connection string is never printed.

## Artifacts

- role-discovery-report.md: summary and suggested follow-up queries.
- role-discovery.json: complete machine-readable discovery.
- active-users.csv: active users and HRM context.
- function-grants.csv: active direct function grants.
- representative-candidates.csv: one candidate per actual PermissionCode.
- business-role-candidates.csv: suggested self-service Leave/OT users and approvers inferred from active grants and routing rows.
- security-schema.csv: security-related table/column inventory.

## Interpretation

- PermissionCode / PermissionName are permission groups, not automatically the business roles Worker, Approver, HR or Admin.
- A worker candidate must be validated using module/action/scope grants, such as Leave/Create and Overtime/Create with Own scope.
- An approver candidate must be validated against active F03Approvers rows for request type, department and approval level.
- HRM role rules in F03HrmUserRoleRules and direct grants in F03UserFunctions must both be considered.
- Candidate accounts are proposals only. The workflow does not log in to the application and does not export passwords or password hashes.
- Application authorization may also be enforced in code. The next phase must inspect authorization handlers and test both UI visibility and server-side rejection.

## Safety

Run discovery first against a non-production/read-only target. The script only issues SELECT statements, but the SQL credential itself must also be restricted to read-only access. Do not seed or alter HRM-synchronized tables.


## Tester workflow (`.github/workflows/tester-develop.yml`)

Runs on the Develop self-hosted runner. Triggers: push to `test/role-discovery-actions` (paths under `test/`, `Tests/`, `scripts/Test-*.ps1`) and manual dispatch. It deliberately has **no** `pull_request` trigger because the repository is public and the runner is a real machine.

| Job | What it does | Needs |
|---|---|---|
| static-checks | Parses every `.ps1` and JSON fixture | nothing |
| backend-regression | `scripts/Test-BackendRegression.ps1` (10 builds + 4 test projects), TRX + summary artifact | `pwsh`, .NET 9; optional `FVN_REGISTER_SQL_CONNECTION` (used only if the DB name contains Test/QA/E2E/Automation) |
| role-matrix | Discovery, then `Test-RoleMatrix.ps1`; manual dispatch only | `FVN_SQL_CONNECTION_STRING` in the `develop` environment |

`Test-RoleMatrix.ps1` reads `role-discovery.json` (no database access) and checks: users without a permission group, approver rows without an active user, non-contiguous approval levels, required request types without a chain, Approve* grants for routed approvers, Leave/Overtime self-service users, ambiguous HRM rules. It also writes `e2e-fixture-proposal.json` (requester + approver L1 + L2 in one department) to seed `scripts/Test-ApprovalEmailE2E.ps1` variables. FAIL exits non-zero; WARN only fails with `-FailOnWarn`.
