/*
  Remove one E2E run from a disposable test database only.
  Edit @RunId to the exact run ID printed by Test-ApprovalEmailE2E.ps1 and
  @RunStartedAt to that run's StartedAt value from its JSON report.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @RunId nvarchar(100)=N'TST_E2E_REPLACE_WITH_EXACT_RUN_ID';
DECLARE @RunStartedAt datetime2(0)='2000-01-01T00:00:00';

IF DB_NAME() NOT LIKE '%TEST%'
   AND DB_NAME() NOT LIKE '%QA%'
   AND DB_NAME() NOT LIKE '%E2E%'
   AND DB_NAME() NOT LIKE '%AUTOMATION%'
    THROW 52011, 'Safety stop: select a disposable Test/QA/E2E/Automation database first.', 1;

IF LEFT(@RunId,8)<>N'TST_E2E_' OR @RunId=N'TST_E2E_REPLACE_WITH_EXACT_RUN_ID'
    THROW 52012, 'Safety stop: replace @RunId with the exact TST_E2E_ run ID first.', 1;

IF OBJECT_ID(N'dbo.F03LeaveDays',N'U') IS NULL
 OR OBJECT_ID(N'dbo.F03LeaveDayDetails',N'U') IS NULL
 OR OBJECT_ID(N'dbo.F03ApprovalSnapshots',N'U') IS NULL
 OR OBJECT_ID(N'dbo.F03ApprovalStepSnapshots',N'U') IS NULL
 OR OBJECT_ID(N'dbo.F03ApprovalHistories',N'U') IS NULL
 OR OBJECT_ID(N'dbo.F03EmailQueues',N'U') IS NULL
    THROW 52013, 'Required cleanup table is missing; do not run partial cleanup.', 1;

DECLARE @RequestIds TABLE (Id int PRIMARY KEY);
INSERT @RequestIds(Id)
SELECT Id
FROM dbo.F03LeaveDays
WHERE LeaveReason LIKE @RunId+N'%'
  AND EmployeeCode=N'E0001';

IF NOT EXISTS (SELECT 1 FROM @RequestIds)
BEGIN
    PRINT N'No F03LeaveDays rows matched this run ID; no business records were deleted.';
    RETURN;
END;

BEGIN TRANSACTION;

-- Remove correlated outbox rows first. Request IDs are matched as complete JSON
-- numeric tokens so request 12 does not match request 112.
DELETE q
FROM dbo.F03EmailQueues q
CROSS JOIN @RequestIds r
WHERE q.CreatedAt>=@RunStartedAt
  AND (
      q.Payload LIKE N'%"requestId":'+CONVERT(nvarchar(20),r.Id)+N',%'
   OR q.Payload LIKE N'%"requestId":'+CONVERT(nvarchar(20),r.Id)+N'}%'
   OR q.Payload LIKE N'%"RequestId":'+CONVERT(nvarchar(20),r.Id)+N',%'
   OR q.Payload LIKE N'%"RequestId":'+CONVERT(nvarchar(20),r.Id)+N'}%'
   OR q.Body LIKE N'%"requestId":'+CONVERT(nvarchar(20),r.Id)+N',%'
   OR q.Body LIKE N'%"requestId":'+CONVERT(nvarchar(20),r.Id)+N'}%'
   OR q.Body LIKE N'%"RequestId":'+CONVERT(nvarchar(20),r.Id)+N',%'
   OR q.Body LIKE N'%"RequestId":'+CONVERT(nvarchar(20),r.Id)+N'}%'
   OR q.Subject LIKE N'%Request ID: '+CONVERT(nvarchar(20),r.Id)+N'%'
  );

DELETE h
FROM dbo.F03ApprovalHistories h
JOIN @RequestIds r ON r.Id=h.RequestId
WHERE h.RequestType=0; -- RequestModule.Leave

DELETE st
FROM dbo.F03ApprovalStepSnapshots st
JOIN dbo.F03ApprovalSnapshots s ON s.Id=st.SnapshotId
JOIN @RequestIds r ON r.Id=s.RequestId
WHERE s.RequestType=0;

DELETE s
FROM dbo.F03ApprovalSnapshots s
JOIN @RequestIds r ON r.Id=s.RequestId
WHERE s.RequestType=0;

DELETE d
FROM dbo.F03LeaveDayDetails d
JOIN @RequestIds r ON r.Id=d.LeaveDaysId;

DELETE l
FROM dbo.F03LeaveDays l
JOIN @RequestIds r ON r.Id=l.Id;

COMMIT TRANSACTION;

PRINT N'E2E cleanup completed for RunId='+@RunId+N'.';
PRINT N'Only Leave request rows, their approval snapshot/history children, and correlated email queue rows were targeted.';
PRINT N'Review TestResults JSON and any notification/action tables separately if those features were exercised.';
