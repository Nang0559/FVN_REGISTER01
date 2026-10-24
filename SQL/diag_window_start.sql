-- Kiểm tra cửa sổ đọc D-6h có lấy nhầm GIỜ VÀO của ngày trước hay không (chạy trên DB FVN_REGISTER).
-- Chạy TRƯỚC khi deploy 22_02 để biết có bị không, và chạy lại SAU khi tính công lại để xác nhận đã hết.

-- A) Dòng có giờ vào thuộc NGÀY LỊCH TRƯỚC ngày công (dấu hiệu lấy nhầm IN của ca đêm ngày D-1). Mong đợi sau sửa: 0 dòng.
SELECT EmployeeCode, WorkDate, ShiftAbbr, CheckInTime, CheckOutTime
FROM dbo.F03HrmAttendanceCalculated
WHERE CheckInTime IS NOT NULL AND CAST(CheckInTime AS date) < WorkDate
ORDER BY EmployeeCode, WorkDate;

-- B) Giờ vào của hai ngày liên tiếp GIỐNG HỆT nhau (cùng 1 lần quẹt bị dùng cho 2 ngày). Mong đợi: 0 dòng.
;WITH x AS (
  SELECT EmployeeCode, WorkDate, ShiftAbbr, CheckInTime, CheckOutTime,
         LAG(CheckInTime) OVER (PARTITION BY EmployeeCode ORDER BY WorkDate) AS PrevCheckIn,
         LAG(WorkDate)    OVER (PARTITION BY EmployeeCode ORDER BY WorkDate) AS PrevWorkDate
  FROM dbo.F03HrmAttendanceCalculated
  WHERE CheckInTime IS NOT NULL)
SELECT * FROM x WHERE CheckInTime = PrevCheckIn ORDER BY EmployeeCode, WorkDate;

-- C) Dòng KHÔNG có ca nhưng có giờ vào (ngày nghỉ bị "dính" IN của ca đêm hôm trước). Xem cột InDate so với WorkDate.
SELECT EmployeeCode, WorkDate, CAST(CheckInTime AS date) AS InDate, CheckInTime, CheckOutTime
FROM dbo.F03HrmAttendanceCalculated
WHERE ShiftAbbr IS NULL AND CheckInTime IS NOT NULL AND CAST(CheckInTime AS date) < WorkDate
ORDER BY EmployeeCode, WorkDate;

-- D) Giờ ra nhỏ hơn giờ vào (lỗi lệch ngày giờ ra, đã sửa ở 22_02). Mong đợi sau sửa: 0 dòng.
SELECT COUNT(*) AS OutBeforeIn
FROM dbo.F03HrmAttendanceCalculated
WHERE CheckInTime IS NOT NULL AND CheckOutTime IS NOT NULL AND CheckOutTime < CheckInTime;

-- E) Đối chiếu 1 nhân viên với dữ liệu quẹt thẻ thô trong HRM (đổi @nv = NVMa trong HRM.dbo.tblNhanVien)
DECLARE @nv int = 0;  -- <-- điền NVMa
SELECT R.ThoiGian, R.IDM, D.DDChinhVao AS IsIn
FROM HRM.dbo.RecordDataNew R
JOIN HRM.dbo.tblCapThe C ON C.CTMaNV=@nv AND C.CTMaThe=R.IDCard
LEFT JOIN HRM.dbo.tblDauDoc D ON D.DDMa=R.IDM
WHERE R.ThoiGian >= '2026-09-27' AND R.ThoiGian < '2026-10-02'
ORDER BY R.ThoiGian;
