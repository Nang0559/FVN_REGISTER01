/*
86 - KIỂM TRA ÁNH XẠ NGÀY NGHỈ HRM -> DayType (chỉ đọc, không sửa dữ liệu)
Chạy sau 83 và 84. Mỗi truy vấn nên trả 0 dòng (trừ mục 3 và 4 là thống kê).
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;

-- 1. Ngày HRM không có danh mục đích theo đúng mapping canonical.
-- NNLLoai=1 -> LNLMa=0003; NNLLoai=2 -> LNLMa=0002.
-- Các mã khác mới dùng fallback LNLMa/LNLLoai.
SELECT n.NNLMa, n.NNLNgay, n.NNLLoai
FROM HRM.dbo.tblNgayNghiLe AS n
WHERE ISNULL(n.DLocked, 0) = 0
  AND NOT EXISTS
  (
      SELECT 1
      FROM HRM.dbo.DM_Loainghile AS t
      WHERE ISNULL(t.DLocked, 0) = 0
        AND
        (
            (RTRIM(CONVERT(varchar(10), n.NNLLoai)) = '1'
             AND RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4) = '0003')
            OR
            (RTRIM(CONVERT(varchar(10), n.NNLLoai)) = '2'
             AND RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4) = '0002')
            OR
            (
                RTRIM(CONVERT(varchar(10), n.NNLLoai)) NOT IN ('1', '2')
                AND
                (
                    RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4)
                        = RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(10), n.NNLLoai))), 4)
                    OR RTRIM(CONVERT(varchar(10), t.LNLLoai))
                        = RTRIM(CONVERT(varchar(10), n.NNLLoai))
                )
            )
        )
  );

-- 2. Mapping fallback mơ hồ chỉ áp dụng cho nhóm khác 1/2.
-- Hai nhóm 1/2 đã được ánh xạ tường minh nên không tham gia phép dò mơ hồ.
SELECT n.NNLLoai, COUNT(DISTINCT t.LNLMa) AS MatchedTypes
FROM HRM.dbo.tblNgayNghiLe AS n
JOIN HRM.dbo.DM_Loainghile AS t
  ON
  (
      RTRIM(CONVERT(varchar(10), n.NNLLoai)) NOT IN ('1', '2')
      AND
      (
          RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(5), t.LNLMa))), 4)
              = RIGHT('0000' + LTRIM(RTRIM(CONVERT(varchar(10), n.NNLLoai))), 4)
          OR RTRIM(CONVERT(varchar(10), t.LNLLoai))
              = RTRIM(CONVERT(varchar(10), n.NNLLoai))
      )
  )
WHERE ISNULL(n.DLocked, 0) = 0
  AND ISNULL(t.DLocked, 0) = 0
GROUP BY n.NNLLoai
HAVING COUNT(DISTINCT t.LNLMa) > 1;

-- 3. DayType đang gán cho từng loại ngày nghỉ. 0001/0002/0003 do sync ép về mapping canonical; chỉ cân nhắc mapping thủ công cho mã HRM khác.
SELECT LNLMa, LNLTen, LNLLoai, DayType, DayTypeIsManual, DLocked
FROM dbo.F03HrmHolidayTypes ORDER BY LNLMa;

-- 4. Số ngày nghỉ theo DayType / nguồn
SELECT Source, DayType, HolidayType, HolidaySymbol, COUNT(*) AS Cnt, SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS ActiveCnt
FROM dbo.F03CompanyHolidays GROUP BY Source, DayType, HolidayType, HolidaySymbol ORDER BY Source, DayType;

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
