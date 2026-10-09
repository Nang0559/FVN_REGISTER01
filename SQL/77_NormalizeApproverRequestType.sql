/*
===============================================================================
77 - Chuẩn hóa F03Approvers.RequestType về mã chuẩn (LEAVE / OT / TRIP / EQUIPMENT)

Vì sao:
  Code (EF) lọc F03Approvers.RequestType bằng mã chuẩn: Overtime -> 'OT'.
  Proc usp_ReconcileEmployeeApprovers (bản cũ) lại ghi 'Overtime' => route không thấy
  approver OT và báo "Chưa cấu hình người phê duyệt...".

THỨ TỰ:
  1) Chạy 31_Hrm_User_Approval_Provisioning.sql (bản đã vá: proc ghi mã chuẩn)
  2) Chạy script này với @DryRun = 1 để xem, rồi @DryRun = 0 để ghi (có backup)
  3) Đồng bộ lại / F5 trang đăng ký
===============================================================================
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun bit = 1;   -- <<< 0 = ghi thật

-- Giá trị hiện có
SELECT RequestType, IsActive, COUNT(*) AS SoDong
FROM   dbo.F03Approvers
GROUP BY RequestType, IsActive
ORDER BY RequestType, IsActive;

IF OBJECT_ID('tempdb..#Map') IS NOT NULL DROP TABLE #Map;

SELECT a.Id, a.RequestType AS Cu,
       Moi = CASE UPPER(LTRIM(RTRIM(a.RequestType)))
                WHEN N'LEAVE'         THEN N'LEAVE'
                WHEN N'LEAVE_REQUEST' THEN N'LEAVE'
                WHEN N'OT'            THEN N'OT'
                WHEN N'OVERTIME'      THEN N'OT'
                WHEN N'TRIP'          THEN N'TRIP'
                WHEN N'TRIP_REQUEST'  THEN N'TRIP'
                WHEN N'EQUIPMENT'     THEN N'EQUIPMENT'
                WHEN N'ATTENDANCE'    THEN N'ATTENDANCE'
                WHEN N'PAYROLL'       THEN N'PAYROLL'
                WHEN N'ACCESSCHANGE'  THEN N'ACCESSCHANGE'
                WHEN N'ACCESS_CHANGE' THEN N'ACCESSCHANGE'
                WHEN N'ENDPOINT'      THEN N'ENDPOINT'
             END
INTO   #Map
FROM   dbo.F03Approvers a;

IF EXISTS (SELECT 1 FROM #Map WHERE Moi IS NULL)
BEGIN
    SELECT * FROM #Map WHERE Moi IS NULL;
    THROW 57701, N'Co RequestType khong nhan dien duoc - dung lai.', 1;
END

-- Dòng sẽ đổi (so sánh phân biệt hoa/thường)
SELECT Cu, Moi, COUNT(*) AS SoDongSeDoi
FROM   #Map
WHERE  Cu COLLATE Latin1_General_BIN2 <> Moi COLLATE Latin1_General_BIN2
GROUP BY Cu, Moi;

IF @DryRun = 0
BEGIN
    IF OBJECT_ID('dbo.F03Approvers_bak_requesttype') IS NULL
        SELECT * INTO dbo.F03Approvers_bak_requesttype FROM dbo.F03Approvers;

    BEGIN TRANSACTION;

    UPDATE a
    SET    a.RequestType = m.Moi
    FROM   dbo.F03Approvers a
    JOIN   #Map m ON m.Id = a.Id
    WHERE  a.RequestType COLLATE Latin1_General_BIN2 <> m.Moi COLLATE Latin1_General_BIN2;

    PRINT CONCAT(N'Da chuan hoa: ', @@ROWCOUNT, N' dong. Backup: dbo.F03Approvers_bak_requesttype');

    COMMIT TRANSACTION;
END
ELSE
    PRINT N'@DryRun = 1: CHUA ghi gi. Dat @DryRun = 0 de ghi that.';

-- Dòng trùng sau chuẩn hóa (cùng loại đơn/approver/cấp/phòng áp dụng) - chỉ liệt kê để xem
SELECT m.Moi AS RequestType, a.ApproverCode, a.Level, a.ApproveForDeptCode,
       COUNT(*) AS SoDong, MIN(a.Id) AS IdGiuLai, MAX(a.Id) AS IdTrung
FROM   dbo.F03Approvers a
JOIN   #Map m ON m.Id = a.Id
WHERE  a.IsActive = 1
GROUP BY m.Moi, a.ApproverCode, a.Level, a.ApproveForDeptCode
HAVING COUNT(*) > 1;

DROP TABLE #Map;
GO
