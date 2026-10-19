/*
87 - BACKFILL KÝ HIỆU NGHỈ HRM (CT / L) CHO DỮ LIỆU ĐÃ TÍNH
Chạy sau 83 + 84 (đã có HolidaySymbol) và sau khi dbo.usp_SyncHrmHolidays đã chạy ít nhất một lần.
Chỉ cập nhật DayType và ký hiệu hiển thị của ngày nghỉ HRM thực sự (0002/0003) trong 12 tháng gần nhất ở bảng Calculated;
HRM LNLMa=0001 (NT/NORMAL) không phải ngày nghỉ, không có dòng F03CompanyHolidays và không được backfill thành ký hiệu nghỉ;
không đụng tới giờ công/OT. Ký hiệu theo rule (WorkSymbol/OtSymbol) vẫn do rule quyết định:
sau script này hãy lưu lại một rule (hoặc tính công lại tháng cần thiết) để rule áp theo DayType mới.
Idempotent.
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF COL_LENGTH(N'dbo.F03CompanyHolidays', N'HolidaySymbol') IS NULL
    THROW 51870, N'Thiếu cột F03CompanyHolidays.HolidaySymbol. Chạy lại 83 và 84 trước.', 1;
GO
EXEC dbo.usp_SyncHrmHolidays;
GO
DECLARE @from date = DATEADD(MONTH, -12, CONVERT(date, GETDATE()));
DECLARE @batch int = 1, @rows int = 1;
WHILE @rows > 0
BEGIN
    UPDATE TOP (5000) a
       SET a.DayType = ds.DayType,
           a.OtDisplayValue = CASE WHEN a.OtSymbol IS NULL AND ds.IsHoliday = 1
                                   THEN COALESCE(ds.HolidaySymbol, N'L') ELSE a.OtDisplayValue END,
           a.AttendanceDisplayValue = CASE WHEN ds.IsHoliday = 1 AND ISNULL(a.LeaveTotal, 0) = 0
                                                AND a.CheckInTime IS NULL AND a.CheckOutTime IS NULL
                                                AND ISNULL(a.AttendanceDisplayValue, N'') = N''
                                           THEN COALESCE(ds.HolidaySymbol, N'') ELSE a.AttendanceDisplayValue END
      FROM dbo.F03HrmAttendanceCalculated AS a
     CROSS APPLY dbo.fn_ResolveDayType(a.WorkDate, a.HrmBCNgayLe, a.HrmBCNgayLeNV) AS ds
     WHERE a.WorkDate >= @from
       AND (ISNULL(a.DayType, N'') <> ds.DayType
            OR (ds.IsHoliday = 1 AND a.OtSymbol IS NULL AND ISNULL(a.OtDisplayValue, N'') <> COALESCE(ds.HolidaySymbol, N'L'))
            OR (ds.IsHoliday = 1 AND ISNULL(a.LeaveTotal, 0) = 0 AND a.CheckInTime IS NULL AND a.CheckOutTime IS NULL
                AND ISNULL(a.AttendanceDisplayValue, N'') = N'' AND ds.HolidaySymbol IS NOT NULL));
    SET @rows = @@ROWCOUNT;
END;
GO
SELECT DayType, COUNT(*) AS Cnt FROM dbo.F03HrmAttendanceCalculated
WHERE WorkDate >= DATEADD(MONTH, -12, CONVERT(date, GETDATE())) GROUP BY DayType ORDER BY DayType;
GO
