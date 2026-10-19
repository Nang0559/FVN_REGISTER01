# HRM attendance calculation jobs and recovery

## Runtime flow

- The API enqueues calculation requests into `dbo.F03HrmAttendanceCalculationJob`; it does not keep a long SQL calculation attached to the HTTP request.
- `HrmAttendanceCalculationJobWorker` is registered with the Generic Host and executes one work date at a time. Each successful date is committed and progress is persisted before the worker considers the next date.
- At date boundaries, the worker yields to queued jobs of equal or higher priority. This prevents a long company-wide catch-up from starving a user's calendar refresh.
- The scheduler persists company-wide catch-up, yesterday's final calculation, and today's periodic refresh as jobs too.
- The calendar backfill coordinator checks successful coverage and locked payroll periods. It queues only missing open date ranges. If today's latest successful calculation is older than `Calendar:AttendanceBackfill:CurrentDayFreshnessMinutes` (default 15 minutes), it queues a high-priority, employee-scoped refresh for today.
- Closing the browser stops UI polling only; it does not cancel the SQL job. Cancellation is honored at date boundaries. Resume reuses the same durable job and skips its successful dates.

## Priority policy

| Priority | Purpose |
|---:|---|
| 0 | Calendar backfill and self-service force recalculation |
| 1 | Manual administrator calculation jobs |
| 2 | Scheduled company-wide catch-up and refresh |

A job yields after each date if another queued job has equal or higher priority. The active SQL date is allowed to finish unless the operator explicitly force-terminates its verified SQL session.

## SQL deployment

Run `SQL/99_HrmAttendanceCalculationJobs.sql` against the `FVN_REGISTER` database after deploying this branch. The script is idempotent and adds the `SkipCoveredDates` and `Priority` columns plus queue indexes to an existing job table.

The job worker needs the same database access as the attendance calculation procedure. Viewing SQL wait/blocking details and safely terminating a SQL session additionally requires the SQL Server DMV permissions already expected by the monitor/recovery endpoints.

## Operational checks

1. Open `/admin/hrm-attendance-calculation-jobs` and confirm active jobs appear first, including source, date progress, last progress timestamp, wait/blocker details, and any error.
2. Start a multi-day job, close the browser, then reopen the page and verify the same JobId and progress remain.
3. Cancel a job during a long date; verify it stops at the next date boundary and can be resumed.
4. For a blocked job, verify the displayed SPID, login time and connection ID before force termination. The API refuses to kill a session whose identity no longer matches.
5. Open the calendar with a missing historical date and verify a priority-0 backfill job is created. Open the calendar again while the job is running; the coordinator should join the existing job rather than enqueue duplicates.
6. Confirm current-day freshness refreshes only the signed-in employee when no successful run is recent, and does not recalculate locked/exported payroll periods.

## Compatibility note

The calculation service and `dbo.usp_CalculateHrmAttendance` remain the source of calculation results. The legacy `/api/hrm-attendance-calculation/calculate` and `/force-recalculate-self` routes now return a durable job response; clients should poll the job endpoint for completion. Export by job gathers all successful daily batches and preserves the requested date range. The Excel templates still support at most 31 day columns.

## HRM source freshness (per employee, per date)

A successful `F03HrmAttendanceCalculationRun` only proves a date was calculated, not that HRM is unchanged since. `SQL/104_HrmAttendanceSourceFreshness.sql` adds `dbo.F03HrmAttendanceSourceStamp` and a read-only fingerprint function over the HRM source tables (raw punches via `tblCapThe`/`RecordDataNew`, daily/hourly/compensatory leave, employee and company holidays).

Flow:

1. The job worker captures the current fingerprint **before** calculating a date and commits it **after** the calculation succeeded (employee scope: that employee; department/company scope: only employees present in the successful batch).
2. Opening the calendar: the coordinator already reads existing data first, then treats `never calculated` gaps **and** `stored fingerprint <> current HRM fingerprint` dates as work. Only those dates are enqueued (priority 0, employee scoped, `SkipCoveredDates=true`); the job worker removes stale dates from its covered set, so fresh dates are never recalculated and a joined older job still recalculates them.
3. Dates in locked/exported payroll periods are never queued (`ReopenPayrollPeriod=false`).
4. A date handed to a job in one coordinator pass is not retried in that pass, and a date whose current fingerprint cannot be computed is never reported stale, so no enqueue loop is possible.
5. Optional automatic flow: `HrmAttendanceStaleSweepWorker` (`HrmAttendanceFreshness:SweepEnabled`, default `false`) queues priority-2, employee-scoped jobs for employees that already have stored fingerprints and are now stale. It never queues a company-wide job.

Rollout order: deploy SQL/104, deploy the build, let normal calculations stamp dates, then optionally enable the sweep. Dates calculated before the build have no fingerprint; only those within `MissingStampLookbackDays` (default 45) are treated as stale, so deployment does not cause a recalculation storm. If SQL/104 is missing the service logs a warning and behaviour falls back to coverage-only.

Known gap: shift schedule tables (`CC_LichTrinhCa`/`tblCa`) are not in the fingerprint yet because their employee mapping is not verified here; a schedule-only change is still not detected until it is added to `dbo.fn_HrmAttendanceSourceStamp`.
