/*
===============================================================================
FVN_REGISTER - Ngày nghỉ lấy từ HRM làm nguồn duy nhất + nối rule ký hiệu vào luồng tính công
===============================================================================
Nguồn chuẩn:
  HRM.dbo.DM_Loainghile  (LNLMa, LNLTen, LNLLoai, LNLKyHieu, LNLMauSac, DLocked)
  HRM.dbo.tblNgayNghiLe  (NNLMa, NNLNgay, NNLTen, NNLLoai, DLocked)

FVN không tự định nghĩa ngày nghỉ nữa:
  * F03HrmHolidayTypes  = bản sao DM_Loainghile + cột DayType (ánh xạ sang DayType của rule ký hiệu).
  * F03CompanyHolidays  = bản sao tblNgayNghiLe (Source='HRM'); mọi consumer cũ (lịch, nghỉ phép,
                          thống kê, xuất Excel) tiếp tục đọc bảng này nên không phải sửa SQL từng nơi.
  * dbo.usp_SyncHrmHolidays  đồng bộ 2 bảng trên (được usp_CalculateHrmAttendance gọi tự động).
  * dbo.fn_ResolveDayType    một hàm duy nhất quyết định DayType của một ngày.

Chạy sau 66, 67 và 22_00. Idempotent.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* 1. Danh mục loại ngày nghỉ HRM + ánh xạ DayType ------------------------- */
IF OBJECT_ID(N'dbo.F03HrmHolidayTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03HrmHolidayTypes
    (
        LNLMa      varchar(5)    NOT NULL CONSTRAINT PK_F03HrmHolidayTypes PRIMARY KEY,
        LNLTen     nvarchar(200) NOT NULL,
        LNLLoai    varchar(10)   NOT NULL,
        LNLKyHieu  nvarchar(50)  NULL,
        LNLMauSac  varchar(50)   NULL,
        DLocked    bit           NOT NULL CONSTRAINT DF_F03HrmHolidayTypes_DLocked DEFAULT(0),
        /* NATIONAL_HOLIDAY | COMPANY_HOLIDAY | COMPENSATORY | OTHER */
        DayType    nvarchar(40)  NOT NULL CONSTRAINT DF_F03HrmHolidayTypes_DayType DEFAULT(N'NATIONAL_HOLIDAY'),
        DayTypeIsManual bit      NOT NULL CONSTRAINT DF_F03HrmHolidayTypes_Manual DEFAULT(0),
        SyncedAt   datetime2(0)  NOT NULL CONSTRAINT DF_F03HrmHolidayTypes_SyncedAt DEFAULT(SYSUTCDATETIME())
    );
END;
GO

/* 2. F03CompanyHolidays trở thành bảng đồng bộ ------------------------------ */
IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'Source') IS NULL
    ALTER TABLE dbo.F03CompanyHolidays ADD Source nvarchar(20) NOT NULL
        CONSTRAINT DF_F03CompanyHolidays_Source DEFAULT(N'FVN') WITH VALUES;
IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'HrmHolidayCode') IS NULL
    ALTER TABLE dbo.F03CompanyHolidays ADD HrmHolidayCode varchar(10) NULL;
IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'HrmTypeCode') IS NULL
    ALTER TABLE dbo.F03CompanyHolidays ADD HrmTypeCode varchar(5) NULL;
IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'DayType') IS NULL
    ALTER TABLE dbo.F03CompanyHolidays ADD DayType nvarchar(40) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_F03CompanyHolidays_HrmHolidayCode'
               AND object_id = OBJECT_ID(N'dbo.F03CompanyHolidays'))
    CREATE UNIQUE INDEX UX_F03CompanyHolidays_HrmHolidayCode
        ON dbo.F03CompanyHolidays(HrmHolidayCode) WHERE HrmHolidayCode IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03CompanyHolidays_HolidayDate'
               AND object_id = OBJECT_ID(N'dbo.F03CompanyHolidays'))
    CREATE INDEX IX_F03CompanyHolidays_HolidayDate ON dbo.F03CompanyHolidays(HolidayDate) INCLUDE (HolidayType, DayType, Source);
