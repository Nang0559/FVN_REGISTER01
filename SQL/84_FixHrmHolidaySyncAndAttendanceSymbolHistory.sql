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
      (national holiday > Sunday > company holiday > legacy other) and a stable NNLMa. */
CREATE OR ALTER PROCEDURE dbo.usp_SyncHrmHolidays
    @DeleteMissing bit = 1,
    /* 0 (mặc định) = không trả result set. Bắt buộc khi gọi từ usp_CalculateHrmAttendance: EF Core chỉ đọc
       result set ĐẦU TIÊN, nếu proc này trả thống kê trước thì mất cột CalculatedRows. */
    @ReturnResult bit = 0
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

    DECLARE @SyncLockResult int;
    EXEC @SyncLockResult = sys.sp_getapplock
        @Resource = N'FVN_REGISTER:HRM_HOLIDAY_SYNC',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 30000;
    IF @SyncLockResult < 0
        THROW 51481, N'Không lấy được khóa đồng bộ ngày nghỉ HRM; vui lòng thử lại.', 1;

    ;WITH TypeSource AS
    (
        SELECT
            LNLMa = RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4),
            LNLTen = t.LNLTen,
            LNLLoai = RTRIM(t.LNLLoai),
            t.LNLKyHieu,
            t.LNLMauSac,
            DLocked = ISNULL(t.DLocked, 0),
            GuessDayType = CASE
                /* 0001=Normal (không phải ngày nghỉ lịch); 0002=Sunday/company rest; 0003=National holiday. */
                WHEN RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4) = '0003' THEN N'NATIONAL_HOLIDAY' -- HRM 003: Nghỉ lễ
                WHEN RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4) = '0002' THEN N'SUNDAY' -- HRM 002: Nghỉ Chủ nhật
                WHEN RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4) = '0001' THEN N'NORMAL' -- HRM 001: Ngày nghỉ thường, không dùng làm ngày nghỉ trên lịch
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
           d.DayType = CASE
               WHEN s.LNLMa = '0001' THEN N'NORMAL'
               WHEN s.LNLMa = '0002' THEN N'SUNDAY'
               WHEN s.LNLMa = '0003' THEN N'NATIONAL_HOLIDAY'
               WHEN d.DayTypeIsManual = 1 THEN d.DayType
               ELSE s.GuessDayType
           END
    FROM dbo.F03HrmHolidayTypes AS d
    INNER JOIN TypeSource AS s ON RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), d.LNLMa))), 4) = s.LNLMa;

    SET @TypesUpserted = @@ROWCOUNT;

    ;WITH TypeSource AS
    (
        SELECT
            LNLMa = RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4),
            LNLTen = t.LNLTen,
            LNLLoai = RTRIM(t.LNLLoai),
            t.LNLKyHieu,
            t.LNLMauSac,
            DLocked = ISNULL(t.DLocked, 0),
            GuessDayType = CASE
                /* Ánh xạ tường minh cho 3 loại HRM hiện có; loại mới chưa biết thì đoán theo tên. */
                WHEN RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4) = '0003' THEN N'NATIONAL_HOLIDAY' -- HRM 003: Nghỉ lễ
                WHEN RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4) = '0002' THEN N'SUNDAY' -- HRM 002: Nghỉ Chủ nhật
                WHEN RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4) = '0001' THEN N'NORMAL' -- HRM 001: Ngày nghỉ thường, không dùng làm ngày nghỉ trên lịch
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
    WHERE NOT EXISTS (SELECT 1 FROM dbo.F03HrmHolidayTypes d WHERE RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), d.LNLMa))), 4)=s.LNLMa);

    SET @TypesUpserted += @@ROWCOUNT;

    ;WITH TypeSource AS
    (
        SELECT
            LNLMa = RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4),
            LNLLoai = RTRIM(t.LNLLoai),
            t.DLocked,
            t.LNLTen,
            t.LNLKyHieu,
            t.LNLMauSac,
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
            HolidaySymbol = LEFT(NULLIF(LTRIM(RTRIM(t.LNLKyHieu)), N''), 50),
            TypeName = LEFT(t.LNLTen, 200),
            TypeColor = LEFT(t.LNLMauSac, 50),
            DayType = COALESCE(t.DayType, N'NATIONAL_HOLIDAY'),
            HolidayType = CONVERT(tinyint, CASE COALESCE(t.DayType, N'NATIONAL_HOLIDAY')
                WHEN N'COMPANY_HOLIDAY' THEN 1
                WHEN N'SUNDAY' THEN 1
                WHEN N'NATIONAL_HOLIDAY' THEN 2
                ELSE 4 END),
            rn = ROW_NUMBER() OVER
            (
                PARTITION BY CONVERT(date, n.NNLNgay)
                ORDER BY
                    CASE COALESCE(t.DayType, N'NATIONAL_HOLIDAY')
                        WHEN N'NATIONAL_HOLIDAY' THEN 1
                        WHEN N'SUNDAY' THEN 2
                        WHEN N'COMPANY_HOLIDAY' THEN 3
                        ELSE 4 END,
                    RTRIM(n.NNLMa)
            )
        FROM HRM.dbo.tblNgayNghiLe AS n
        /* NNLLoai của tblNgayNghiLe là mã loại => khớp LNLMa trước. Chỉ khi không có LNLMa nào khớp
           mới thử LNLLoai (nhóm loại); khi đó chọn một DayType xác định để không nhân dòng. */
        OUTER APPLY
        (
            SELECT TOP (1) x.DayType, x.LNLKyHieu, x.LNLTen, x.LNLMauSac
            FROM TypeSource AS x
            WHERE ISNULL(x.DLocked, 0) = 0
              AND (x.LNLMa = RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), n.NNLLoai))), 4) OR x.LNLLoai = RTRIM(n.NNLLoai))
            ORDER BY CASE WHEN x.LNLMa = RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), n.NNLLoai))), 4) THEN 0 ELSE 1 END,
                     CASE x.DayType
                         WHEN N'NATIONAL_HOLIDAY' THEN 1
                         WHEN N'COMPANY_HOLIDAY'  THEN 2
                         ELSE 4 END,
                     x.LNLMa
        ) AS t
        WHERE ISNULL(n.DLocked, 0) = 0
          AND n.NNLNgay IS NOT NULL
    )
    SELECT HrmCode, HolidayDate, Description, TypeCode, HolidaySymbol, TypeName, TypeColor, DayType, HolidayType
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
           d.HolidaySymbol = s.HolidaySymbol,
           d.HolidayTypeName = s.TypeName,
           d.HolidayColor = s.TypeColor,
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
        Source, HrmHolidayCode, HrmTypeCode, DayType,
        HolidaySymbol, HolidayTypeName, HolidayColor
    )
    SELECT
        1, 0, N'HRM_SYNC', GETDATE(),
        s.HolidayDate, s.Description, YEAR(s.HolidayDate), 1, s.HolidayType,
        N'HRM', s.HrmCode, s.TypeCode, s.DayType,
        s.HolidaySymbol, s.TypeName, s.TypeColor
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

    /* HRM is the sole source of company holidays.
       Rows from the former FVN-maintained calendar that were not adopted by
       the date-based upsert must no longer affect the shared calendar. */
    UPDATE dbo.F03CompanyHolidays
       SET IsActive = 0,
           ModifiedBy = 0,
           ModifiedAt = GETDATE(),
           LastModifiedSource = N'HRM_SYNC_LEGACY_DISABLED'
     WHERE Source = N'FVN'
       AND IsActive = 1;

    DROP TABLE #HrmHolidaySource;
    COMMIT TRANSACTION;

    IF @ReturnResult = 1
        SELECT
            TypesUpserted = @TypesUpserted,
            HolidaysUpserted = @HolidaysUpserted,
            HolidaysDeleted = @HolidaysDeleted,
            HolidayCount = (SELECT COUNT(*) FROM dbo.F03CompanyHolidays WHERE Source = N'HRM');
