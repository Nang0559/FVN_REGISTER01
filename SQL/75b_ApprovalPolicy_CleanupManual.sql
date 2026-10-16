/*
===============================================================================
75b - Dọn policy test/thủ công trước khi chạy lại seed 76 (VÔ HIỆU HÓA, KHÔNG XÓA)

Làm gì:
  - Chọn mọi policy ĐANG ACTIVE có LastModifiedSource <> 'Seed' (tức tạo tay/test)
    trong phạm vi @OnlyDeptCode / @Types.
  - Đặt IsActive = 0 (giữ nguyên dòng, giữ nguồn gốc 'Manual', ghi ModifiedBy/ModifiedAt).
  - Không đụng policy do seed tạo (LastModifiedSource = 'Seed').

Sau khi chạy script này, chạy 76 NGUYÊN BẢN (@Overwrite = 0): các (loại đơn, phòng)
không còn policy active sẽ được seed lại theo thang chuẩn.

THỨ TỰ:
  1) 75b với @DryRun = 1  -> xem danh sách sẽ bị vô hiệu hóa
  2) 75b với @DryRun = 0  -> ghi thật (có backup)
  3) 76_ApprovalPolicySeed.sql
  4) Đồng bộ tất cả (tạo F03Approvers từ policy mới)
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun       bit = 1;      -- <<< 0 = ghi thật
DECLARE @OnlyDeptCode int = NULL;   -- NULL = mọi phòng; ví dụ 13 = chỉ phòng 13
DECLARE @ModifiedBy   int = 0;

-- Loại đơn áp dụng (0 Leave, 1 Overtime, 2 Trip, 3 Equipment)
DECLARE @Types TABLE (RequestType int NOT NULL PRIMARY KEY);
INSERT @Types(RequestType) VALUES (0),(1),(2),(3);

IF OBJECT_ID('tempdb..#Target') IS NOT NULL DROP TABLE #Target;

SELECT ap.Id, ap.RequestType, ap.DeptCode, ap.PositionCode AS NguoiDangKy,
       ap.[Level], ap.ApprovalPositionCode, ap.LastModifiedSource, ap.CreatedAt
INTO   #Target
FROM   dbo.F03ApprovalPolicies ap
JOIN   @Types t ON t.RequestType = ap.RequestType
WHERE  ap.IsActive = 1
  AND  ISNULL(ap.LastModifiedSource, N'') <> N'Seed'
  AND  (@OnlyDeptCode IS NULL OR ap.DeptCode = @OnlyDeptCode);

-- ===== XEM TRƯỚC =====
SELECT DeptCode, RequestType, LastModifiedSource, COUNT(*) AS SoDongSeVoHieuHoa
FROM   #Target
GROUP BY DeptCode, RequestType, LastModifiedSource
ORDER BY DeptCode, RequestType;

SELECT * FROM #Target ORDER BY DeptCode, RequestType, NguoiDangKy, [Level];

-- ===== GHI THẬT =====
IF @DryRun = 0
BEGIN
    IF OBJECT_ID('dbo.F03ApprovalPolicies_bak_20261008') IS NULL
        SELECT * INTO dbo.F03ApprovalPolicies_bak_20261008 FROM dbo.F03ApprovalPolicies;

    BEGIN TRANSACTION;

    UPDATE ap
    SET    ap.IsActive = 0,
           ap.ModifiedBy = @ModifiedBy,
           ap.ModifiedAt = GETDATE()
    FROM   dbo.F03ApprovalPolicies ap
    JOIN   #Target t ON t.Id = ap.Id;

    PRINT CONCAT('Da vo hieu hoa: ', @@ROWCOUNT, ' dong. Backup: dbo.F03ApprovalPolicies_bak_20261008');

    COMMIT TRANSACTION;
END
ELSE
    PRINT N'@DryRun = 1: CHUA ghi gi. Dat @DryRun = 0 de ghi that.';

DROP TABLE #Target;
GO
