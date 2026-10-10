USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
GO

/*
===============================================================================
63_CalendarAttendancePerformance
===============================================================================
Calendar reads current and archived attendance by EmployeeCode + WorkDate.
Both tables can become large; both need seekable indexes for the per-employee,
per-period lookup. The history table's existing HrmEmployeeId + WorkDate index
does not support this query because the calendar filters by EmployeeCode.
===============================================================================
*/

IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculated', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.F03HrmAttendanceCalculated', N'EmployeeCode') IS NOT NULL
   AND COL_LENGTH(N'dbo.F03HrmAttendanceCalculated', N'WorkDate') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = N'IX_F03HrmAttendanceCalculated_EmployeeCode_WorkDate'
         AND object_id = OBJECT_ID(N'dbo.F03HrmAttendanceCalculated', N'U')
   )
BEGIN
    CREATE INDEX [IX_F03HrmAttendanceCalculated_EmployeeCode_WorkDate]
        ON dbo.[F03HrmAttendanceCalculated]([EmployeeCode], [WorkDate]);
END;
GO

/*
History lookup is filtered by EmployeeCode (not HrmEmployeeId) and WorkDate.
Without this index SQL Server can scan the full archived attendance history for
each calendar request. Keep WorkDate in the key so the index is aligned with
the monthly partition scheme and can seek the requested employee/date range.
*/
IF OBJECT_ID(N'dbo.F03HrmAttendanceHistory', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.F03HrmAttendanceHistory', N'EmployeeCode') IS NOT NULL
   AND COL_LENGTH(N'dbo.F03HrmAttendanceHistory', N'WorkDate') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = N'IX_F03HrmAttendanceHistory_EmployeeCode_WorkDate'
         AND object_id = OBJECT_ID(N'dbo.F03HrmAttendanceHistory', N'U')
   )
BEGIN
    CREATE INDEX [IX_F03HrmAttendanceHistory_EmployeeCode_WorkDate]
        ON dbo.[F03HrmAttendanceHistory]([EmployeeCode], [WorkDate])
        ON PS_F03HrmAttendanceHistory_Month([WorkDate]);
END;
GO

PRINT N'Calendar attendance performance indexes verified.';
GO