END;
GO

/* C. Backfill old archive rows from their immutable calculation batch snapshots.
      New archive operations copy these fields directly in 22_04; new batch writes
      copy them directly in 22_05. */
IF OBJECT_ID(N'dbo.F03HrmAttendanceHistory', N'U') IS NOT NULL
BEGIN
    UPDATE h
       SET h.DayType = COALESCE(s.DayType, h.DayType),
           h.WorkSymbol = COALESCE(s.WorkSymbol, h.WorkSymbol),
           h.OtSymbol = COALESCE(s.OtSymbol, h.OtSymbol),
           h.SymbolRuleCode = COALESCE(s.SymbolRuleCode, h.SymbolRuleCode)
    FROM dbo.F03HrmAttendanceHistory AS h
    INNER JOIN dbo.F03HrmAttendanceCalculatedBatchSnapshot AS s
      ON s.CalculationBatchId = h.CalculationBatchId
     AND s.HrmEmployeeId = h.HrmEmployeeId
     AND s.WorkDate = h.WorkDate
    WHERE h.DayType IS NULL OR h.WorkSymbol IS NULL
       OR h.OtSymbol IS NULL OR h.SymbolRuleCode IS NULL;
END;
GO

/* Run the initial sync only after the date-safe procedure has replaced the
   old MERGE implementation. */
IF DB_ID(N'HRM') IS NOT NULL
    EXEC dbo.usp_SyncHrmHolidays;
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
