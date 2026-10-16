/*
===============================================================================
90 - CHUẨN HOÁ LOẠI NGÀY VỀ ĐÚNG 4 LOẠI THEO HRM
===============================================================================
HRM DM_Loainghile chỉ có 3 loại nghỉ: 0002 CT (Chủ nhật/Nghỉ công ty), 0003 L (Ngày lễ), 0001 NT (Ngày nghỉ thường).
Quy ước ký hiệu (khớp bảng hệ số):
  NORMAL           T2-T7 (kể cả Thứ 7)             OT: K..                 hệ số 150%
  COMPANY_HOLIDAY  CT / Chủ nhật mặc định          công/OT: CN.. CNK.. CNC2.. CNC3..   200%
  NATIONAL_HOLIDAY L  (Ngày lễ)                    công/OT: NL.. NLK.. NLC2.. NLC3..   300%
  OTHER            NT (ngày thường cho nghỉ)       công/OT: T.. TK.. T2.. T3..         200%
Bỏ hẳn 2 loại tự suy ra từ thứ trong tuần: SUNDAY và COMPANY_SATURDAY.

Script này:
  0) sao lưu F03AttendanceSymbolRules -> F03AttendanceSymbolRules_Pre90Backup
  1) bỏ 3 CHECK constraint DayType cũ
  2) xoá rule nhân bản sai (CHOL-/COMP-/OTH-: copy của rule Lễ do SQL/83 sinh ra)
     đổi rule SUN-* -> CT-* (COMPANY_HOLIDAY), SAT-* -> NT-* (OTHER), HOL-* đổi tên; sửa NORMAL-C2 dùng K{hours}
  3) đổi DayType cũ trong F03HrmHolidayTypes / F03CompanyHolidays
  4) dữ liệu tính công KỲ ĐANG MỞ: tính lại DayType bằng dbo.fn_ResolveDayType và xoá ký hiệu rule đã hết hiệu lực
     (kỳ đã khoá/lịch sử: chỉ chuẩn hoá DayType cho dòng CHƯA có ký hiệu, không đụng ký hiệu đã ghi)
  5) bật lại CHECK constraint với đúng 4 loại
Chạy SAU 83, 84, 87, 88, 89. Idempotent.
Sau khi chạy: LƯU LẠI 1 rule trên /admin/attendance-symbol-rules (hệ thống áp lại ký hiệu) và tính lại công kỳ đang mở
để AttendanceDisplayValue của các ngày Thứ 7/Chủ nhật được dựng lại từ đầu.
===============================================================================
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* 0. Sao lưu -------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.F03AttendanceSymbolRules_Pre90Backup', N'U') IS NULL
    SELECT * INTO dbo.F03AttendanceSymbolRules_Pre90Backup FROM dbo.F03AttendanceSymbolRules;
GO

/* 1. Bỏ constraint cũ ------------------------------------------------------ */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_F03AttendanceSymbolRules_DayType')
    ALTER TABLE dbo.F03AttendanceSymbolRules DROP CONSTRAINT CK_F03AttendanceSymbolRules_DayType;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_F03CompanyHolidays_DayType')
    ALTER TABLE dbo.F03CompanyHolidays DROP CONSTRAINT CK_F03CompanyHolidays_DayType;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_F03HrmHolidayTypes_DayType')
    ALTER TABLE dbo.F03HrmHolidayTypes DROP CONSTRAINT CK_F03HrmHolidayTypes_DayType;
GO

/* 2. Rule ký hiệu ---------------------------------------------------------- */
BEGIN TRANSACTION;

DELETE FROM dbo.F03AttendanceSymbolRules
 WHERE RuleCode LIKE N'CHOL-%' OR RuleCode LIKE N'COMP-%' OR RuleCode LIKE N'OTH-%'
    OR DayType IN (N'COMPANY_HOLIDAY', N'OTHER', N'COMPENSATORY');
PRINT CONCAT(N'Đã xoá rule nhân bản/sai loại: ', @@ROWCOUNT);

