/*
===============================================================================
85 - ATTENDANCE CALENDAR SEEK INDEXES + SCHEMA VERIFICATION
===============================================================================
Run after 22_00, 22_04, 63 and 84.
The attendance tables define EmployeeCode as nvarchar(50) and WorkDate as date.
The application query binds @code as nvarchar(50), @from/@to as date.
Keep the index key order aligned with equality + date-range predicates.
===============================================================================
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculated', N'U') IS NULL
    THROW 51850, N'Missing dbo.F03HrmAttendanceCalculated.', 1;
IF OBJECT_ID(N'dbo.F03HrmAttendanceHistory', N'U') IS NULL
    THROW 51851, N'Missing dbo.F03HrmAttendanceHistory.', 1;

DECLARE @CurrentType sysname =
(
    SELECT TYPE_NAME(user_type_id)
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.F03HrmAttendanceCalculated')
      AND name = N'EmployeeCode'
);
DECLARE @HistoryType sysname =
(
    SELECT TYPE_NAME(user_type_id)
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.F03HrmAttendanceHistory')
      AND name = N'EmployeeCode'
);
DECLARE @CurrentLength smallint =
(
    SELECT max_length
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.F03HrmAttendanceCalculated')
      AND name = N'EmployeeCode'
);
DECLARE @HistoryLength smallint =
(
    SELECT max_length
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.F03HrmAttendanceHistory')
      AND name = N'EmployeeCode'
);

IF @CurrentType IS NULL OR @HistoryType IS NULL
    THROW 51852, N'EmployeeCode is missing from one of the attendance tables.', 1;
IF @CurrentType <> @HistoryType OR @CurrentLength <> @HistoryLength
    THROW 51853, N'EmployeeCode type/length differs between Calculated and History. Align the schema and C# parameter before deploying.', 1;
IF @CurrentType <> N'nvarchar' OR @CurrentLength <> 100
    THROW 51854, N'Expected EmployeeCode nvarchar(50) (max_length=100 bytes), matching the typed calendar query parameter.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.F03HrmAttendanceCalculated')
      AND name = N'IX_F03HrmAttendanceCalculated_EmployeeCode_WorkDate'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_F03HrmAttendanceCalculated_EmployeeCode_WorkDate
        ON dbo.F03HrmAttendanceCalculated(EmployeeCode, WorkDate)
        WITH (DATA_COMPRESSION = PAGE);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.F03HrmAttendanceHistory')
      AND name = N'IX_F03HrmAttendanceHistory_EmployeeCode_WorkDate'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_F03HrmAttendanceHistory_EmployeeCode_WorkDate
        ON dbo.F03HrmAttendanceHistory(EmployeeCode, WorkDate)
        WITH (DATA_COMPRESSION = PAGE)
        ON PS_F03HrmAttendanceHistory_Month(WorkDate);
END;
GO

/* Deployment verification: expected data types, lengths and ordered index keys. */
SELECT
    OBJECT_NAME(c.object_id) AS TableName,
    TYPE_NAME(c.user_type_id) AS DataType,
    c.max_length,
    c.is_nullable
FROM sys.columns AS c
WHERE c.object_id IN
(
    OBJECT_ID(N'dbo.F03HrmAttendanceCalculated'),
    OBJECT_ID(N'dbo.F03HrmAttendanceHistory')
)
AND c.name = N'EmployeeCode'
ORDER BY TableName;

SELECT
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    i.type_desc,
    i.is_disabled,
    ic.key_ordinal,
    c.name AS ColumnName,
    ic.is_included_column
FROM sys.indexes AS i
JOIN sys.index_columns AS ic
  ON ic.object_id = i.object_id
 AND ic.index_id = i.index_id
JOIN sys.columns AS c
  ON c.object_id = ic.object_id
 AND c.column_id = ic.column_id
WHERE i.object_id IN
(
    OBJECT_ID(N'dbo.F03HrmAttendanceCalculated'),
    OBJECT_ID(N'dbo.F03HrmAttendanceHistory')
)
AND i.name IN
(
    N'IX_F03HrmAttendanceCalculated_EmployeeCode_WorkDate',
    N'IX_F03HrmAttendanceHistory_EmployeeCode_WorkDate'
)
ORDER BY TableName, IndexName, ic.is_included_column, ic.key_ordinal;
GO

PRINT N'Attendance calendar seek indexes and EmployeeCode schema verified.';
GO
