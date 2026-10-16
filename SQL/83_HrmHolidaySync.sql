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
        /* NATIONAL_HOLIDAY | COMPANY_HOLIDAY | OTHER  (đúng 3 loại của HRM: L, CT, NT) */
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
/* Ký hiệu nghỉ lấy từ HRM (DM_Loainghile.LNLKyHieu: L, CT, NT...), tên loại và màu của HRM. */
IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'HolidaySymbol') IS NULL
    ALTER TABLE dbo.F03CompanyHolidays ADD HolidaySymbol nvarchar(50) NULL;
IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'HolidayTypeName') IS NULL
    ALTER TABLE dbo.F03CompanyHolidays ADD HolidayTypeName nvarchar(200) NULL;
IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'HolidayColor') IS NULL
    ALTER TABLE dbo.F03CompanyHolidays ADD HolidayColor varchar(50) NULL;
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

/* 4. Đồng bộ ngày nghỉ từ HRM -----------------------------------------------
   Procedure triển khai chính nằm ở migration 84. Không cài phiên bản MERGE cũ
   tại đây vì nó INSERT theo NNLMa và có thể vi phạm unique key HolidayDate khi
   HRM có nhiều mã nghỉ cùng một ngày hoặc FVN đã có dữ liệu ngày đó.
   Chạy SQL/84_FixHrmHolidaySyncAndAttendanceSymbolHistory.sql ngay sau script này.
*/
/* Chỉ tạo bản stub khi procedure CHƯA tồn tại. Không dùng CREATE OR ALTER ở đây:
   chạy lại script 83 sau khi đã cài 84 sẽ ghi đè procedure thật bằng bản THROW
   và làm usp_CalculateHrmAttendance (22_03) lỗi ở mọi lần tính công. */
IF OBJECT_ID(N'dbo.usp_SyncHrmHolidays', N'P') IS NULL
    EXEC (N'CREATE PROCEDURE dbo.usp_SyncHrmHolidays @DeleteMissing bit = 1
            AS BEGIN SET NOCOUNT ON;
               THROW 51482, N''Chưa cài migration 84. Hãy chạy SQL/84_FixHrmHolidaySyncAndAttendanceSymbolHistory.sql trước khi đồng bộ ngày nghỉ.'', 1;
            END');
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
        /* 4 loại ngày duy nhất (khớp HRM DM_Loainghile: 0002 CT, 0003 L, 0001 NT):
             1) ngày nghỉ HRM đăng ký  -> DayType của loại đó (CT=COMPANY_HOLIDAY, L=NATIONAL_HOLIDAY, NT=OTHER)
             2) cờ HRM BCNgayLe / BCNgayLeNV khi chưa đồng bộ kịp
             3) Chủ nhật mặc định     -> COMPANY_HOLIDAY (Chủ nhật/Nghỉ công ty)
             4) còn lại (T2-T7)       -> NORMAL. Thứ 7 là ngày làm việc thường. */
        DayType = COALESCE(
            (SELECT TOP (1) h.DayType
               FROM dbo.F03CompanyHolidays h
              WHERE h.HolidayDate = @WorkDate AND h.Source = N'HRM' AND h.IsActive = 1 ORDER BY h.Id),
            CASE WHEN ISNULL(@HrmHolidayFlag, 0) = 1 THEN N'NATIONAL_HOLIDAY'
                 WHEN ISNULL(@HrmEmployeeHolidayFlag, 0) = 1 THEN N'OTHER' END,
            CASE WHEN DATEDIFF(day, '19000107', @WorkDate) % 7 = 0 THEN N'COMPANY_HOLIDAY' END,
            N'NORMAL'),
        IsHoliday = CASE WHEN EXISTS (SELECT 1 FROM dbo.F03CompanyHolidays h WHERE h.HolidayDate = @WorkDate AND h.Source = N'HRM' AND h.IsActive = 1)
                           OR ISNULL(@HrmHolidayFlag, 0) = 1 OR ISNULL(@HrmEmployeeHolidayFlag, 0) = 1
                         THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
        /* Ký hiệu nghỉ theo HRM (LNLKyHieu). Cờ HRM không có loại (ngày nghỉ riêng nhân viên) thì dùng "L". */
        HolidaySymbol = COALESCE(
            (SELECT TOP (1) h.HolidaySymbol FROM dbo.F03CompanyHolidays h
              WHERE h.HolidayDate = @WorkDate AND h.Source = N'HRM' AND h.IsActive = 1 ORDER BY h.Id),
            CASE WHEN ISNULL(@HrmHolidayFlag, 0) = 1 OR ISNULL(@HrmEmployeeHolidayFlag, 0) = 1 THEN N'L' END)
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
                   (N'OTH-',  N'OTHER',           N'Nghỉ khác')) AS p(Prefix, DayType, Label)
WHERE r.DayType = N'NATIONAL_HOLIDAY' AND r.RuleCode LIKE N'HOL-%'
  AND NOT EXISTS (SELECT 1 FROM dbo.F03AttendanceSymbolRules x WHERE x.RuleCode = p.Prefix + SUBSTRING(r.RuleCode, 5, 100));
GO

/* 7. Đồng bộ lần đầu được chuyển sang migration 84.
   Migration 84 thay procedure MERGE theo mã bằng upsert theo HolidayDate,
   để không lỗi khi database đã có unique index ngày nghỉ hoặc dữ liệu FVN cũ. */
PRINT N'Holiday sync bootstrap is executed by 84_FixHrmHolidaySyncAndAttendanceSymbolHistory.sql.';
GO