GO

/* 3. Cột kết quả ký hiệu trên bảng tính công ------------------------------- */
DECLARE @T TABLE (Tbl sysname);
INSERT @T VALUES (N'F03HrmAttendanceCalculated');
IF OBJECT_ID(N'dbo.F03HrmAttendanceHistory', N'U') IS NOT NULL INSERT @T VALUES (N'F03HrmAttendanceHistory');
DECLARE @tbl sysname, @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Tbl FROM @T;
OPEN c; FETCH NEXT FROM c INTO @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'
    IF COL_LENGTH(N''dbo.' + @tbl + N''', N''DayType'') IS NULL ALTER TABLE dbo.' + @tbl + N' ADD DayType nvarchar(40) NULL;
    IF COL_LENGTH(N''dbo.' + @tbl + N''', N''WorkSymbol'') IS NULL ALTER TABLE dbo.' + @tbl + N' ADD WorkSymbol nvarchar(50) NULL;
    IF COL_LENGTH(N''dbo.' + @tbl + N''', N''OtSymbol'') IS NULL ALTER TABLE dbo.' + @tbl + N' ADD OtSymbol nvarchar(50) NULL;
    IF COL_LENGTH(N''dbo.' + @tbl + N''', N''SymbolRuleCode'') IS NULL ALTER TABLE dbo.' + @tbl + N' ADD SymbolRuleCode nvarchar(100) NULL;';
    EXEC sys.sp_executesql @sql;
    FETCH NEXT FROM c INTO @tbl;
END;
CLOSE c; DEALLOCATE c;
GO

