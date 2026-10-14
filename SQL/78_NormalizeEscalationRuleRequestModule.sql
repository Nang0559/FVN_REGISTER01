/*
===============================================================================
78 - Chuẩn hóa F03EscalationRules.RequestModule về tên enum (Leave / Overtime / Trip / Equipment)

Vì sao:
  Quy ước thống nhất: cột RequestModule lưu đúng tên enum RequestModule, giống
  F03Approvers.RequestType (xem 77). Dòng cũ lưu 'OT' (hoặc LEAVE/TRIP/... viết hoa).

THỨ TỰ:
  1) Deploy code mới (F03EscalationRuleConfiguration dùng ToStorageName)
  2) Chạy script này với @DryRun = 1 để xem, rồi @DryRun = 0 để ghi (có backup)
  3) Có thể chạy lại 06_Seed.sql (đã chuyển sang 'Overtime', không seed trùng dòng 'OT' cũ)
===============================================================================
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun bit = 1;   -- <<< 0 = ghi thật

-- Giá trị hiện có
SELECT RequestModule, IsActive, COUNT(*) AS SoDong
FROM   dbo.F03EscalationRules
GROUP BY RequestModule, IsActive
ORDER BY RequestModule, IsActive;

IF OBJECT_ID('tempdb..#MapEsc') IS NOT NULL DROP TABLE #MapEsc;

SELECT r.Id, r.RequestModule AS Cu,
       Moi = CASE UPPER(LTRIM(RTRIM(r.RequestModule)))
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
INTO   #MapEsc
FROM   dbo.F03EscalationRules r;

IF EXISTS (SELECT 1 FROM #MapEsc WHERE Moi IS NULL)
BEGIN
    SELECT * FROM #MapEsc WHERE Moi IS NULL;
    THROW 57801, N'Co RequestModule khong nhan dien duoc - dung lai.', 1;
END

-- Dòng sẽ đổi (so sánh phân biệt hoa/thường)
SELECT Cu, Moi, COUNT(*) AS SoDongSeDoi
FROM   #MapEsc
WHERE  Cu COLLATE Latin1_General_BIN2 <> Moi COLLATE Latin1_General_BIN2
GROUP BY Cu, Moi;

IF @DryRun = 0
BEGIN
    IF OBJECT_ID('dbo.F03EscalationRules_bak_requestmodule') IS NULL
        SELECT * INTO dbo.F03EscalationRules_bak_requestmodule FROM dbo.F03EscalationRules;

    BEGIN TRANSACTION;

    UPDATE r
    SET    r.RequestModule = m.Moi
    FROM   dbo.F03EscalationRules r
    JOIN   #MapEsc m ON m.Id = r.Id
    WHERE  r.RequestModule COLLATE Latin1_General_BIN2 <> m.Moi COLLATE Latin1_General_BIN2;

    PRINT CONCAT(N'Da chuan hoa: ', @@ROWCOUNT, N' dong. Backup: dbo.F03EscalationRules_bak_requestmodule');

    COMMIT TRANSACTION;
END
ELSE
    PRINT N'@DryRun = 1: CHUA ghi gi. Dat @DryRun = 0 de ghi that.';

-- Dòng trùng sau chuẩn hóa (cùng loại đơn/cấp/phòng) - chỉ liệt kê để xem
SELECT m.Moi AS RequestModule, r.Level, r.DeptCode,
       COUNT(*) AS SoDong, MIN(r.Id) AS IdGiuLai, MAX(r.Id) AS IdTrung
FROM   dbo.F03EscalationRules r
JOIN   #MapEsc m ON m.Id = r.Id
WHERE  r.IsActive = 1
GROUP BY m.Moi, r.Level, r.DeptCode
HAVING COUNT(*) > 1;

DROP TABLE #MapEsc;
GO
