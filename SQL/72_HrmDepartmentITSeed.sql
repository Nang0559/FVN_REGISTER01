/*
===============================================================================
72_HrmDepartmentITSeed.sql
===============================================================================
Creates the HRM department identity used by FVN_REGISTER for the management /
system IT unit.

BPMa 57 is the next department code after the currently supplied HRM list
(1..56). Run this script against the HRM database before relying on HRM
employee synchronization for the IT department.

This script is intentionally NOT included in FVN_REGISTER's automatic deploy
chain because it changes the external HRM database.
===============================================================================
*/
USE [HRM];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.tblBoPhan
    WHERE BPMa = 57
      AND ISNULL(BPTen, N'') <> N'IT'
)
BEGIN
    THROW 51501, N'HRM BPMa=57 is already assigned to another department.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.tblBoPhan
    WHERE BPMa = 57
)
BEGIN
    INSERT INTO dbo.tblBoPhan
    (
        BPMa,
        BPTen,
        BPMaCha,
        BPUuTien,
        BPHienThiBC,
        User1,
        Date1,
        User2,
        Date2,
        DLocked,
        User3,
        Date3
    )
    VALUES
    (
        57,
        N'IT',
        0,
        0,
        1,
        NULL,
        GETDATE(),
        NULL,
        NULL,
        NULL,
        NULL,
        NULL
    );
END;
GO

SELECT
    BPMa,
    BPTen,
    BPMaCha,
    BPUuTien,
    BPHienThiBC,
    DLocked
FROM dbo.tblBoPhan
WHERE BPMa = 57;
GO
