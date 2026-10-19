/*
89 - CHẶN DỮ LIỆU LOẠI NGÀY SAI QUAY LẠI
===============================================================================
Danh sách constraint là allowlist tương thích chuyển tiếp của database:
  NORMAL, COMPANY_SATURDAY (legacy), SUNDAY, COMPANY_HOLIDAY, NATIONAL_HOLIDAY, OTHER.
DayTypeCatalog là danh mục canonical cho UI; COMPANY_SATURDAY là legacy database value
được giữ trong giai đoạn chuyển đổi, không phải loại HRM chuẩn hiện tại.
SQL/84 có thể cho phép thêm COMPENSATORY trong constraint chuyển tiếp; SQL/89
chỉ được chạy sau khi SQL/88 đã dọn dữ liệu legacy và các truy vấn kiểm tra bên dưới
không còn dòng sai. Không chạy SQL/90 sau script này.
Idempotent.
===============================================================================
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.F03AttendanceSymbolRules
           WHERE DayType NOT IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'))
    THROW 51890, N'F03AttendanceSymbolRules còn DayType sai. Chạy lại 88 trước.', 1;
IF EXISTS (SELECT 1 FROM dbo.F03CompanyHolidays
           WHERE DayType IS NOT NULL AND DayType NOT IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER', N'COMPENSATORY'))
    THROW 51891, N'F03CompanyHolidays còn DayType sai. Chạy lại 88 trước.', 1;
IF EXISTS (SELECT 1 FROM dbo.F03HrmHolidayTypes
           WHERE DayType NOT IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER', N'COMPENSATORY'))
    THROW 51892, N'F03HrmHolidayTypes còn DayType sai (ánh xạ thủ công). Sửa lại ánh xạ trước.', 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_F03AttendanceSymbolRules_DayType')
    ALTER TABLE dbo.F03AttendanceSymbolRules WITH CHECK ADD CONSTRAINT CK_F03AttendanceSymbolRules_DayType
        CHECK (DayType IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_F03CompanyHolidays_DayType')
    ALTER TABLE dbo.F03CompanyHolidays WITH CHECK ADD CONSTRAINT CK_F03CompanyHolidays_DayType
        CHECK (DayType IS NULL OR DayType IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER', N'COMPENSATORY'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_F03HrmHolidayTypes_DayType')
    ALTER TABLE dbo.F03HrmHolidayTypes WITH CHECK ADD CONSTRAINT CK_F03HrmHolidayTypes_DayType
        CHECK (DayType IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER', N'COMPENSATORY'));
GO
PRINT N'Đã bật ràng buộc DayType cho F03AttendanceSymbolRules, F03CompanyHolidays, F03HrmHolidayTypes.';
GO