UPDATE dbo.F03AttendanceSymbolRules
   SET DayType  = N'COMPANY_HOLIDAY',
       RuleCode = CASE WHEN RuleCode LIKE N'SUN-%' THEN N'CT-' + SUBSTRING(RuleCode, 5, 100) ELSE RuleCode END,
       RuleName = REPLACE(RuleName, N'Chủ nhật', N'Chủ nhật / Nghỉ công ty (CT)'),
       ModifiedAt = SYSUTCDATETIME(), LastModifiedSource = N'MIGRATION_90'
 WHERE DayType = N'SUNDAY'
   AND NOT EXISTS (SELECT 1 FROM dbo.F03AttendanceSymbolRules x
                   WHERE x.RuleCode = N'CT-' + SUBSTRING(F03AttendanceSymbolRules.RuleCode, 5, 100) AND F03AttendanceSymbolRules.RuleCode LIKE N'SUN-%');

UPDATE dbo.F03AttendanceSymbolRules
   SET DayType  = N'OTHER',
       RuleCode = CASE WHEN RuleCode LIKE N'SAT-%' THEN N'NT-' + SUBSTRING(RuleCode, 5, 100) ELSE RuleCode END,
       RuleName = REPLACE(RuleName, N'Thứ 7 nghỉ công ty', N'Ngày nghỉ thường (NT)'),
       ModifiedAt = SYSUTCDATETIME(), LastModifiedSource = N'MIGRATION_90'
 WHERE DayType = N'COMPANY_SATURDAY'
   AND NOT EXISTS (SELECT 1 FROM dbo.F03AttendanceSymbolRules x
                   WHERE x.RuleCode = N'NT-' + SUBSTRING(F03AttendanceSymbolRules.RuleCode, 5, 100) AND F03AttendanceSymbolRules.RuleCode LIKE N'SAT-%');

UPDATE dbo.F03AttendanceSymbolRules
   SET RuleName = REPLACE(RuleName, N'Lễ quốc gia', N'Ngày lễ (L)'), ModifiedAt = SYSUTCDATETIME(), LastModifiedSource = N'MIGRATION_90'
 WHERE DayType = N'NATIONAL_HOLIDAY' AND RuleName LIKE N'%Lễ quốc gia%';

/* Ngày thường: OT ca C2 đang sinh ký hiệu trần "{hours}" -> phải là K{hours} như các ca khác. */
UPDATE dbo.F03AttendanceSymbolRules
   SET RuleJson = REPLACE(RuleJson, N'"symbolTemplate":"{hours}"', N'"symbolTemplate":"K{hours}"'),
       ModifiedAt = SYSUTCDATETIME(), LastModifiedSource = N'MIGRATION_90'
 WHERE RuleCode = N'NORMAL-C2' AND RuleJson LIKE N'%"symbolTemplate":"{hours}"%';

COMMIT TRANSACTION;
GO

/* 3. Danh mục loại nghỉ HRM + ngày nghỉ ------------------------------------ */
UPDATE dbo.F03HrmHolidayTypes SET DayType = N'COMPANY_HOLIDAY', DayTypeIsManual = 1
 WHERE DayType IN (N'SUNDAY', N'COMPANY_SATURDAY', N'COMPENSATORY');
UPDATE dbo.F03CompanyHolidays SET DayType = N'COMPANY_HOLIDAY'
 WHERE DayType IN (N'SUNDAY', N'COMPANY_SATURDAY', N'COMPENSATORY');
GO

/* 4. Dữ liệu tính công ----------------------------------------------------- */
DECLARE @rows int = 1;
/* 4a. Kỳ đang mở (F03HrmAttendanceCalculated) */
WHILE @rows > 0
BEGIN
    UPDATE TOP (5000) a
       SET a.DayType = ds.DayType,
           a.WorkSymbol = NULL, a.OtSymbol = NULL, a.SymbolRuleCode = NULL,
           a.OtDisplayValue = CASE WHEN ds.IsHoliday = 1 THEN COALESCE(ds.HolidaySymbol, N'L') ELSE NULL END
      FROM dbo.F03HrmAttendanceCalculated AS a
     CROSS APPLY dbo.fn_ResolveDayType(a.WorkDate, a.HrmBCNgayLe, a.HrmBCNgayLeNV) AS ds
     WHERE a.DayType IN (N'SUNDAY', N'COMPANY_SATURDAY', N'COMPENSATORY')
        OR a.SymbolRuleCode LIKE N'SUN-%' OR a.SymbolRuleCode LIKE N'SAT-%'
        OR a.SymbolRuleCode LIKE N'CHOL-%' OR a.SymbolRuleCode LIKE N'COMP-%' OR a.SymbolRuleCode LIKE N'OTH-%';
    SET @rows = @@ROWCOUNT;
