/*
===============================================================================
98 - OT OVERVIEW: COVERING INDEX FOR UNAPPROVED ATTENDANCE
===============================================================================
Run against FVN_REGISTER after attendance schema scripts.
Supports GET api/OT/unapproved-attendance:
  EmployeeCode = @employee AND WorkDate in date range AND OtSymbol is populated.
Filtered covering index avoids scanning/looking up the full attendance table.
Safe to rerun.
===============================================================================
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculated', N'U') IS NULL
    THROW 51880, N'Missing dbo.F03HrmAttendanceCalculated.', 1;

IF COL_LENGTH(N'dbo.F03HrmAttendanceCalculated', N'EmployeeCode') IS NULL
   OR COL_LENGTH(N'dbo.F03HrmAttendanceCalculated', N'WorkDate') IS NULL
   OR COL_LENGTH(N'dbo.F03HrmAttendanceCalculated', N'OtSymbol') IS NULL
   OR COL_LENGTH(N'dbo.F03HrmAttendanceCalculated', N'CheckInTime') IS NULL
   OR COL_LENGTH(N'dbo.F03HrmAttendanceCalculated', N'CheckOutTime') IS NULL
   OR COL_LENGTH(N'dbo.F03HrmAttendanceCalculated', N'OTRecognizedMinutesDay') IS NULL
   OR COL_LENGTH(N'dbo.F03HrmAttendanceCalculated', N'OTRecognizedMinutesNight') IS NULL
    THROW 51881, N'One or more OT overview query columns are missing.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.F03HrmAttendanceCalculated')
      AND name = N'IX_F03HrmAttendanceCalculated_OTOverview'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_F03HrmAttendanceCalculated_OTOverview
        ON dbo.F03HrmAttendanceCalculated(EmployeeCode, WorkDate DESC)
        INCLUDE (FullName, ShiftAbbr, OtSymbol, CheckInTime, CheckOutTime,
                 OTRecognizedMinutesDay, OTRecognizedMinutesNight)
        WHERE OtSymbol IS NOT NULL AND OtSymbol <> N''
        WITH (DATA_COMPRESSION = PAGE);
END;
GO

SELECT i.name, i.is_disabled, i.has_filter, i.filter_definition
FROM sys.indexes AS i
WHERE i.object_id = OBJECT_ID(N'dbo.F03HrmAttendanceCalculated')
  AND i.name = N'IX_F03HrmAttendanceCalculated_OTOverview';
GO

PRINT N'OT overview attendance index is ready.';
GO
