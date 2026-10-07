/*
FVN_REGISTER - HRM security/performance hardening

Purpose:
  1. Prevent usp_ReconcileEmployeeUsers from deactivating manually-owned F03Users.
  2. Prevent usp_ReconcileEmployeeApprovers from stamping active F03Users as HRM-owned.
  3. Align the SQL default password hash with the C# HRM provisioning password Fcc@123.
  4. Keep DepartmentCode comparisons typed as int/int? (no LTRIM/RTRIM/casts).

IMPORTANT:
  - This script does NOT auto-reclassify existing F03Users whose LastModifiedSource
    was already polluted by the old procedure. Review those rows before correcting
    ownership manually.
  - Run this script against FVN_REGISTER after pulling the application commit.
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* -------------------------------------------------------------------------
   1. Harden usp_ReconcileEmployeeUsers.
      Only HRM-owned users may have IsActive changed by HRM reconciliation.
   ------------------------------------------------------------------------- */
DECLARE @ProcDefinition nvarchar(max);
DECLARE @Original nvarchar(max);
DECLARE @Patched nvarchar(max);

SELECT @ProcDefinition = OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_ReconcileEmployeeUsers', N'P'));

IF @ProcDefinition IS NULL
    THROW 51074, 'dbo.usp_ReconcileEmployeeUsers was not found.', 1;

SET @Original = @ProcDefinition;
SET @Patched = @ProcDefinition;

SET @Patched = REPLACE(
    @Patched,
    N'@DefaultPasswordHash nvarchar(255)=N''6ed2b24e5c570014cc5de09121c111ca'' -- MD5("FVN@123")',
    N'@DefaultPasswordHash nvarchar(255)=N''edbf6b4c784a9d55a68f115834be9d51'' -- MD5("Fcc@123")');

SET @Patched = REPLACE(
    @Patched,
    N'WHERE (@EmployeeCode IS NULL OR e.EmployeeCode=@EmployeeCode);',
    N'WHERE (@EmployeeCode IS NULL OR e.EmployeeCode=@EmployeeCode)\n      AND u.LastModifiedSource=N''HRM'';', 1);

IF @Patched = @Original
    THROW 51075, 'usp_ReconcileEmployeeUsers hardening pattern was not found; procedure was not changed.', 1;

EXEC sys.sp_executesql @Patched;
GO

/* -------------------------------------------------------------------------
   2. Harden usp_ReconcileEmployeeApprovers.
      Its F03User mirror update must not take ownership of manual accounts.
   ------------------------------------------------------------------------- */
DECLARE @ProcDefinition2 nvarchar(max);
DECLARE @Original2 nvarchar(max);
DECLARE @Patched2 nvarchar(max);

SELECT @ProcDefinition2 = OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_ReconcileEmployeeApprovers', N'P'));

IF @ProcDefinition2 IS NULL
    THROW 51076, 'dbo.usp_ReconcileEmployeeApprovers was not found.', 1;

SET @Original2 = @ProcDefinition2;
SET @Patched2 = @ProcDefinition2;

SET @Patched2 = REPLACE(
    @Patched2,
    N'WHERE e.IsActive=1;\n\n    /*\n      Positions that are no longer referenced',
    N'WHERE e.IsActive=1\n      AND u.LastModifiedSource=N''HRM'';\n\n    /*\n      Positions that are no longer referenced');

IF @Patched2 = @Original2
    THROW 51077, 'usp_ReconcileEmployeeApprovers user-ownership pattern was not found; procedure was not changed.', 1;

EXEC sys.sp_executesql @Patched2;
GO

/* -------------------------------------------------------------------------
   3. Verification.
   ------------------------------------------------------------------------- */
SELECT
    ProcedureName = N'usp_ReconcileEmployeeUsers',
    DefinitionContainsHrmOwnershipGuard = CASE
        WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_ReconcileEmployeeUsers', N'P')) LIKE N'%u.LastModifiedSource=N''HRM''%'
        THEN 1 ELSE 0 END,
    DefinitionContainsNewPasswordHash = CASE
        WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_ReconcileEmployeeUsers', N'P')) LIKE N'%edbf6b4c784a9d55a68f115834be9d51%'
        THEN 1 ELSE 0 END;

SELECT
    ProcedureName = N'usp_ReconcileEmployeeApprovers',
    DefinitionContainsHrmUserOwnershipGuard = CASE
        WHEN OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_ReconcileEmployeeApprovers', N'P')) LIKE N'%u.LastModifiedSource=N''HRM''%'
        THEN 1 ELSE 0 END;

/* Review candidates potentially affected by the historical ownership bug. */
SELECT
    u.Id,
    u.EmployeeCode,
    u.FullName,
    u.IsActive,
    u.LastModifiedSource,
    e.IsActive AS EmployeeIsActive
FROM dbo.F03Users u
INNER JOIN dbo.F03Employees e ON e.EmployeeCode=u.EmployeeCode
WHERE u.LastModifiedSource=N'HRM'
  AND u.IsActive=0
  AND e.IsActive=1
ORDER BY u.EmployeeCode;
GO
