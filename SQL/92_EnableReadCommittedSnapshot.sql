/*
===============================================================================
92 - BẬT READ_COMMITTED_SNAPSHOT ĐỂ LỊCH CHẤM CÔNG KHÔNG BỊ TÍNH GIỜ CHẶN (TIMEOUT 30s)
===============================================================================
NGUYÊN NHÂN
  dbo.usp_CalculateHrmAttendance (22_03) với @DeptCode = NULL (job nền, cả công ty) xử lý TỪNG NGÀY:
    BEGIN TRANSACTION
      DELETE toàn bộ dòng ngày đó khỏi F03HrmAttendanceCalculated (+ F03HrmOTActual)
      lặp từng nhân viên: usp_HrmCompatibleTimeKeepingForStaff -> INSERT -> UPDATE
    COMMIT
  => các khoá X của cả ngày được giữ suốt thời gian tính (hàng nghìn nhân viên, command timeout 1800s).
  Truy vấn lịch (AttendanceCalendarModuleProvider, UNION ALL Calculated + History, timeout 30s) chạy ở
  READ COMMITTED mặc định nên bị chặn đến khi COMMIT => "Execution Timeout Expired" (Error -2).

CÁCH SỬA
  READ_COMMITTED_SNAPSHOT = ON: người đọc thấy bản đã COMMIT gần nhất (kết quả tính công trước đó) thay vì chờ khoá,
  đúng ý đồ trong proc ("giữ nguyên kết quả cũ nếu tính giữa chừng bị lỗi"). Không đổi code, không đổi thuật toán tính công.

LƯU Ý TRIỂN KHAI (cần DBA)
  - WITH ROLLBACK IMMEDIATE ngắt các kết nối đang mở của database -> chạy ngoài giờ cao điểm / khi không có job tính công.
  - Tăng nhẹ dung lượng tempdb (row versioning).
  - Không ảnh hưởng HRM (chỉ đổi database FVN_REGISTER).
===============================================================================
*/
USE [master];
GO
SET NOCOUNT ON;

SELECT name, is_read_committed_snapshot_on, snapshot_isolation_state_desc
FROM sys.databases WHERE name = N'FVN_REGISTER';

IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'FVN_REGISTER' AND is_read_committed_snapshot_on = 0)
BEGIN
    PRINT N'Đang bật READ_COMMITTED_SNAPSHOT cho FVN_REGISTER ...';
    ALTER DATABASE [FVN_REGISTER] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
END
ELSE
    PRINT N'READ_COMMITTED_SNAPSHOT đã bật sẵn.';
GO

-- Kiểm tra sau khi bật: is_read_committed_snapshot_on phải = 1
SELECT name, is_read_committed_snapshot_on, snapshot_isolation_state_desc
FROM sys.databases WHERE name = N'FVN_REGISTER';
GO

-- Nếu vẫn timeout sau khi bật: chạy 91_DiagnoseCalendarBlocking.sql và 85_AttendanceCalendarTypedSeekIndexes.sql
-- (kiểm tra index (EmployeeCode, WorkDate) đã được tạo trên cả 2 bảng).
