USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
GO

/*
===============================================================================
63_CalendarAttendancePerformance
===============================================================================
Calendar reads F03HrmAttendanceCalculated by canonical EmployeeCode + WorkDate.
This table can become large because attendance is calculated for the company,
therefore the calendar lookup must not scan the attendance result set.
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

PRINT N'Calendar attendance performance index verified.';
GO
