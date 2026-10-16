/*
===============================================================================
84 - FIX HRM HOLIDAY UPSERT + ATTENDANCE SYMBOL HISTORY
===============================================================================
Fixes:
  1) HRM holidays are upserted by HolidayDate (the canonical unique key), not
     only by NNLMa. Existing FVN rows on an HRM holiday date are adopted.
  2) Source rows are deduplicated by date before writing to F03CompanyHolidays.
  3) Symbol fields are carried into batch snapshots and archived attendance.
  4) Existing archived rows are backfilled from their batch snapshots where
     available. Rows without a symbol snapshot remain NULL and are explicitly
     eligible for controlled re-calculation; no guessed symbol is written.

Run after 83_HrmHolidaySync.sql and 22_05_HrmAttendanceBatchSnapshot.sql.
Idempotent.
===============================================================================
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* A. Make sure all three durable result stores have the rule columns. */
DECLARE @Targets TABLE (TableName sysname);
INSERT @Targets VALUES
 (N'F03HrmAttendanceCalculated'),
 (N'F03HrmAttendanceCalculatedBatchSnapshot'),
 (N'F03HrmAttendanceHistory');

DECLARE @TableName sysname, @AlterSql nvarchar(max);
DECLARE target_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT TableName FROM @Targets;
OPEN target_cursor;
FETCH NEXT FROM target_cursor INTO @TableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + @TableName, N'U') IS NOT NULL
    BEGIN
        SET @AlterSql = N'
        IF COL_LENGTH(N''dbo.' + @TableName + N''', N''DayType'') IS NULL
            ALTER TABLE dbo.' + QUOTENAME(@TableName) + N' ADD DayType nvarchar(40) NULL;
        IF COL_LENGTH(N''dbo.' + @TableName + N''', N''WorkSymbol'') IS NULL
            ALTER TABLE dbo.' + QUOTENAME(@TableName) + N' ADD WorkSymbol nvarchar(50) NULL;
        IF COL_LENGTH(N''dbo.' + @TableName + N''', N''OtSymbol'') IS NULL
            ALTER TABLE dbo.' + QUOTENAME(@TableName) + N' ADD OtSymbol nvarchar(50) NULL;
        IF COL_LENGTH(N''dbo.' + @TableName + N''', N''SymbolRuleCode'') IS NULL
            ALTER TABLE dbo.' + QUOTENAME(@TableName) + N' ADD SymbolRuleCode nvarchar(100) NULL;';
        EXEC sys.sp_executesql @AlterSql;
    END;
    FETCH NEXT FROM target_cursor INTO @TableName;
END;
CLOSE target_cursor;
DEALLOCATE target_cursor;
GO

/* B. Replace the faulty MERGE-by-NNLMa with a date-safe, idempotent sync.
      The target schema permits one row per HolidayDate. If HRM has more than
      one active row on the same date, choose one deterministic classification
      (national > company > compensatory > other) and a stable NNLMa. */
