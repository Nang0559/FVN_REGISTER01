# HRM Attendance Pipeline Trace and Dashboard Integration Contract

Branch: `feature/i18n-vi-ja`

## Purpose

Document the existing production attendance path before extending the approver dashboard. The HRM-compatible attendance calculation is the business authority; dashboard work must consume its persisted result and must not reimplement attendance/OT rules.

## Confirmed data flow

1. **Shift/schedule master** — `dbo.usp_SyncHrmShiftMaster` (owned by `SQL/09_StoredProcedures.sql`) reads HRM `tblca`, `CC_LichTrinhCa`, `tblNhanVien`, and `CC_LichTrinhVaoRa` into local `F03Shifts`, `F03ShiftSchedules`, `F03ShiftScheduleDays`, and `F03EmployeeShiftSchedules`. HRM is read-only.
2. **Raw swipe + local staging projection** — `dbo.usp_SyncAttendanceStaging @WorkDate` (also in `SQL/09_StoredProcedures.sql`) derives candidate shifts from employee schedule assignment and schedule-day definitions, reads HRM card/device swipe records, resolves swipes against scan windows (including overnight shift windows), and writes matched shift/attendance rows to `F03AttendanceStaging`. It also refreshes `F03HrmShiftReference` from HRM `tblBaoCao` for comparison/regression purposes.
3. **Important separation** — `F03HrmShiftReference` is a reference copy of the HRM report's employee/date schedule, shift, check-in and check-out fields. The reviewed code does **not** show `usp_CalculateHrmAttendance` consuming `F03HrmShiftReference`; it is not safe to treat this table as the calculation engine's input or as the authoritative complete roster.
4. **Official calculation** — `dbo.usp_CalculateHrmAttendance` in `SQL/22_03_CalculateHrmAttendance.sql` orchestrates per-date transactions, takes a date-scoped SQL application lock, calls the existing `dbo.usp_HrmCompatibleTimeKeepingForStaff` for each eligible HRM employee, and replaces current-state rows in `F03HrmAttendanceCalculated`. The proven per-staff algorithm in `SQL/22_02_HrmCompatibleTimeKeepingForStaff.sql` must remain unchanged during dashboard work. OT actual persistence is coordinated in the same orchestration.
5. **Calculated result / calendar** — `F03HrmAttendanceCalculated` is current state keyed uniquely by `(HrmEmployeeId, WorkDate)`; it carries official times, minutes, leave/holiday fields and `AttendanceDisplayValue` / `OtDisplayValue`. `AttendanceCalendarModuleProvider` reads current calculated rows and falls back to `F03HrmAttendanceHistory` when no current row exists. The symbol value is produced by the existing calculation path; dashboard must display/reuse it rather than recalculate it.
6. **Scheduling** — `HrmAttendanceCalculationWorker` invokes `IHrmAttendanceCalculationService.CalculateAsync`, which executes `usp_CalculateHrmAttendance`. Dashboard HTTP requests must remain read-only and must not trigger company-wide calculations or a per-approver HRM sync.

## Safe change made

In `usp_SyncAttendanceStaging`, validation that active shift and schedule masters exist now runs **before** deleting the existing date's staging rows. Previously, a missing/unsynced master caused the procedure to throw only after deleting the last successful staging snapshot. This reorders preconditions only; it does not change shift resolution, attendance calculations, symbols, OT logic, or HRM writes.

## Dashboard integration guardrails

- Use the existing `F03HrmAttendanceCalculated` data and its persisted display fields for actual attendance and symbols.
- For expected headcount/roster, establish a separate, explicit roster source before labeling anyone absent. `F03HrmShiftReference` contains only rows present in HRM `tblBaoCao`, while staging writes matched swipe/shift results; neither should be assumed to contain every scheduled employee without verifying data contracts.
- Join the roster and actual data by normalized employee code and work date, with department scope derived from active approval policy and the requester's approval level/type. Do not widen approver scope to all departments.
- Distinguish missing attendance from not-yet-due, approved leave/trip, missing/stale source data, and overnight shifts. Do not infer absence solely because a calculated attendance row is absent.
- Keep current attendance calculation and its stored procedures unchanged unless a separate regression-tested business change is explicitly requested.

## Verification status / remaining work

This is a static source trace against the repository files. No live SQL Server/HRM database was available in this change, so runtime row counts and stored-procedure execution have not been verified. The dashboard feature itself still requires implementation after the roster completeness contract is confirmed against actual HRM data.
