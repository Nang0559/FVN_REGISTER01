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
/* Bảng sao lưu được tạo bằng SELECT ... INTO nên có thể mang theo cột IDENTITY:
   phải INSERT có danh sách cột và bật IDENTITY_INSERT, không dùng "SELECT *". */
BEGIN TRANSACTION;
    DECLARE @cols nvarchar(max), @sql nvarchar(max), @hasIdentity bit;
    SELECT @cols = STUFF((SELECT N',' + QUOTENAME(c.name)
      FROM sys.columns AS c
      JOIN sys.columns AS b ON b.object_id = OBJECT_ID(N'dbo.F03CompanyHolidays_FvnLegacyBackup') AND b.name = c.name
     WHERE c.object_id = OBJECT_ID(N'dbo.F03CompanyHolidays') AND c.is_computed = 0
     ORDER BY c.column_id FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 1, N'');
    SET @hasIdentity = CASE WHEN OBJECTPROPERTY(OBJECT_ID(N'dbo.F03CompanyHolidays_FvnLegacyBackup'), 'TableHasIdentity') = 1 THEN 1 ELSE 0 END;
    SET @sql = CASE WHEN @hasIdentity = 1 THEN N'SET IDENTITY_INSERT dbo.F03CompanyHolidays_FvnLegacyBackup ON; ' ELSE N'' END
             + N'INSERT dbo.F03CompanyHolidays_FvnLegacyBackup (' + @cols + N') SELECT ' + @cols
             + N' FROM dbo.F03CompanyHolidays AS h WHERE h.Source <> N''HRM'''
             + N' AND NOT EXISTS (SELECT 1 FROM dbo.F03CompanyHolidays_FvnLegacyBackup AS b WHERE b.Id = h.Id);'
             + CASE WHEN @hasIdentity = 1 THEN N' SET IDENTITY_INSERT dbo.F03CompanyHolidays_FvnLegacyBackup OFF;' ELSE N'' END;
    EXEC sys.sp_executesql @sql;

    DELETE FROM dbo.F03CompanyHolidays WHERE Source <> N'HRM';
    PRINT CONCAT(N'Đã xóa ngày nghỉ FVN cũ: ', @@ROWCOUNT);
COMMIT TRANSACTION;
GO

/* 2. Rule ký hiệu có DayType không hợp lệ (gồm Nghỉ bù) ---------------------- */
IF OBJECT_ID(N'dbo.F03AttendanceSymbolRules_RemovedBackup', N'U') IS NULL
    SELECT TOP (0) * INTO dbo.F03AttendanceSymbolRules_RemovedBackup FROM dbo.F03AttendanceSymbolRules;
GO
/* Bảng sao lưu được tạo bằng SELECT ... INTO nên có thể mang theo cột IDENTITY:
   phải INSERT có danh sách cột và bật IDENTITY_INSERT, không dùng "SELECT *". */
BEGIN TRANSACTION;
    DECLARE @cols nvarchar(max), @sql nvarchar(max), @hasIdentity bit;
    SELECT @cols = STUFF((SELECT N',' + QUOTENAME(c.name)
      FROM sys.columns AS c
      JOIN sys.columns AS b ON b.object_id = OBJECT_ID(N'dbo.F03AttendanceSymbolRules_RemovedBackup') AND b.name = c.name
     WHERE c.object_id = OBJECT_ID(N'dbo.F03AttendanceSymbolRules') AND c.is_computed = 0
     ORDER BY c.column_id FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 1, N'');
    SET @hasIdentity = CASE WHEN OBJECTPROPERTY(OBJECT_ID(N'dbo.F03AttendanceSymbolRules_RemovedBackup'), 'TableHasIdentity') = 1 THEN 1 ELSE 0 END;
    SET @sql = CASE WHEN @hasIdentity = 1 THEN N'SET IDENTITY_INSERT dbo.F03AttendanceSymbolRules_RemovedBackup ON; ' ELSE N'' END
             + N'INSERT dbo.F03AttendanceSymbolRules_RemovedBackup (' + @cols + N') SELECT ' + @cols
             + N' FROM dbo.F03AttendanceSymbolRules AS h WHERE h.DayType NOT IN (N''NORMAL'', N''COMPANY_SATURDAY'', N''SUNDAY'', N''COMPANY_HOLIDAY'', N''NATIONAL_HOLIDAY'', N''OTHER'')'
             + N' AND NOT EXISTS (SELECT 1 FROM dbo.F03AttendanceSymbolRules_RemovedBackup AS b WHERE b.Id = h.Id);'
             + CASE WHEN @hasIdentity = 1 THEN N' SET IDENTITY_INSERT dbo.F03AttendanceSymbolRules_RemovedBackup OFF;' ELSE N'' END;
    EXEC sys.sp_executesql @sql;

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