CREATE OR ALTER PROCEDURE dbo.usp_SyncHrmHolidays
    @DeleteMissing bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_ID(N'HRM') IS NULL
        THROW 51480, N'Không tìm thấy database HRM để đồng bộ ngày nghỉ.', 1;

    DECLARE @TypesUpserted int = 0,
            @HolidaysUpserted int = 0,
            @HolidaysDeleted int = 0;

    BEGIN TRANSACTION;

    ;WITH TypeSource AS
    (
        SELECT
            LNLMa = RTRIM(t.LNLMa),
            LNLTen = t.LNLTen,
            LNLLoai = RTRIM(t.LNLLoai),
            t.LNLKyHieu,
            t.LNLMauSac,
            DLocked = ISNULL(t.DLocked, 0),
            GuessDayType = CASE
                WHEN LOWER(t.LNLTen) LIKE N'%bù%' THEN N'COMPENSATORY'
                WHEN LOWER(t.LNLTen) LIKE N'%công ty%' THEN N'COMPANY_HOLIDAY'
                WHEN LOWER(t.LNLTen) LIKE N'%lễ%'
                  OR LOWER(t.LNLTen) LIKE N'%tết%'
                  OR LOWER(t.LNLTen) LIKE N'%quốc%' THEN N'NATIONAL_HOLIDAY'
                ELSE N'OTHER'
            END
        FROM HRM.dbo.DM_Loainghile AS t
    )
    UPDATE d
       SET d.LNLTen = s.LNLTen,
           d.LNLLoai = s.LNLLoai,
           d.LNLKyHieu = s.LNLKyHieu,
           d.LNLMauSac = s.LNLMauSac,
           d.DLocked = s.DLocked,
           d.SyncedAt = SYSUTCDATETIME(),
           d.DayType = CASE WHEN d.DayTypeIsManual = 1 THEN d.DayType ELSE s.GuessDayType END
    FROM dbo.F03HrmHolidayTypes AS d
    INNER JOIN TypeSource AS s ON s.LNLMa = d.LNLMa;

    SET @TypesUpserted = @@ROWCOUNT;

    ;WITH TypeSource AS
    (
        SELECT
            LNLMa = RTRIM(t.LNLMa),
            LNLTen = t.LNLTen,
            LNLLoai = RTRIM(t.LNLLoai),
            t.LNLKyHieu,
            t.LNLMauSac,
            DLocked = ISNULL(t.DLocked, 0),
            GuessDayType = CASE
                WHEN LOWER(t.LNLTen) LIKE N'%bù%' THEN N'COMPENSATORY'
                WHEN LOWER(t.LNLTen) LIKE N'%công ty%' THEN N'COMPANY_HOLIDAY'
                WHEN LOWER(t.LNLTen) LIKE N'%lễ%'
                  OR LOWER(t.LNLTen) LIKE N'%tết%'
                  OR LOWER(t.LNLTen) LIKE N'%quốc%' THEN N'NATIONAL_HOLIDAY'
                ELSE N'OTHER'
            END
        FROM HRM.dbo.DM_Loainghile AS t
    )
    INSERT dbo.F03HrmHolidayTypes (LNLMa,LNLTen,LNLLoai,LNLKyHieu,LNLMauSac,DLocked,DayType)
    SELECT s.LNLMa,s.LNLTen,s.LNLLoai,s.LNLKyHieu,s.LNLMauSac,s.DLocked,s.GuessDayType
    FROM TypeSource AS s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.F03HrmHolidayTypes d WHERE d.LNLMa=s.LNLMa);

    SET @TypesUpserted += @@ROWCOUNT;

    ;WITH TypeSource AS
    (
        SELECT
            LNLMa = RTRIM(t.LNLMa),
            LNLLoai = RTRIM(t.LNLLoai),
            t.DLocked,
            t.LNLTen,
            DayType = ISNULL(t.DayType, N'NATIONAL_HOLIDAY')
        FROM dbo.F03HrmHolidayTypes AS t
    ),
    HolidaySource AS
    (
        SELECT
            HrmCode = RTRIM(n.NNLMa),
            HolidayDate = CONVERT(date, n.NNLNgay),
            Description = LEFT(COALESCE(NULLIF(LTRIM(RTRIM(n.NNLTen)), N''), N'Ngày nghỉ HRM'), 200),
            TypeCode = RTRIM(n.NNLLoai),
            DayType = COALESCE(t.DayType, N'NATIONAL_HOLIDAY'),
            HolidayType = CONVERT(tinyint, CASE COALESCE(t.DayType, N'NATIONAL_HOLIDAY')
                WHEN N'COMPANY_HOLIDAY' THEN 1
                WHEN N'NATIONAL_HOLIDAY' THEN 2
                WHEN N'COMPENSATORY' THEN 3
                ELSE 4 END),
            rn = ROW_NUMBER() OVER
            (
                PARTITION BY CONVERT(date, n.NNLNgay)
                ORDER BY
                    CASE COALESCE(t.DayType, N'NATIONAL_HOLIDAY')
                        WHEN N'NATIONAL_HOLIDAY' THEN 1
                        WHEN N'COMPANY_HOLIDAY' THEN 2
                        WHEN N'COMPENSATORY' THEN 3
                        ELSE 4 END,
                    RTRIM(n.NNLMa)
            )
        FROM HRM.dbo.tblNgayNghiLe AS n
        LEFT JOIN TypeSource AS t
          ON t.LNLLoai = RTRIM(n.NNLLoai)
         AND ISNULL(t.DLocked, 0) = 0
        WHERE ISNULL(n.DLocked, 0) = 0
          AND n.NNLNgay IS NOT NULL
    )
    SELECT HrmCode, HolidayDate, Description, TypeCode, DayType, HolidayType
    INTO #HrmHolidaySource
    FROM HolidaySource
    WHERE rn = 1;

    /* Release a source-code key if HRM moved that code to another date.
       The old HRM row is removed below after the new date has been upserted. */
    UPDATE d
       SET d.HrmHolidayCode = NULL,
           d.HrmTypeCode = NULL
    FROM dbo.F03CompanyHolidays AS d
    INNER JOIN #HrmHolidaySource AS s
      ON s.HrmCode = d.HrmHolidayCode
     AND s.HolidayDate <> d.HolidayDate
    WHERE d.Source = N'HRM';

    /* Adopt/update by the unique business key (HolidayDate). This also adopts
       legacy FVN rows already occupying the same date instead of inserting a
       second row and violating IX_F03CompanyHolidays_HolidayDate. */
    UPDATE d
       SET d.Description = s.Description,
           d.Year = YEAR(s.HolidayDate),
           d.HrmTypeCode = s.TypeCode,
           d.DayType = s.DayType,
           d.HolidayType = s.HolidayType,
           d.Source = N'HRM',
           d.HrmHolidayCode = s.HrmCode,
           d.IsActive = 1,
           d.ModifiedBy = 0,
           d.ModifiedAt = GETDATE(),
           d.LastModifiedSource = N'HRM_SYNC'
    FROM dbo.F03CompanyHolidays AS d
    INNER JOIN #HrmHolidaySource AS s
      ON s.HolidayDate = d.HolidayDate;

    SET @HolidaysUpserted = @@ROWCOUNT;

    INSERT dbo.F03CompanyHolidays
    (
        IsActive, CreatedBy, LastModifiedSource, CreatedAt,
        HolidayDate, Description, Year, TinhPhep, HolidayType,
        Source, HrmHolidayCode, HrmTypeCode, DayType
    )
    SELECT
        1, 0, N'HRM_SYNC', GETDATE(),
        s.HolidayDate, s.Description, YEAR(s.HolidayDate), 1, s.HolidayType,
        N'HRM', s.HrmCode, s.TypeCode, s.DayType
    FROM #HrmHolidaySource AS s
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.F03CompanyHolidays AS d WITH (UPDLOCK, HOLDLOCK)
        WHERE d.HolidayDate = s.HolidayDate
    );

    SET @HolidaysUpserted += @@ROWCOUNT;

    IF @DeleteMissing = 1
    BEGIN
        DELETE d
        FROM dbo.F03CompanyHolidays AS d
        WHERE d.Source = N'HRM'
          AND NOT EXISTS
          (
              SELECT 1
              FROM HRM.dbo.tblNgayNghiLe AS n
              WHERE RTRIM(n.NNLMa) = d.HrmHolidayCode
                AND ISNULL(n.DLocked, 0) = 0
          );
        SET @HolidaysDeleted = @@ROWCOUNT;
    END;

    DROP TABLE #HrmHolidaySource;
    COMMIT TRANSACTION;

    SELECT
        TypesUpserted = @TypesUpserted,
        HolidaysUpserted = @HolidaysUpserted,
        HolidaysDeleted = @HolidaysDeleted,
        HolidayCount = (SELECT COUNT(*) FROM dbo.F03CompanyHolidays WHERE Source = N'HRM');