END;

/* 4b. Snapshot theo batch: chỉ chuẩn hoá DayType cho dòng chưa có ký hiệu */
IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot', N'DayType') IS NOT NULL
BEGIN
    SET @rows = 1;
    WHILE @rows > 0
    BEGIN
        UPDATE TOP (5000) s SET s.DayType = ds.DayType
          FROM dbo.F03HrmAttendanceCalculatedBatchSnapshot AS s
         CROSS APPLY dbo.fn_ResolveDayType(s.WorkDate, s.HrmBCNgayLe, s.HrmBCNgayLeNV) AS ds
         WHERE s.DayType IN (N'SUNDAY', N'COMPANY_SATURDAY', N'COMPENSATORY') AND s.SymbolRuleCode IS NULL;
        SET @rows = @@ROWCOUNT;
    END;
END;

/* 4c. Lịch sử kỳ đã khoá: KHÔNG đổi ký hiệu đã ghi; chỉ chuẩn hoá DayType cho dòng chưa có ký hiệu */
IF OBJECT_ID(N'dbo.F03HrmAttendanceHistory', N'U') IS NOT NULL
BEGIN
    SET @rows = 1;
    WHILE @rows > 0
    BEGIN
        UPDATE TOP (5000) h SET h.DayType = ds.DayType
          FROM dbo.F03HrmAttendanceHistory AS h
         CROSS APPLY dbo.fn_ResolveDayType(h.WorkDate, h.HrmBCNgayLe, h.HrmBCNgayLeNV) AS ds
         WHERE h.DayType IN (N'SUNDAY', N'COMPANY_SATURDAY', N'COMPENSATORY') AND h.SymbolRuleCode IS NULL;
        SET @rows = @@ROWCOUNT;
    END;
END;
GO

/* 5. Constraint mới: đúng 4 loại ------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.F03AttendanceSymbolRules WHERE DayType NOT IN (N'NORMAL', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'))
    THROW 51900, N'F03AttendanceSymbolRules còn DayType ngoài 4 loại. Xem F03AttendanceSymbolRules_Pre90Backup.', 1;
IF EXISTS (SELECT 1 FROM dbo.F03CompanyHolidays WHERE DayType IS NOT NULL AND DayType NOT IN (N'NORMAL', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'))
    THROW 51901, N'F03CompanyHolidays còn DayType ngoài 4 loại.', 1;
IF EXISTS (SELECT 1 FROM dbo.F03HrmHolidayTypes WHERE DayType NOT IN (N'NORMAL', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'))
    THROW 51902, N'F03HrmHolidayTypes còn DayType ngoài 4 loại.', 1;
ALTER TABLE dbo.F03AttendanceSymbolRules WITH CHECK ADD CONSTRAINT CK_F03AttendanceSymbolRules_DayType
    CHECK (DayType IN (N'NORMAL', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'));
ALTER TABLE dbo.F03CompanyHolidays WITH CHECK ADD CONSTRAINT CK_F03CompanyHolidays_DayType
    CHECK (DayType IS NULL OR DayType IN (N'NORMAL', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'));
ALTER TABLE dbo.F03HrmHolidayTypes WITH CHECK ADD CONSTRAINT CK_F03HrmHolidayTypes_DayType
    CHECK (DayType IN (N'NORMAL', N'COMPANY_HOLIDAY', N'NATIONAL_HOLIDAY', N'OTHER'));
GO

/* Kiểm tra: rule còn lại theo loại ngày + ca; loại nghỉ HRM đã ánh xạ */
SELECT DayType, COUNT(*) AS Rules, SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS ActiveRules
FROM dbo.F03AttendanceSymbolRules GROUP BY DayType ORDER BY DayType;
SELECT LNLMa, LNLTen, LNLKyHieu, DayType FROM dbo.F03HrmHolidayTypes ORDER BY LNLMa;
GO
