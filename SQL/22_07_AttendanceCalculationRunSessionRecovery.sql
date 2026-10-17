USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/*
===============================================================================
22_07 - HRM ATTENDANCE RUN SESSION IDENTITY

Purpose:
  - Persist the exact SQL Server session identity for every calculation Run.
  - Pair @@SPID + login_time with sys.dm_exec_connections.connection_id so a recycled SPID cannot
    be mistaken for the original calculation session.
  - The API startup worker uses this pair to reconcile abandoned Running rows.

Deployment:
  - Run after 22_00_HrmAttendanceTables.sql and before 22_03_CalculateHrmAttendance.sql.
  - Runtime SQL login needs VIEW SERVER STATE (SQL Server 2019 and earlier /
    standard SQL Server DMV permission model) to inspect other sessions.
    On SQL Server 2022+, grant VIEW SERVER PERFORMANCE STATE where required.
  - If DMV access is denied, the application deliberately leaves Run rows
    unchanged rather than guessing that a calculation has stopped.
===============================================================================
*/

IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculationRun',N'U') IS NULL
    THROW 51707,N'F03HrmAttendanceCalculationRun chưa tồn tại. Chạy SQL/22_00_HrmAttendanceTables.sql trước.',1;
GO

IF COL_LENGTH(N'dbo.F03HrmAttendanceCalculationRun',N'SqlSessionId') IS NULL
    ALTER TABLE dbo.F03HrmAttendanceCalculationRun ADD SqlSessionId int NULL;
GO

IF COL_LENGTH(N'dbo.F03HrmAttendanceCalculationRun',N'SqlSessionLoginTime') IS NULL
    ALTER TABLE dbo.F03HrmAttendanceCalculationRun ADD SqlSessionLoginTime datetime2(3) NULL;
ELSE
    ALTER TABLE dbo.F03HrmAttendanceCalculationRun ALTER COLUMN SqlSessionLoginTime datetime2(3) NULL;
IF COL_LENGTH(N'dbo.F03HrmAttendanceCalculationRun',N'SqlConnectionId') IS NULL
    ALTER TABLE dbo.F03HrmAttendanceCalculationRun ADD SqlConnectionId uniqueidentifier NULL;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id=OBJECT_ID(N'dbo.F03HrmAttendanceCalculationRun')
      AND name=N'IX_F03HrmAttendanceCalculationRun_RunningSession'
)
    CREATE INDEX IX_F03HrmAttendanceCalculationRun_RunningSession
        ON dbo.F03HrmAttendanceCalculationRun(Status,StartedAt,SqlSessionId,SqlSessionLoginTime,SqlConnectionId)
        WHERE Status=N'Running';
GO

SELECT
    c.name AS ColumnName,
    TYPE_NAME(c.user_type_id) AS DataType,
    c.is_nullable AS IsNullable
FROM sys.columns AS c
WHERE c.object_id=OBJECT_ID(N'dbo.F03HrmAttendanceCalculationRun')
  AND c.name IN (N'SqlSessionId',N'SqlSessionLoginTime',N'SqlConnectionId')
ORDER BY c.column_id;
GO