END;
GO

/* C. Keep the calculated batch snapshot synchronized when the application
      applies or re-applies attendance symbol rules after the SQL calculation. */
IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot', N'U') IS NOT NULL
BEGIN
    EXEC(N'
    CREATE OR ALTER TRIGGER dbo.tr_F03HrmAttendanceCalculated_SymbolSnapshot
    ON dbo.F03HrmAttendanceCalculated
    AFTER INSERT, UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;

        UPDATE target
           SET target.DayType = source.DayType,
               target.WorkSymbol = source.WorkSymbol,
               target.OtSymbol = source.OtSymbol,
               target.SymbolRuleCode = source.SymbolRuleCode
        FROM dbo.F03HrmAttendanceCalculatedBatchSnapshot AS target
        INNER JOIN inserted AS source
          ON target.CalculationBatchId = source.CalculationBatchId
         AND target.HrmEmployeeId = source.HrmEmployeeId
         AND target.WorkDate = source.WorkDate;
    END;');

    EXEC sys.sp_settriggerorder
        @triggername = N'dbo.tr_F03HrmAttendanceCalculated_SymbolSnapshot',
        @order = N'Last',
        @stmttype = N'INSERT';
    EXEC sys.sp_settriggerorder
        @triggername = N'dbo.tr_F03HrmAttendanceCalculated_SymbolSnapshot',
        @order = N'Last',
        @stmttype = N'UPDATE';
END;
GO

/* D. Copy symbols into archive rows while their calculated/snapshot source is
      still available. Backfill old history rows from immutable batch snapshots. */
IF OBJECT_ID(N'dbo.F03HrmAttendanceHistory', N'U') IS NOT NULL
BEGIN
    UPDATE h
       SET h.DayType = COALESCE(s.DayType, c.DayType),
           h.WorkSymbol = COALESCE(s.WorkSymbol, c.WorkSymbol),
           h.OtSymbol = COALESCE(s.OtSymbol, c.OtSymbol),
           h.SymbolRuleCode = COALESCE(s.SymbolRuleCode, c.SymbolRuleCode)
    FROM dbo.F03HrmAttendanceHistory AS h
    LEFT JOIN dbo.F03HrmAttendanceCalculatedBatchSnapshot AS s
      ON s.CalculationBatchId = h.CalculationBatchId
     AND s.HrmEmployeeId = h.HrmEmployeeId
     AND s.WorkDate = h.WorkDate
    LEFT JOIN dbo.F03HrmAttendanceCalculated AS c
      ON c.CalculationBatchId = h.CalculationBatchId
     AND c.HrmEmployeeId = h.HrmEmployeeId
     AND c.WorkDate = h.WorkDate
    WHERE h.DayType IS NULL
       OR h.WorkSymbol IS NULL
       OR h.OtSymbol IS NULL
       OR h.SymbolRuleCode IS NULL;

    EXEC(N'
    CREATE OR ALTER TRIGGER dbo.tr_F03HrmAttendanceHistory_SymbolSnapshot
    ON dbo.F03HrmAttendanceHistory
    AFTER INSERT
    AS
    BEGIN
        SET NOCOUNT ON;

        UPDATE h
           SET h.DayType = COALESCE(c.DayType, s.DayType),
               h.WorkSymbol = COALESCE(c.WorkSymbol, s.WorkSymbol),
               h.OtSymbol = COALESCE(c.OtSymbol, s.OtSymbol),
               h.SymbolRuleCode = COALESCE(c.SymbolRuleCode, s.SymbolRuleCode)
        FROM dbo.F03HrmAttendanceHistory AS h
        INNER JOIN inserted AS i ON i.HistoryId = h.HistoryId
        LEFT JOIN dbo.F03HrmAttendanceCalculated AS c
          ON c.CalculationBatchId = i.CalculationBatchId
         AND c.HrmEmployeeId = i.HrmEmployeeId
         AND c.WorkDate = i.WorkDate
        LEFT JOIN dbo.F03HrmAttendanceCalculatedBatchSnapshot AS s
          ON s.CalculationBatchId = i.CalculationBatchId
         AND s.HrmEmployeeId = i.HrmEmployeeId
         AND s.WorkDate = i.WorkDate;
    END;');
END;
GO

/* E. Verification: the sync source must be unique by date before writing. */
IF EXISTS
(
    SELECT 1
    FROM HRM.dbo.tblNgayNghiLe
    WHERE ISNULL(DLocked, 0) = 0 AND NNLNgay IS NOT NULL
    GROUP BY CONVERT(date, NNLNgay)
    HAVING COUNT(*) > 1
)
    PRINT N'HRM có nhiều mã ngày nghỉ cùng một ngày; procedure đã chọn một mã theo thứ tự ưu tiên loại ngày và NNLMa.';
GO
