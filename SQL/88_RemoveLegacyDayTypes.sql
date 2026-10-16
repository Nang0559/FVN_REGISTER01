/*
88 - DỌN LOẠI NGÀY / KÝ HIỆU CŨ, CHỈ GIỮ 3 LOẠI NGHỈ CỦA HRM
===============================================================================
HRM DM_Loainghile chỉ có: 0002 CT (Chủ nhật/Nghỉ công ty), 0003 L (Ngày lễ), 0001 NT (Ngày nghỉ thường).
Script này:
  1) Sao lưu rồi xóa các dòng ngày nghỉ FVN cũ (Source = 'FVN', đã bị tắt khi đồng bộ HRM).
  2) Sao lưu rồi xóa MỌI rule ký hiệu có DayType ngoài 6 loại hợp lệ (gồm COMPENSATORY - Nghỉ bù, HRM không có loại này).
  2b) Xóa ký hiệu (WorkSymbol/OtSymbol/SymbolRuleCode) mà rule sinh ra không còn tồn tại, ở cả bảng tính công và lịch sử,
      để ký hiệu sai không còn hiển thị. Hãy TÍNH CÔNG LẠI các kỳ bị ảnh hưởng (hoặc lưu lại 1 rule) để rule hiện hành áp lại.
  3) Chuẩn hóa DayType cho dữ liệu tính công NGOÀI 12 tháng gần nhất (12 tháng gần nhất do
     "Đồng bộ từ HRM" trên trang work-calendar tính lại cùng với WorkSymbol/OtSymbol).
     Chỉ đổi DayType, không đụng ký hiệu và giờ công.
Chạy sau 83, 84, 87. Idempotent. Bảng sao lưu: F03CompanyHolidays_FvnLegacyBackup,
F03AttendanceSymbolRules_RemovedBackup.
===============================================================================
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* 1. Ngày nghỉ FVN cũ ------------------------------------------------------ */
IF OBJECT_ID(N'dbo.F03CompanyHolidays_FvnLegacyBackup', N'U') IS NULL
    SELECT TOP (0) * INTO dbo.F03CompanyHolidays_FvnLegacyBackup FROM dbo.F03CompanyHolidays;
GO
BEGIN TRANSACTION;
    INSERT dbo.F03CompanyHolidays_FvnLegacyBackup
    SELECT h.* FROM dbo.F03CompanyHolidays AS h
    WHERE h.Source <> N'HRM'
      AND NOT EXISTS (SELECT 1 FROM dbo.F03CompanyHolidays_FvnLegacyBackup b WHERE b.Id = h.Id);

    DELETE FROM dbo.F03CompanyHolidays WHERE Source <> N'HRM';
    PRINT CONCAT(N'Đã xóa ngày nghỉ FVN cũ: ', @@ROWCOUNT);
COMMIT TRANSACTION;
GO

/* 2. Rule ký hiệu có DayType không hợp lệ (gồm Nghỉ bù) ----------------------------------------- */
IF OBJECT_ID(N'dbo.F03AttendanceSymbolRules_RemovedBackup', N'U') IS NULL
    SELECT TOP (0) * INTO dbo.F03AttendanceSymbolRules_RemovedBackup FROM dbo.F03AttendanceSymbolRules;
GO
BEGIN TRANSACTION;
    INSERT dbo.F03AttendanceSymbolRules_RemovedBackup
    SELECT r.* FROM dbo.F03AttendanceSymbolRules AS r
    WHERE r.DayType NOT IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER')
      AND NOT EXISTS (SELECT 1 FROM dbo.F03AttendanceSymbolRules_RemovedBackup b WHERE b.Id = r.Id);

    DELETE FROM dbo.F03AttendanceSymbolRules WHERE DayType NOT IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER');
    PRINT CONCAT(N'Đã xóa rule có DayType không hợp lệ: ', @@ROWCOUNT);
COMMIT TRANSACTION;
GO

/* 2b. Ký hiệu do rule không còn tồn tại (rule Nghỉ bù vừa xóa, hoặc rule bị xóa từ trước) ---- */
DECLARE @rows int = 1;
WHILE @rows > 0
BEGIN
    UPDATE TOP (5000) a
       SET a.WorkSymbol = NULL, a.OtSymbol = NULL, a.SymbolRuleCode = NULL
      FROM dbo.F03HrmAttendanceCalculated AS a
     WHERE a.SymbolRuleCode IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.F03AttendanceSymbolRules r WHERE r.RuleCode = a.SymbolRuleCode);
    SET @rows = @@ROWCOUNT;
END;
IF OBJECT_ID(N'dbo.F03HrmAttendanceHistory', N'U') IS NOT NULL
BEGIN
    SET @rows = 1;
    WHILE @rows > 0
    BEGIN
        UPDATE TOP (5000) h
           SET h.WorkSymbol = NULL, h.OtSymbol = NULL, h.SymbolRuleCode = NULL
          FROM dbo.F03HrmAttendanceHistory AS h
         WHERE h.SymbolRuleCode IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM dbo.F03AttendanceSymbolRules r WHERE r.RuleCode = h.SymbolRuleCode);
        SET @rows = @@ROWCOUNT;
    END;
END;
GO

/* 3. DayType không hợp lệ ở mọi dữ liệu tính công/lịch sử --------------------------- */
DECLARE @rows int = 1;
WHILE @rows > 0
BEGIN
    UPDATE TOP (5000) a
       SET a.DayType = ds.DayType
      FROM dbo.F03HrmAttendanceCalculated AS a
     CROSS APPLY dbo.fn_ResolveDayType(a.WorkDate, a.HrmBCNgayLe, a.HrmBCNgayLeNV) AS ds
     WHERE (a.DayType IS NULL OR a.DayType = N'COMPENSATORY'
            OR a.DayType NOT IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'));
    SET @rows = @@ROWCOUNT;
END;

IF OBJECT_ID(N'dbo.F03HrmAttendanceHistory', N'U') IS NOT NULL
BEGIN
    SET @rows = 1;
    WHILE @rows > 0
    BEGIN
        UPDATE TOP (5000) h
           SET h.DayType = ds.DayType
          FROM dbo.F03HrmAttendanceHistory AS h
         CROSS APPLY dbo.fn_ResolveDayType(h.WorkDate, h.HrmBCNgayLe, h.HrmBCNgayLeNV) AS ds
         WHERE h.DayType IS NULL OR h.DayType = N'COMPENSATORY'
            OR h.DayType NOT IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER');
        SET @rows = @@ROWCOUNT;
    END;
END;
GO

/* Kiểm tra: kết quả mong đợi là 0 dòng ở 2 truy vấn đầu --------------------- */
SELECT DayType, COUNT(*) AS Cnt FROM dbo.F03HrmAttendanceCalculated
WHERE DayType IS NULL OR DayType NOT IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER')
GROUP BY DayType;
SELECT DayType, COUNT(*) AS Cnt FROM dbo.F03AttendanceSymbolRules
WHERE DayType NOT IN (N'NORMAL', N'COMPANY_SATURDAY', N'SUNDAY', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER')
GROUP BY DayType;
SELECT Source, COUNT(*) AS Cnt FROM dbo.F03CompanyHolidays GROUP BY Source;
GO