/* 4. Đồng bộ ngày nghỉ từ HRM ----------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_SyncHrmHolidays
    @DeleteMissing bit = 1
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @TypesUpserted int = 0, @HolidaysUpserted int = 0, @HolidaysDeleted int = 0;

    BEGIN TRANSACTION;

    /* 4.1 Loại ngày nghỉ. DayType chỉ được đoán khi dòng MỚI hoặc chưa sửa tay (DayTypeIsManual=0).
           Đoán theo tên loại; nếu sai, sửa trực tiếp F03HrmHolidayTypes.DayType + DayTypeIsManual=1. */
    ;WITH src AS
    (
        SELECT LNLMa = RTRIM(t.LNLMa), LNLTen = t.LNLTen, LNLLoai = RTRIM(t.LNLLoai), t.LNLKyHieu, t.LNLMauSac,
               DLocked = ISNULL(t.DLocked, 0),
               GuessDayType = CASE
                   WHEN LOWER(t.LNLTen) LIKE N'%bù%'                           THEN N'COMPENSATORY'
                   WHEN LOWER(t.LNLTen) LIKE N'%công ty%'                      THEN N'COMPANY_HOLIDAY'
                   WHEN LOWER(t.LNLTen) LIKE N'%lễ%' OR LOWER(t.LNLTen) LIKE N'%tết%'
                     OR LOWER(t.LNLTen) LIKE N'%quốc%'                         THEN N'NATIONAL_HOLIDAY'
                   ELSE N'OTHER' END
        FROM HRM.dbo.DM_Loainghile AS t
    )
    MERGE dbo.F03HrmHolidayTypes AS d
    USING src AS s ON d.LNLMa = s.LNLMa
    WHEN MATCHED THEN UPDATE SET
        d.LNLTen = s.LNLTen, d.LNLLoai = s.LNLLoai, d.LNLKyHieu = s.LNLKyHieu, d.LNLMauSac = s.LNLMauSac,
        d.DLocked = s.DLocked, d.SyncedAt = SYSUTCDATETIME(),
        d.DayType = CASE WHEN d.DayTypeIsManual = 1 THEN d.DayType ELSE s.GuessDayType END
    WHEN NOT MATCHED BY TARGET THEN INSERT (LNLMa, LNLTen, LNLLoai, LNLKyHieu, LNLMauSac, DLocked, DayType)
        VALUES (s.LNLMa, s.LNLTen, s.LNLLoai, s.LNLKyHieu, s.LNLMauSac, s.DLocked, s.GuessDayType);
    SET @TypesUpserted = @@ROWCOUNT;

    /* 4.2 Ngày nghỉ. tblNgayNghiLe.NNLLoai nối DM_Loainghile.LNLLoai (cùng cách HRM tự nối). */
    ;WITH src AS
    (
        SELECT HrmCode = RTRIM(n.NNLMa),
               HolidayDate = CAST(n.NNLNgay AS date),
               Description = LEFT(n.NNLTen, 200),
               TypeCode = RTRIM(n.NNLLoai),
               DayType = ISNULL(t.DayType, N'NATIONAL_HOLIDAY'),
               HolidayType = CASE ISNULL(t.DayType, N'NATIONAL_HOLIDAY')
                                 WHEN N'COMPANY_HOLIDAY' THEN 1 WHEN N'NATIONAL_HOLIDAY' THEN 2
                                 WHEN N'COMPENSATORY' THEN 3 ELSE 4 END,
               rn = ROW_NUMBER() OVER (PARTITION BY RTRIM(n.NNLMa) ORDER BY t.LNLMa)
        FROM HRM.dbo.tblNgayNghiLe AS n
        LEFT JOIN dbo.F03HrmHolidayTypes AS t ON t.LNLLoai = RTRIM(n.NNLLoai) AND t.DLocked = 0
        WHERE ISNULL(n.DLocked, 0) = 0
    )
    MERGE dbo.F03CompanyHolidays AS d
    USING (SELECT * FROM src WHERE rn = 1) AS s ON d.HrmHolidayCode = s.HrmCode
    WHEN MATCHED THEN UPDATE SET
        d.HolidayDate = s.HolidayDate, d.Description = s.Description, d.Year = YEAR(s.HolidayDate),
        d.HrmTypeCode = s.TypeCode, d.DayType = s.DayType, d.HolidayType = s.HolidayType,
        d.Source = N'HRM', d.IsActive = 1, d.ModifiedBy = 0, d.ModifiedAt = GETDATE(), d.LastModifiedSource = N'HRM_SYNC'
    WHEN NOT MATCHED BY TARGET THEN INSERT
        (IsActive, CreatedBy, LastModifiedSource, CreatedAt, HolidayDate, Description, Year, TinhPhep, HolidayType,
         Source, HrmHolidayCode, HrmTypeCode, DayType)
        VALUES (1, 0, N'HRM_SYNC', GETDATE(), s.HolidayDate, s.Description, YEAR(s.HolidayDate), 1, s.HolidayType,
                N'HRM', s.HrmCode, s.TypeCode, s.DayType);
    SET @HolidaysUpserted = @@ROWCOUNT;

    IF @DeleteMissing = 1
    BEGIN
        DELETE d FROM dbo.F03CompanyHolidays AS d
        WHERE d.Source = N'HRM'
          AND NOT EXISTS (SELECT 1 FROM HRM.dbo.tblNgayNghiLe AS n
                          WHERE RTRIM(n.NNLMa) = d.HrmHolidayCode AND ISNULL(n.DLocked, 0) = 0);
        SET @HolidaysDeleted = @@ROWCOUNT;
    END;

    COMMIT TRANSACTION;

    SELECT TypesUpserted = @TypesUpserted, HolidaysUpserted = @HolidaysUpserted, HolidaysDeleted = @HolidaysDeleted,
           HolidayCount = (SELECT COUNT(*) FROM dbo.F03CompanyHolidays WHERE Source = N'HRM');
END;
GO

