/*
===============================================================================
98A - Gán lại DayType theo KÝ HIỆU HRM (L / CT / NT) để màu lịch khớp HRM
===============================================================================
Bảng HRM thực tế (HRM.dbo.DM_Loainghile):
    0001  NT  Ngày nghỉ thường        LNLLoai 3  LightYellow   -> OTHER
    0002  CT  Chủ nhật/Nghỉ công ty   LNLLoai 2  Navy          -> COMPANY_HOLIDAY
    0003  L   Ngày lễ                 LNLLoai 1  HotPink       -> NATIONAL_HOLIDAY

Lỗi cũ: migration 83/84 đoán theo số mã (0001 = nghỉ công ty, 0002 = Chủ nhật) nên
NT (LightYellow) mang DayType COMPANY_HOLIDAY và OTHER không có dòng HRM nào
=> ngày NT trắng/sai màu, Chủ nhật không có bản ghi lấy nhầm màu NT, rule CT-/NT- áp lẫn.

Idempotent. Chạy SAU 84 và 90. Sau khi chạy: tính công lại kỳ đang mở để ký hiệu theo rule được ghi lại.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

/* 1. Danh mục loại nghỉ HRM: DayType theo ký hiệu, fallback LNLLoai (1=L, 2=CT, 3=NT) */
UPDATE t
   SET t.DayType = x.NewType,
       t.DayTypeIsManual = 0
  FROM dbo.F03HrmHolidayTypes AS t
 CROSS APPLY
 (
     SELECT NewType = CASE UPPER(LTRIM(RTRIM(ISNULL(t.LNLKyHieu, N''))))
                          WHEN N'L'  THEN N'NATIONAL_HOLIDAY'
                          WHEN N'CT' THEN N'COMPANY_HOLIDAY'
                          WHEN N'NT' THEN N'OTHER'
                          ELSE CASE RTRIM(t.LNLLoai)
                                   WHEN '1' THEN N'NATIONAL_HOLIDAY'
                                   WHEN '2' THEN N'COMPANY_HOLIDAY'
                                   WHEN '3' THEN N'OTHER'
                               END
                      END
 ) AS x
 WHERE x.NewType IS NOT NULL
   AND ISNULL(t.DayType, N'') <> x.NewType;
PRINT CONCAT(N'F03HrmHolidayTypes updated: ', @@ROWCOUNT);

/* 2. Ngày nghỉ đã đồng bộ từ HRM: DayType (và HolidayType 1/2/4 tương ứng) theo ký hiệu */
UPDATE h
   SET h.DayType = t.DayType,
       h.HolidayType = CASE t.DayType WHEN N'COMPANY_HOLIDAY' THEN 1 WHEN N'NATIONAL_HOLIDAY' THEN 2 ELSE 4 END
  FROM dbo.F03CompanyHolidays AS h
  INNER JOIN dbo.F03HrmHolidayTypes AS t
          ON UPPER(LTRIM(RTRIM(h.HolidaySymbol))) = UPPER(LTRIM(RTRIM(t.LNLKyHieu)))
 WHERE h.Source = N'HRM'
   AND ISNULL(t.DLocked, 0) = 0
   AND t.DayType IN (N'NATIONAL_HOLIDAY', N'COMPANY_HOLIDAY', N'OTHER')
   AND (ISNULL(h.DayType, N'') <> t.DayType);
PRINT CONCAT(N'F03CompanyHolidays updated: ', @@ROWCOUNT);

COMMIT TRANSACTION;
GO

/* 3. Dòng tính công kỳ đang mở: tính lại DayType bằng hàm chuẩn; xoá ký hiệu cũ để tính công ghi lại theo rule */
DECLARE @rows int = 1;
WHILE @rows > 0
BEGIN
    UPDATE TOP (5000) a
       SET a.DayType = ds.DayType,
           a.WorkSymbol = NULL, a.OtSymbol = NULL, a.SymbolRuleCode = NULL,
           a.OtDisplayValue = CASE WHEN ds.IsHoliday = 1 THEN COALESCE(ds.HolidaySymbol, N'L') ELSE NULL END
      FROM dbo.F03HrmAttendanceCalculated AS a
     CROSS APPLY dbo.fn_ResolveDayType(a.WorkDate, a.HrmBCNgayLe, a.HrmBCNgayLeNV) AS ds
     WHERE a.DayType IS NULL OR a.DayType <> ds.DayType;
    SET @rows = @@ROWCOUNT;
END;
GO

/* 4. Kiểm tra: mỗi DayType phải có đúng 1 loại HRM (màu lịch tra theo DayType) */
SELECT t.LNLMa, t.LNLTen, t.LNLKyHieu, t.LNLLoai, t.LNLMauSac, t.DayType
  FROM dbo.F03HrmHolidayTypes AS t
 ORDER BY t.LNLMa;
IF EXISTS (SELECT DayType FROM dbo.F03HrmHolidayTypes WHERE ISNULL(DLocked, 0) = 0 GROUP BY DayType HAVING COUNT(*) > 1)
    PRINT N'WARNING: có DayType gắn với nhiều loại HRM - màu lịch sẽ lấy loại có LNLMa nhỏ nhất. Kiểm tra bảng trên.';
GO
