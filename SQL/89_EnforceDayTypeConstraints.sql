/*
89 - CHẶN DỮ LIỆU LOẠI NGÀY SAI QUAY LẠI
===============================================================================
Danh sách hợp lệ PHẢI trùng FVN_REGISTER.Contract.Dtos.Calendar.DayTypeCatalog.Codes:
  NORMAL, COMPANY_SATURDAY, SUNDAY, COMPANY_HOLIDAY, NATIONAL_HOLIDAY, OTHER
Chạy SAU 88 (88 đã xóa dữ liệu sai). Nếu còn dữ liệu sai, script dừng và báo rõ bảng nào.
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
           WHERE DayType IS NOT NULL AND DayType NOT IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'))
    THROW 51891, N'F03CompanyHolidays còn DayType sai. Chạy lại 88 trước.', 1;
IF EXISTS (SELECT 1 FROM dbo.F03HrmHolidayTypes
           WHERE DayType NOT IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'))
    THROW 51892, N'F03HrmHolidayTypes còn DayType sai (ánh xạ thủ công). Sửa lại ánh xạ trước.', 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_F03AttendanceSymbolRules_DayType')
    ALTER TABLE dbo.F03AttendanceSymbolRules WITH CHECK ADD CONSTRAINT CK_F03AttendanceSymbolRules_DayType
        CHECK (DayType IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_F03CompanyHolidays_DayType')
    ALTER TABLE dbo.F03CompanyHolidays WITH CHECK ADD CONSTRAINT CK_F03CompanyHolidays_DayType
        CHECK (DayType IS NULL OR DayType IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_F03HrmHolidayTypes_DayType')
    ALTER TABLE dbo.F03HrmHolidayTypes WITH CHECK ADD CONSTRAINT CK_F03HrmHolidayTypes_DayType
        CHECK (DayType IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'));
GO
PRINT N'Đã bật ràng buộc DayType cho F03AttendanceSymbolRules, F03CompanyHolidays, F03HrmHolidayTypes.';
GO
