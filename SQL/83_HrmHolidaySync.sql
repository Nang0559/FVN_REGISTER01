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
        /* HRM 001 = nghỉ công ty T2-T7; 002 = Chủ nhật; 003 = nghỉ lễ.
           Cờ HRM là fallback khi chưa có bản ghi đồng bộ. */
        DayType = COALESCE(
            (SELECT TOP (1) h.DayType
               FROM dbo.F03CompanyHolidays h
              WHERE h.HolidayDate = @WorkDate AND h.Source = N'HRM' AND h.IsActive = 1
              ORDER BY CASE h.DayType WHEN N'NATIONAL_HOLIDAY' THEN 1 WHEN N'SUNDAY' THEN 2 WHEN N'COMPANY_HOLIDAY' THEN 3 ELSE 4 END, h.Id),
            CASE WHEN ISNULL(@HrmHolidayFlag, 0) = 1 THEN N'NATIONAL_HOLIDAY'
                 WHEN ISNULL(@HrmEmployeeHolidayFlag, 0) = 1 THEN N'COMPANY_HOLIDAY' END,
            CASE WHEN DATEDIFF(day, '19000107', @WorkDate) % 7 = 0 THEN N'SUNDAY' END,
            N'NORMAL'),
        IsHoliday = CASE WHEN EXISTS (SELECT 1 FROM dbo.F03CompanyHolidays h WHERE h.HolidayDate = @WorkDate AND h.Source = N'HRM' AND h.IsActive = 1)
                           OR ISNULL(@HrmHolidayFlag, 0) = 1 OR ISNULL(@HrmEmployeeHolidayFlag, 0) = 1
                         THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
        /* Ký hiệu nghỉ theo HRM (LNLKyHieu); chỉ cờ ngày lễ fallback thành L. */
        HolidaySymbol = COALESCE(
            (SELECT TOP (1) h.HolidaySymbol FROM dbo.F03CompanyHolidays h
              WHERE h.HolidayDate = @WorkDate AND h.Source = N'HRM' AND h.IsActive = 1 ORDER BY h.Id),
            CASE WHEN ISNULL(@HrmHolidayFlag, 0) = 1 THEN N'L' END)
);
GO

/* 6. Rule ký hiệu: bộ seed chuẩn 4 loại ngày nằm ở SQL/67_AttendanceSymbolRules.sql (không nhân bản rule Lễ nữa). */
GO

/* 7. Đồng bộ lần đầu được chuyển sang migration 84.
   Migration 84 thay procedure MERGE theo mã bằng upsert theo HolidayDate,
   để không lỗi khi database đã có unique index ngày nghỉ hoặc dữ liệu FVN cũ. */
PRINT N'Holiday sync bootstrap is executed by 84_FixHrmHolidaySyncAndAttendanceSymbolHistory.sql.';
GO
