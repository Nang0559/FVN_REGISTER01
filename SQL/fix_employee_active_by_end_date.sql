/*
  Sửa F03Employees.IsActive theo ngày nghỉ việc (EndWorkingDate) do HRM đồng bộ.

  Quy ước đúng: còn làm việc khi EndWorkingDate IS NULL hoặc EndWorkingDate >= hôm nay.
  Code cũ coi mọi EndWorkingDate (kể cả ngày ở tương lai, vd 31/12) là đã nghỉ -> IsActive = 0.
  Việc đồng bộ chỉ chạy lại cho dòng HRM có thay đổi nên các dòng đã sai không tự được sửa;
  script này sửa một lần các dòng đó, và có thể chạy định kỳ (idempotent) để dòng hết hạn
  ngày nghỉ được chuyển sang IsActive = 0.

  Chỉ đụng các dòng do HRM sở hữu (LastModifiedSource = 'HRM'); dòng nhập tay không bị thay đổi.
  Bước 1 chỉ xem trước. Bước 2 mới ghi dữ liệu.
*/
SET NOCOUNT ON;
DECLARE @Today date = CAST(GETDATE() AS date);

/* ===== Bước 1: xem trước ===== */
SELECT e.EmployeeCode, e.EmployeeName, e.DeptCode, e.IsActive, e.EndWorkingDate,
       NewIsActive = CASE WHEN e.EndWorkingDate IS NULL OR CAST(e.EndWorkingDate AS date) >= @Today THEN 1 ELSE 0 END
FROM dbo.F03Employees e
WHERE e.LastModifiedSource = N'HRM'
  AND ISNULL(e.IsActive,0) <> CASE WHEN e.EndWorkingDate IS NULL OR CAST(e.EndWorkingDate AS date) >= @Today THEN 1 ELSE 0 END
ORDER BY e.EmployeeCode;

/* ===== Bước 2: ghi dữ liệu (bỏ comment khối bên dưới sau khi đã kiểm tra bước 1) ===== */
/*
BEGIN TRAN;

DECLARE @Changed TABLE (EmployeeCode nvarchar(50) PRIMARY KEY);

UPDATE e
   SET e.IsActive = CASE WHEN e.EndWorkingDate IS NULL OR CAST(e.EndWorkingDate AS date) >= @Today THEN 1 ELSE 0 END,
       e.ModifiedAt = GETDATE()
OUTPUT inserted.EmployeeCode INTO @Changed(EmployeeCode)
FROM dbo.F03Employees e
WHERE e.LastModifiedSource = N'HRM'
  AND ISNULL(e.IsActive,0) <> CASE WHEN e.EndWorkingDate IS NULL OR CAST(e.EndWorkingDate AS date) >= @Today THEN 1 ELSE 0 END;

COMMIT;

-- Đồng bộ tài khoản + người duyệt cho các nhân viên vừa đổi trạng thái (cùng thủ tục mà đồng bộ HRM dùng).
DECLARE @Code nvarchar(50);
WHILE EXISTS (SELECT 1 FROM @Changed)
BEGIN
    SELECT TOP (1) @Code = EmployeeCode FROM @Changed ORDER BY EmployeeCode;
    EXEC dbo.usp_ReconcileEmployeeUsers     @EmployeeCode = @Code;
    EXEC dbo.usp_ReconcileEmployeeApprovers @EmployeeCode = @Code;
    DELETE FROM @Changed WHERE EmployeeCode = @Code;
END;
*/
