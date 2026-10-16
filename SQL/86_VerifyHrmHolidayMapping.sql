/*
86 - KIỂM TRA ÁNH XẠ NGÀY NGHỈ HRM -> DayType (chỉ đọc, không sửa dữ liệu)
Chạy sau 83 và 84. Mỗi truy vấn nên trả 0 dòng (trừ mục 3 và 4 là thống kê).
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;

-- 1. Ngày nghỉ HRM không khớp loại nào trong DM_Loainghile (cả theo LNLMa lẫn LNLLoai)
SELECT n.NNLMa, n.NNLNgay, n.NNLLoai
FROM HRM.dbo.tblNgayNghiLe AS n
WHERE ISNULL(n.DLocked, 0) = 0
  AND NOT EXISTS (SELECT 1 FROM HRM.dbo.DM_Loainghile t
                  WHERE ISNULL(t.DLocked, 0) = 0
                    AND (RTRIM(t.LNLMa) = RTRIM(n.NNLLoai) OR RTRIM(t.LNLLoai) = RTRIM(n.NNLLoai)));

-- 2. Mã NNLLoai khớp mơ hồ: vừa là LNLMa của loại này, vừa là LNLLoai của loại khác
SELECT n.NNLLoai, COUNT(DISTINCT t.LNLMa) AS MatchedTypes
FROM HRM.dbo.tblNgayNghiLe AS n
JOIN HRM.dbo.DM_Loainghile AS t
  ON RTRIM(t.LNLMa) = RTRIM(n.NNLLoai) OR RTRIM(t.LNLLoai) = RTRIM(n.NNLLoai)
GROUP BY n.NNLLoai
HAVING COUNT(DISTINCT t.LNLMa) > 1;

-- 3. DayType đang gán cho từng loại ngày nghỉ (kiểm tra bằng mắt; sửa tay + DayTypeIsManual = 1 nếu sai)
SELECT LNLMa, LNLTen, LNLLoai, DayType, DayTypeIsManual, DLocked
FROM dbo.F03HrmHolidayTypes ORDER BY LNLMa;

-- 4. Số ngày nghỉ theo DayType / nguồn
SELECT Source, DayType, HolidayType, COUNT(*) AS Cnt, SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS ActiveCnt
FROM dbo.F03CompanyHolidays GROUP BY Source, DayType, HolidayType ORDER BY Source, DayType;

-- 5. DayType của ngày nghỉ không có rule ký hiệu đang bật
SELECT DISTINCT h.DayType
FROM dbo.F03CompanyHolidays AS h
WHERE h.Source = N'HRM' AND h.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.F03AttendanceSymbolRules r WHERE r.IsActive = 1 AND r.DayType = h.DayType);

-- 6. Dòng tính công đã có In/Out nhưng chưa có ký hiệu theo rule (cần tính lại hoặc bổ sung rule)
SELECT TOP (50) DayType, ShiftAbbr, COUNT(*) AS Cnt
FROM dbo.F03HrmAttendanceCalculated
WHERE CheckInTime IS NOT NULL AND CheckOutTime IS NOT NULL AND SymbolRuleCode IS NULL
GROUP BY DayType, ShiftAbbr ORDER BY Cnt DESC;
GO
