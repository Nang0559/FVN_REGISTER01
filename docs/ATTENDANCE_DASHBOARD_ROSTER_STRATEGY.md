# Attendance Dashboard: roster-first strategy

## Decision

Do not build a full employee shift-planning module yet. Reuse HRM schedule data where it can authoritatively identify the employee, work date, and planned shift. Use `F03HrmAttendanceCalculated` for actual attendance only. If HRM does not expose employee-level planned shifts, add a minimal expected-work roster as a later, explicit prerequisite for absence classification.

## Data semantics

The dashboard must keep these concepts separate:

- **Actual attendance**: calculated HRM attendance records, including employee code, work date, shift abbreviation, check-in, and check-out.
- **Expected work**: an employee's planned shift on a specific work date. It must come from an authoritative HRM employee schedule or a maintained expected-work roster.
- **Reconciliation**: comparison of expected work with actual attendance. It must never infer that an employee was absent simply because there is no attendance row.

Until expected work is available, dashboard labels should be limited to observed facts such as "đã ghi nhận giờ vào", "đã ghi nhận giờ ra", and "chưa có giờ ra". Do not label employees as absent or publish an absence rate calculated as total active employees minus employees with check-in.

## Source verification gate

Before coding the roster join, inspect the deployed schema and sample data for:

- `CC_LichTrinhCa`
- `tblca`
- related HRM employee/shift schedule tables and the HRM sync procedures
- `F03HrmAttendanceCalculated`

The source qualifies as an expected roster only if records can be mapped unambiguously to an employee, a work date (including overnight shifts), and a planned shift. A shift master/configuration table by itself is not an employee roster.

## Dashboard behavior

1. Read only locally synchronized/calculated data during dashboard requests; never trigger a per-approver HRM sync.
2. Filter department-level summaries by the active approval policies matching the authenticated approver position, level, and request type.
3. Group observed attendance by work date and shift. Deduplicate people by stable employee identity, not by punch count.
4. Treat overnight shifts using the shift's work date and shift interval, not calendar date alone.
5. Show the latest successful calculation/sync timestamp and clearly flag stale or incomplete data.
6. Only show expected headcount, missing check-in, or absence rate after an authoritative roster exists for the selected date and scope.
7. Once verified that HRM has no employee/day roster, introduce a minimal local roster with employee code, work date, shift code, source, active/version fields, and audit metadata. Add uniqueness/idempotency rules and an import/sync path; do not create a full shift-planning workflow unless business requirements demand it.

## Acceptance criteria

- No false absence classification when roster data is unavailable or stale.
- Actual attendance counters reconcile to distinct employee identities in the calculated attendance source.
- Department scope is restricted to the authenticated approver's matching active policy scope.
- Dashboard HTTP requests do not call HRM directly.
- Day, evening, and overnight shifts are grouped consistently and the source freshness is visible.
- Tests cover duplicate attendance rows, missing check-in, missing check-out, overnight shifts, stale calculation batches, and empty roster data.
