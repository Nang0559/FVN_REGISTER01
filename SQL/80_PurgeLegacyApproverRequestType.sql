/*
===============================================================================
80 - Dọn triệt để F03Approvers.RequestType không đồng nhất (OT / LEAVE / ...)

Quy ước duy nhất: F03Approvers.RequestType lưu đúng tên enum RequestModule
  Leave / Overtime / Trip / Equipment (và Attendance / Payroll / AccessChange / Endpoint).

Script này (dùng cho DB dữ liệu khởi tạo, cho phép XÓA - không chỉ tắt IsActive):
  1) Backup toàn bộ F03Approvers -> dbo.F03Approvers_bak_80 (chỉ tạo nếu chưa có).
  2) Với mỗi khóa (RequestType chuẩn, ApproverCode, Level, ApproveForDeptCode):
     chỉ giữ 1 dòng, XÓA các dòng trùng. Ưu tiên giữ: dòng đang active, rồi dòng
     nhập tay (không phải HRM), rồi dòng đã đúng chuẩn, rồi Id nhỏ nhất.
  3) (@PurgeInactiveLegacy = 1) XÓA dòng HRM đã inactive mà RequestType đang ở
     dạng cũ (OT, LEAVE, TRIP, EQUIPMENT...). Đây là rác do proc bản cũ để lại.
  4) Đổi các dòng còn lại về tên chuẩn (OT -> Overtime, LEAVE -> Leave, ...).
  5) (@AddGuard = 1) Thêm CHECK constraint phân biệt hoa/thường: từ nay ghi 'OT'
     vào F03Approvers sẽ bị DB từ chối ngay, không còn lệch âm thầm.
  6) (@Regenerate = 1) Chạy usp_ReconcileEmployeeApprovers để sinh lại approver
     theo F03ApprovalPolicies.
  7) Kiểm tra kết quả; nếu còn dòng sai chuẩn hoặc còn dòng trùng thì THROW.

ĐIỀU KIỆN TRƯỚC KHI CHẠY (@DryRun = 0):
  - Đã deploy code mới (F03ApproverConfiguration dùng ToStorageName, HrmSyncService
    so sánh Overtime) và RESTART mọi instance ứng dụng dùng DB này.
  - Đã chạy lại 31_Hrm_User_Approval_Provisioning.sql (proc dùng Overtime).
  - Tạm dừng job HRM sync trong lúc chạy script.
  Nếu còn instance build cũ, CHECK constraint sẽ làm lệnh ghi 'OT' của nó báo lỗi
  (job sync ghi cờ ApproverProvisioningFailed) thay vì âm thầm tạo lại dữ liệu sai.

Cách dùng: chạy với @DryRun = 1 để xem kế hoạch, rồi đặt @DryRun = 0 để ghi thật.
Có thể thay thế 77_NormalizeApproverRequestType.sql (script 77 chỉ đổi tên,
không xử lý dòng trùng).
===============================================================================
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun             bit = 1;   -- <<< 0 = ghi thật
DECLARE @PurgeInactiveLegacy bit = 1;  -- xóa dòng HRM inactive đang ở dạng cũ
DECLARE @AddGuard           bit = 1;   -- thêm CHECK constraint chống ghi lại 'OT'
DECLARE @Regenerate         bit = 1;   -- chạy proc sinh lại approver sau khi dọn
DECLARE @CheckDept          int = 13;  -- phòng ban in ra để kiểm tra nhanh

-- ---------------------------------------------------------------------------
-- 0) Hiện trạng
-- ---------------------------------------------------------------------------
SELECT N'TRUOC' AS Buoc, RequestType, IsActive, LastModifiedSource, COUNT(*) AS SoDong
FROM   dbo.F03Approvers
GROUP BY RequestType, IsActive, LastModifiedSource
ORDER BY RequestType, IsActive, LastModifiedSource;

-- ---------------------------------------------------------------------------
-- 1) Bảng ánh xạ: mỗi dòng -> tên chuẩn
-- ---------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#Map') IS NOT NULL DROP TABLE #Map;

SELECT x.Id, x.Cu, x.IsActive, x.LastModifiedSource, x.ApproverCode, x.[Level],
       x.ApproveForDeptCode, x.Moi,
       -- đã đúng chuẩn? (so sánh phân biệt hoa/thường)
       Chuan = CASE WHEN x.Cu COLLATE Latin1_General_BIN2 = x.Moi COLLATE Latin1_General_BIN2
                    THEN 1 ELSE 0 END
INTO   #Map
FROM
(
    SELECT a.Id,
           a.RequestType AS Cu,
           a.IsActive,
           a.LastModifiedSource,
           a.ApproverCode,
           a.[Level],
           a.ApproveForDeptCode,
           Moi = CASE UPPER(LTRIM(RTRIM(a.RequestType)))
                    WHEN N'LEAVE'         THEN N'Leave'
                    WHEN N'LEAVE_REQUEST' THEN N'Leave'
                    WHEN N'OT'            THEN N'Overtime'
                    WHEN N'OVERTIME'      THEN N'Overtime'
                    WHEN N'TRIP'          THEN N'Trip'
                    WHEN N'TRIP_REQUEST'  THEN N'Trip'
                    WHEN N'EQUIPMENT'     THEN N'Equipment'
                    WHEN N'ATTENDANCE'    THEN N'Attendance'
                    WHEN N'PAYROLL'       THEN N'Payroll'
                    WHEN N'ACCESSCHANGE'  THEN N'AccessChange'
                    WHEN N'ACCESS_CHANGE' THEN N'AccessChange'
                    WHEN N'ENDPOINT'      THEN N'Endpoint'
                 END
    FROM dbo.F03Approvers a
) x;

IF EXISTS (SELECT 1 FROM #Map WHERE Moi IS NULL)
BEGIN
    SELECT N'KHONG NHAN DIEN DUOC' AS Loi, * FROM #Map WHERE Moi IS NULL;
    THROW 58001, N'Co RequestType khong nhan dien duoc - dung lai, khong ghi gi.', 1;
END;

-- ---------------------------------------------------------------------------
-- 2) Danh sách dòng sẽ XÓA
-- ---------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#Del') IS NOT NULL DROP TABLE #Del;
CREATE TABLE #Del (Id int NOT NULL PRIMARY KEY, LyDo nvarchar(40) NOT NULL);

-- 2a) Dòng trùng khóa chuẩn: giữ đúng 1 dòng
;WITH r AS
(
    SELECT m.Id,
           rn = ROW_NUMBER() OVER
           (
               PARTITION BY m.Moi, m.ApproverCode, m.[Level], m.ApproveForDeptCode
               ORDER BY CASE WHEN ISNULL(m.IsActive, 0) = 1 THEN 0 ELSE 1 END,
                        CASE WHEN ISNULL(m.LastModifiedSource, N'') = N'HRM' THEN 1 ELSE 0 END,
                        CASE WHEN m.Chuan = 1 THEN 0 ELSE 1 END,
                        m.Id
           )
    FROM #Map m
)
INSERT #Del (Id, LyDo)
SELECT Id, N'TRUNG KHOA'
FROM   r
WHERE  rn > 1;

-- 2b) Dòng HRM inactive còn ở dạng cũ: rác của proc bản cũ
IF @PurgeInactiveLegacy = 1
    INSERT #Del (Id, LyDo)
    SELECT m.Id, N'INACTIVE DANG CU (HRM)'
    FROM   #Map m
    WHERE  m.Chuan = 0
      AND  ISNULL(m.IsActive, 0) = 0
      AND  ISNULL(m.LastModifiedSource, N'') = N'HRM'
      AND  NOT EXISTS (SELECT 1 FROM #Del d WHERE d.Id = m.Id);

-- ---------------------------------------------------------------------------
-- 3) Kế hoạch
-- ---------------------------------------------------------------------------
SELECT N'KE HOACH' AS Buoc,
       m.Cu AS RequestType_Hien_Tai,
       m.Moi AS RequestType_Chuan,
       m.IsActive,
       HanhDong = CASE WHEN d.Id IS NOT NULL THEN N'XOA (' + d.LyDo + N')'
                       WHEN m.Chuan = 0      THEN N'DOI TEN'
                       ELSE N'GIU NGUYEN' END,
       COUNT(*) AS SoDong
FROM   #Map m
LEFT JOIN #Del d ON d.Id = m.Id
GROUP BY m.Cu, m.Moi, m.IsActive,
         CASE WHEN d.Id IS NOT NULL THEN N'XOA (' + d.LyDo + N')'
              WHEN m.Chuan = 0      THEN N'DOI TEN'
              ELSE N'GIU NGUYEN' END
ORDER BY m.Moi, m.Cu, m.IsActive;

IF @DryRun = 1
BEGIN
    PRINT N'@DryRun = 1: CHUA ghi gi. Dat @DryRun = 0 de ghi that.';
    RETURN;
END;

-- ---------------------------------------------------------------------------
-- 4) Ghi thật (một transaction; lỗi -> rollback toàn bộ)
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.F03Approvers_bak_80', N'U') IS NULL
    SELECT * INTO dbo.F03Approvers_bak_80 FROM dbo.F03Approvers;

DECLARE @Xoa int = 0, @Doi int = 0;

BEGIN TRANSACTION;

DELETE a
FROM   dbo.F03Approvers a
JOIN   #Del d ON d.Id = a.Id;
SET @Xoa = @@ROWCOUNT;

UPDATE a
SET    a.RequestType = m.Moi
FROM   dbo.F03Approvers a
JOIN   #Map m ON m.Id = a.Id
WHERE  a.RequestType COLLATE Latin1_General_BIN2 <> m.Moi COLLATE Latin1_General_BIN2;
SET @Doi = @@ROWCOUNT;

IF @AddGuard = 1
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints
                   WHERE name = N'CK_F03Approvers_RequestType_Canonical'
                     AND parent_object_id = OBJECT_ID(N'dbo.F03Approvers'))
BEGIN
    ALTER TABLE dbo.F03Approvers WITH CHECK
        ADD CONSTRAINT CK_F03Approvers_RequestType_Canonical
        CHECK (RequestType COLLATE Latin1_General_BIN2 IN
               (N'Leave', N'Overtime', N'Trip', N'Equipment',
                N'Attendance', N'Payroll', N'AccessChange', N'Endpoint'));
END;

COMMIT TRANSACTION;

PRINT CONCAT(N'Da xoa: ', @Xoa, N' dong; da doi ten: ', @Doi,
             N' dong. Backup: dbo.F03Approvers_bak_80');

-- ---------------------------------------------------------------------------
-- 5) Sinh lại approver theo policy bằng proc bản mới (ngoài transaction trên)
-- ---------------------------------------------------------------------------
IF @Regenerate = 1
BEGIN
    BEGIN TRY
        EXEC dbo.usp_ReconcileEmployeeApprovers @EmployeeCode = NULL, @CreatedBy = 0;
        PRINT N'Da chay usp_ReconcileEmployeeApprovers.';
    END TRY
    BEGIN CATCH
        PRINT CONCAT(N'CANH BAO: usp_ReconcileEmployeeApprovers loi: ', ERROR_MESSAGE());
    END CATCH;
END;

-- ---------------------------------------------------------------------------
-- 6) Kiểm tra sau khi dọn
-- ---------------------------------------------------------------------------
SELECT N'SAU' AS Buoc, RequestType, IsActive, LastModifiedSource, COUNT(*) AS SoDong
FROM   dbo.F03Approvers
GROUP BY RequestType, IsActive, LastModifiedSource
ORDER BY RequestType, IsActive, LastModifiedSource;

DECLARE @SaiChuan int =
(
    SELECT COUNT(*) FROM dbo.F03Approvers
    WHERE RequestType COLLATE Latin1_General_BIN2 NOT IN
          (N'Leave', N'Overtime', N'Trip', N'Equipment',
           N'Attendance', N'Payroll', N'AccessChange', N'Endpoint')
);
DECLARE @Trung int =
(
    SELECT COUNT(*) FROM
    (
        SELECT 1 AS x
        FROM   dbo.F03Approvers
        GROUP BY RequestType, ApproverCode, [Level], ApproveForDeptCode
        HAVING COUNT(*) > 1
    ) t
);

SELECT SaiChuan = @SaiChuan, NhomKhoaTrung = @Trung;

SELECT N'OVERTIME ACTIVE' AS Buoc, ApproverCode, PositionCode, ApproverName, [Level], ApproveForDeptCode
FROM   dbo.F03Approvers
WHERE  RequestType = N'Overtime' AND IsActive = 1
  AND  ApproveForDeptCode = @CheckDept
ORDER BY [Level], ApproverCode;

IF @SaiChuan > 0 OR @Trung > 0
    THROW 58002, N'Van con dong sai chuan hoac dong trung trong F03Approvers - xem result set o tren.', 1;

PRINT N'OK: F03Approvers.RequestType da dong nhat.';
