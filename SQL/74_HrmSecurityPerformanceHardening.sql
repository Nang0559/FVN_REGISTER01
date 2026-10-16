/*
FVN_REGISTER - HRM security/performance hardening (kiểm tra + rà dữ liệu)

Thứ tự chạy:
  1. SQL/31_Hrm_User_Approval_Provisioning.sql  (CREATE OR ALTER - đã chứa ownership guard,
     chỉ-update-dòng-thay-đổi và default password Fcc@123). Chạy lại file 31 trên DB hiện có.
  2. Script này: chỉ SELECT kiểm tra + danh sách tài khoản cần rà soát. KHÔNG tự sửa dữ liệu.

Script cũ dùng REPLACE trên OBJECT_DEFINITION nên dễ lỗi nửa chừng; không còn dùng nữa.
Chạy script này nhiều lần an toàn.
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
GO

/* 1. Kiểm tra proc đã có ownership guard chưa (cả hai phải = 1). */
SELECT
    ProcedureName = N'usp_ReconcileEmployeeUsers',
    HasOwnershipGuard = CASE WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_ReconcileEmployeeUsers', N'P'))
                              LIKE N'%WHERE u.LastModifiedSource=N''HRM''%' THEN 1 ELSE 0 END,
    HasFcCPassword    = CASE WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_ReconcileEmployeeUsers', N'P'))
                              LIKE N'%edbf6b4c784a9d55a68f115834be9d51%' THEN 1 ELSE 0 END;

SELECT
    ProcedureName = N'usp_ReconcileEmployeeApprovers',
    HasUserOwnershipGuard = CASE WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_ReconcileEmployeeApprovers', N'P'))
                              LIKE N'%AND u.LastModifiedSource = N''HRM''%' THEN 1 ELSE 0 END;
GO

/*
   2. Tài khoản có thể đã bị đóng dấu 'HRM' nhầm bởi procedure cũ.
      Guard LastModifiedSource='HRM' KHÔNG bảo vệ được các tài khoản này vì dấu đã sai.
      Rà soát cột Reason rồi tự quyết định.
*/
SELECT
    u.Id, u.EmployeeCode, u.FullName, u.PermissionCode, u.CreatedBy,
    u.IsActive AS UserIsActive, u.LastModifiedSource,
    e.IsActive AS EmployeeIsActive,
    Reason = CONCAT(
        CASE WHEN u.CreatedBy > 0 THEN N'CreatedBy>0; ' ELSE N'' END,
        CASE WHEN u.PermissionCode IN (1, 2) THEN N'Admin/SuperAdmin; ' ELSE N'' END,
        CASE WHEN e.EmployeeCode IS NULL THEN N'KhongCoDongEmployee; ' ELSE N'' END,
        CASE WHEN ISNULL(u.IsActive, 0) = 0 AND e.IsActive = 1 THEN N'User inactive nhung Employee active; ' ELSE N'' END)
FROM dbo.F03Users u
LEFT JOIN dbo.F03Employees e ON e.EmployeeCode = u.EmployeeCode
WHERE u.LastModifiedSource = N'HRM'
  AND (   u.CreatedBy > 0
       OR u.PermissionCode IN (1, 2)
       OR e.EmployeeCode IS NULL
       OR (ISNULL(u.IsActive, 0) = 0 AND e.IsActive = 1))
ORDER BY u.EmployeeCode;
GO

/*
   3. Sau khi rà soát, đánh dấu các tài khoản THỦ CÔNG để HRM không còn khóa/ghi đè.
      (Bỏ comment và điền đúng mã nhân viên. Nếu cần mở lại tài khoản, đặt IsActive=1.)

UPDATE dbo.F03Users
   SET LastModifiedSource = N'Manual', ModifiedAt = GETDATE()
 WHERE EmployeeCode IN (N'...', N'...');
*/
GO