/* 5. Một hàm duy nhất quyết định DayType -------------------------------------
   Thứ tự: ngày nghỉ HRM > cờ HRM (BCNgayLe / BCNgayLeNV) > Chủ nhật > Thứ 7 > NORMAL.
   Chủ nhật/Thứ 7 xác định bằng DATEDIFF để không phụ thuộc SET DATEFIRST.       */
CREATE OR ALTER FUNCTION dbo.fn_ResolveDayType
(
    @WorkDate date,
    @HrmHolidayFlag bit,
    @HrmEmployeeHolidayFlag bit
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        DayType = COALESCE(
            (SELECT TOP (1) h.DayType FROM dbo.F03CompanyHolidays h
              WHERE h.HolidayDate = @WorkDate AND h.Source = N'HRM' AND h.IsActive = 1 ORDER BY h.Id),
            CASE WHEN ISNULL(@HrmHolidayFlag, 0) = 1 THEN N'NATIONAL_HOLIDAY'
                 WHEN ISNULL(@HrmEmployeeHolidayFlag, 0) = 1 THEN N'OTHER' END,
            CASE DATEDIFF(day, '19000107', @WorkDate) % 7 WHEN 0 THEN N'SUNDAY' WHEN 6 THEN N'COMPANY_SATURDAY' END,
            N'NORMAL'),
        IsHoliday = CASE WHEN EXISTS (SELECT 1 FROM dbo.F03CompanyHolidays h WHERE h.HolidayDate = @WorkDate AND h.Source = N'HRM' AND h.IsActive = 1)
                           OR ISNULL(@HrmHolidayFlag, 0) = 1 OR ISNULL(@HrmEmployeeHolidayFlag, 0) = 1
                         THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
);
GO

/* 6. Seed rule cho các DayType còn thiếu (copy từ rule lễ quốc gia, sửa lại trên trang rule) */
INSERT dbo.F03AttendanceSymbolRules
    (RuleCode, RuleName, DayType, ShiftCode, ShiftId, Priority, MinActualMinutes, MaxActualMinutes,
     CheckInFrom, CheckInTo, EffectiveFrom, EffectiveTo, RuleJson, IsActive, CreatedBy, CreatedAt, LastModifiedSource)
SELECT p.Prefix + SUBSTRING(r.RuleCode, 5, 100),
       REPLACE(r.RuleName, N'Lễ quốc gia', p.Label), p.DayType, r.ShiftCode, r.ShiftId, r.Priority,
       r.MinActualMinutes, r.MaxActualMinutes, r.CheckInFrom, r.CheckInTo, r.EffectiveFrom, r.EffectiveTo,
       r.RuleJson, 1, 0, SYSUTCDATETIME(), N'SEED'
FROM dbo.F03AttendanceSymbolRules AS r
CROSS JOIN (VALUES (N'CHOL-', N'COMPANY_HOLIDAY', N'Nghỉ công ty'),
                   (N'COMP-', N'COMPENSATORY',    N'Nghỉ bù'),
                   (N'OTH-',  N'OTHER',           N'Nghỉ khác')) AS p(Prefix, DayType, Label)
WHERE r.DayType = N'NATIONAL_HOLIDAY' AND r.RuleCode LIKE N'HOL-%'
  AND NOT EXISTS (SELECT 1 FROM dbo.F03AttendanceSymbolRules x WHERE x.RuleCode = p.Prefix + SUBSTRING(r.RuleCode, 5, 100));
GO

/* 7. Đồng bộ lần đầu được chuyển sang migration 84.
   Migration 84 thay procedure MERGE theo mã bằng upsert theo HolidayDate,
   để không lỗi khi database đã có unique index ngày nghỉ hoặc dữ liệu FVN cũ. */
PRINT N'Holiday sync bootstrap is executed by 84_FixHrmHolidaySyncAndAttendanceSymbolHistory.sql.';
GO
