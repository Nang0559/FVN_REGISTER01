/*
===============================================================================
 22_06 - Diagnose / clean up attendance calculation sessions (MANUAL USE ONLY)

 Why: when the API process is killed or restarted while dbo.usp_CalculateHrmAttendance is running,
 SQL Server does not notice (the proc sends nothing to the client until it ends), so the session
 keeps running for hours, holds exclusive locks on F03HrmAttendanceCalculated / F03HrmOTActual and
 the per-date applock, and the Run row stays 'Running'.

 This script is NOT part of the deployment chain. Run the SELECTs, check host/program/start time,
 then KILL only the sessions you are sure belong to a stopped API process. KILL rolls back the
 current (uncommitted) day; days already committed are kept.
===============================================================================
*/

-- 1) Sessions currently executing the calculation proc (original batch text via input buffer).
SELECT  r.session_id,
        s.host_name, s.program_name, s.login_name,
        r.status, r.command,
        r.start_time,
        DATEDIFF(minute, r.start_time, GETDATE()) AS running_minutes,
        r.blocking_session_id, r.wait_type, r.wait_time,
        r.open_transaction_count,
        ib.event_info AS original_batch
FROM sys.dm_exec_requests AS r
JOIN sys.dm_exec_sessions AS s ON s.session_id = r.session_id
CROSS APPLY sys.dm_exec_input_buffer(r.session_id, r.request_id) AS ib
WHERE r.session_id <> @@SPID
  AND ib.event_info LIKE N'%usp_CalculateHrmAttendance%'
ORDER BY r.start_time;

-- 2) Who holds the per-date attendance applocks / the company-run guard.
SELECT  l.request_session_id AS session_id,
        l.resource_description,
        l.request_mode, l.request_status
FROM sys.dm_tran_locks AS l
WHERE l.resource_type = N'APPLICATION'
  AND l.resource_description LIKE N'%FVN_REGISTER:ATTENDANCE:%';

-- 3) Run rows that are still 'Running' (the worker closes its own stale rows on startup).
SELECT  CalculationBatchId, DeptCode, EmployeeCode, FromDate, ToDate, TriggeredBy, StartedAt,
        DATEDIFF(minute, StartedAt, GETDATE()) AS running_minutes
FROM dbo.F03HrmAttendanceCalculationRun
WHERE Status = N'Running'
ORDER BY StartedAt;

-- 4) Stop an orphaned session (replace 123 with a session_id from query 1), then re-run query 1/3.
-- KILL 123;
