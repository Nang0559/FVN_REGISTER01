/*
===============================================================================
53_ApprovalUnifiedFoundation.sql

Purpose:
- Freeze requester identity in F03ApprovalSnapshots.
- Add a dedicated Payroll approval capability without renumbering existing codes.
- Do NOT seed an approval policy/approver here: Payroll/period approval routing
  must use the existing PositionCode -> F03ApprovalPolicy -> F03Approver model
  and therefore remains administrator-configurable.
===============================================================================
*/

USE [FVN_REGISTER];
GO

IF OBJECT_ID(N'dbo.F03ApprovalSnapshots', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.F03ApprovalSnapshots', N'RequesterEmployeeCode') IS NULL
        ALTER TABLE dbo.F03ApprovalSnapshots
            ADD RequesterEmployeeCode nvarchar(50) NULL;
END;
GO

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.F03Functions
        WHERE FunctionCode = 2810
    )
    BEGIN
        INSERT INTO dbo.F03Functions
        (
            IsActive,
            CreatedBy,
            LastModifiedSource,
            CreatedAt,
            FunctionCode,
            FunctionName,
            Detail
        )
        VALUES
        (
            1,
            0,
            N'SYSTEM_APPROVAL_UNIFIED',
            GETDATE(),
            2810,
            N'Phê duyệt bảng công/bảng lương',
            N'Phê duyệt kỳ đã chốt trước khi được in hoặc xuất dữ liệu. Dùng chung Approval Engine; không thay đổi luồng Leave/OT/Trip/Equipment.'
        );
    END;
END;
GO

/*
Configuration check only. No automatic approver is created here.
The administrator must configure F03ApprovalPolicies + F03Approvers for
RequestType = Payroll using the existing PositionCode-based routing.
*/
SELECT
    FunctionCode,
    FunctionName,
    IsActive
FROM dbo.F03Functions
WHERE FunctionCode = 2810;
GO
