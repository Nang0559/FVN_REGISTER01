# FVN_REGISTER SQL deployment

## Canonical deployment

`00_Deploy_All.sql` is the canonical full deployment chain. It owns the execution order for the repository SQL migrations.

For an existing installation, use the appropriate baseline runner only when its starting schema is already present:

- `00_Deploy_From16.sql` — continue from Equipment/SQL 16.
- `00_Deploy_From48.sql` — continue from SQL 48.
- `00_Deploy_Run.sql` — full runner using the configured local SQL root.

All runners keep the Endpoint Governance sequence consistent:

`53 -> 54 -> 54 -> 55 -> 55 -> 56 -> 57 -> 58 -> 59 -> 60`

`60_EndpointAgentDataHardening.sql` is therefore included in every supported deployment runner.

## Legacy FunctionKey patches — manual only

The following scripts are **not** part of the normal deployment chain:

- `06A_FunctionKeyCompatibility.sql`
- `06B_FunctionKeyBackfill.sql`

They exist only to repair databases that were previously initialized with an older `06_Seed.sql` implementation that did not populate/require `FunctionKey` consistently.

Run them manually, in order, only when the target database is an affected legacy installation:

`06A_FunctionKeyCompatibility.sql` -> `06B_FunctionKeyBackfill.sql`

Do **not** add them to the normal `00_Deploy_*.sql` chain. `06_Seed.sql` is intentionally excluded from normal deployment, so these compatibility patches must remain an explicit legacy-migration step.

## Canonical security / notification schemas

- `40_TwoFactorAuthentication.sql` is **active deployment SQL**, not legacy. `14A_SecurityTwoFactorColumns.sql` owns the canonical user 2FA columns, while `40_TwoFactorAuthentication.sql` also creates `F03TwoFactorChallenges`, which is consumed by the 2FA service. It is therefore included in `00_Deploy_All.sql`, `00_Deploy_Run.sql`, and `00_Deploy_From16.sql`.
- `64_WebPushSubscriptions.sql` is **active deployment SQL** because it owns `F03PushSubscriptions`, consumed by the Web Push notification service. It is included in all runners whose baseline reaches SQL 64, including `00_Deploy_From48.sql`.
- `38_FeatureOperatorAssignments.sql` and `38_RBAC_HR_IT_MANAGED_SCOPE.sql` are historical superseded migrations and have been moved to `SQL/Legacy/`. They are not part of any deployment runner.


## SSMS

Enable **Query -> SQLCMD Mode** before running any `00_Deploy_*.sql` file containing `:r` directives.

## Department identity (DepartmentCode = INT = HRM BPMa)

Every department-code column in FVN_REGISTER (`DeptCode`, `ParentDeptCode`, `SubDepartmentCode`, `ApproverDeptCode`, `ApproveForDeptCode`, `OperatingResponsibleDeptCode`, ...) is `int` and holds the HRM `tblBoPhan.BPMa` value.

- `05A_DepartmentCodeInt.sql` is the early DepartmentCode migration. It normalizes legacy codes (`FIN`=14, `HR`=13, `PROD`=12, `QA`=10, `IT`=57), converts the affected department-code columns to `int`, rebuilds dependent indexes/FKs, and ensures department 57 (IT) exists in `F03Departments`. It runs before any script that compares DeptCode with numeric HRM BPMa values. `71_DepartmentCodeInt.sql` is verification-only and runs at the end of every runner; it does not mutate data.
- `72_HrmDepartmentITSeed.sql` runs on the **HRM** database (not part of the runners) and adds `BPMa = 57 / IT`, the department that holds management/SuperAdmin users.
- `22_03A_DepartmentCodeCompatibility.sql` is now only a guard that fails the deployment if `usp_CalculateHrmAttendance` is not INT-canonical.
- Code that needs the IT department compares `DeptCode = 57`, never `N'IT'`.
