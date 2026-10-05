/*
===============================================================================
71_DepartmentCodeInt.sql
===============================================================================
Canonical HRM department identity:
    HRM.tblNhanVien.NVMaBP
    HRM.tblBoPhan.BPMa
Both are INT. FVN_REGISTER must preserve that type instead of converting the
department code to nvarchar/string.

This migration converts the FVN department-code columns in-place. It fails
closed when existing values are not numeric.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @Bad nvarchar(max) = N'';

IF EXISTS (SELECT 1 FROM dbo.F03Departments WHERE TRY_CONVERT(int, DeptCode) IS NULL)
    SET @Bad += N'F03Departments.DeptCode; ';
IF EXISTS (SELECT 1 FROM dbo.F03Employees WHERE TRY_CONVERT(int, DeptCode) IS NULL)
    SET @Bad += N'F03Employees.DeptCode; ';
IF EXISTS (SELECT 1 FROM dbo.F03Users WHERE DeptCode IS NOT NULL AND TRY_CONVERT(int, DeptCode) IS NULL)
    SET @Bad += N'F03Users.DeptCode; ';
IF EXISTS (SELECT 1 FROM dbo.F03Approvers WHERE TRY_CONVERT(int, ApproverDeptCode) IS NULL OR TRY_CONVERT(int, ApproveForDeptCode) IS NULL)
    SET @Bad += N'F03Approvers.DeptCode; ';
IF OBJECT_ID(N'dbo.F03ApprovalPolicies',N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.F03ApprovalPolicies WHERE TRY_CONVERT(int, DeptCode) IS NULL)
    SET @Bad += N'F03ApprovalPolicies.DeptCode; ';
IF OBJECT_ID(N'dbo.F03HrmUserRoleRules',N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.F03HrmUserRoleRules WHERE DeptCode IS NOT NULL AND TRY_CONVERT(int, DeptCode) IS NULL)
    SET @Bad += N'F03HrmUserRoleRules.DeptCode; ';
IF OBJECT_ID(N'dbo.F03ManagedScopes',N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.F03ManagedScopes WHERE DeptCode IS NOT NULL AND TRY_CONVERT(int, DeptCode) IS NULL)
    SET @Bad += N'F03ManagedScopes.DeptCode; ';
IF OBJECT_ID(N'dbo.F03ManagedScopes',N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.F03ManagedScopes WHERE SubDepartmentCode IS NOT NULL AND TRY_CONVERT(int, SubDepartmentCode) IS NULL)
    SET @Bad += N'F03ManagedScopes.SubDepartmentCode; ';
IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculated',N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.F03HrmAttendanceCalculated WHERE DeptCode IS NOT NULL AND TRY_CONVERT(int, DeptCode) IS NULL)
    SET @Bad += N'F03HrmAttendanceCalculated.DeptCode; ';
IF OBJECT_ID(N'dbo.F03HrmOTActual',N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.F03HrmOTActual WHERE DeptCode IS NOT NULL AND TRY_CONVERT(int, DeptCode) IS NULL)
    SET @Bad += N'F03HrmOTActual.DeptCode; ';
IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculationRun',N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.F03HrmAttendanceCalculationRun WHERE DeptCode IS NOT NULL AND TRY_CONVERT(int, DeptCode) IS NULL)
    SET @Bad += N'F03HrmAttendanceCalculationRun.DeptCode; ';

IF @Bad <> N''
    THROW 51401, N'Cannot convert DepartmentCode to INT; non-numeric existing values found: ' + @Bad, 1;
GO

/* Foreign keys whose dependent columns are being converted. */
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03Employees_F03Departments')
    ALTER TABLE dbo.F03Employees DROP CONSTRAINT FK_F03Employees_F03Departments;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03ApprovalPolicies_F03Departments')
    ALTER TABLE dbo.F03ApprovalPolicies DROP CONSTRAINT FK_F03ApprovalPolicies_F03Departments;
GO

/* Canonical lookup indexes are recreated after the type conversion. */
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03Department_Code' AND object_id=OBJECT_ID(N'dbo.F03Departments'))
    DROP INDEX IX_F03Department_Code ON dbo.F03Departments;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Approver_Lookup' AND object_id=OBJECT_ID(N'dbo.F03Approvers'))
    DROP INDEX IX_Approver_Lookup ON dbo.F03Approvers;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03ApprovalPolicies_Request_Dept_Position_Level' AND object_id=OBJECT_ID(N'dbo.F03ApprovalPolicies'))
    DROP INDEX UX_F03ApprovalPolicies_Request_Dept_Position_Level ON dbo.F03ApprovalPolicies;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03ApprovalPolicies_Route' AND object_id=OBJECT_ID(N'dbo.F03ApprovalPolicies'))
    DROP INDEX IX_F03ApprovalPolicies_Route ON dbo.F03ApprovalPolicies;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_OTLimitRule_Lookup' AND object_id=OBJECT_ID(N'dbo.F03OTLimitRules'))
    DROP INDEX IX_OTLimitRule_Lookup ON dbo.F03OTLimitRules;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EquipmentAsset_Dept' AND object_id=OBJECT_ID(N'dbo.F03EquipmentAssets'))
    DROP INDEX IX_F03EquipmentAsset_Dept ON dbo.F03EquipmentAssets;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_EscalationRule_Lookup' AND object_id=OBJECT_ID(N'dbo.F03EscalationRules'))
    DROP INDEX IX_EscalationRule_Lookup ON dbo.F03EscalationRules;
GO

ALTER TABLE dbo.F03Departments ALTER COLUMN DeptCode int NOT NULL;
ALTER TABLE dbo.F03Departments ALTER COLUMN ParentDeptCode int NULL;
ALTER TABLE dbo.F03Employees ALTER COLUMN DeptCode int NOT NULL;
ALTER TABLE dbo.F03Users ALTER COLUMN DeptCode int NULL;
ALTER TABLE dbo.F03Approvers ALTER COLUMN ApproverDeptCode int NOT NULL;
ALTER TABLE dbo.F03Approvers ALTER COLUMN ApproveForDeptCode int NOT NULL;
IF OBJECT_ID(N'dbo.F03ApprovalPolicies',N'U') IS NOT NULL
    ALTER TABLE dbo.F03ApprovalPolicies ALTER COLUMN DeptCode int NOT NULL;
IF OBJECT_ID(N'dbo.F03HrmUserRoleRules',N'U') IS NOT NULL
    ALTER TABLE dbo.F03HrmUserRoleRules ALTER COLUMN DeptCode int NULL;
IF OBJECT_ID(N'dbo.F03ManagedScopes',N'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.F03ManagedScopes ALTER COLUMN DeptCode int NULL;
    ALTER TABLE dbo.F03ManagedScopes ALTER COLUMN SubDepartmentCode int NULL;
END;
IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculated',N'U') IS NOT NULL
    ALTER TABLE dbo.F03HrmAttendanceCalculated ALTER COLUMN DeptCode int NULL;
IF OBJECT_ID(N'dbo.F03HrmOTActual',N'U') IS NOT NULL
    ALTER TABLE dbo.F03HrmOTActual ALTER COLUMN DeptCode int NULL;
IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculationRun',N'U') IS NOT NULL
    ALTER TABLE dbo.F03HrmAttendanceCalculationRun ALTER COLUMN DeptCode int NULL;
IF OBJECT_ID(N'dbo.F03OTLimitRules',N'U') IS NOT NULL
    ALTER TABLE dbo.F03OTLimitRules ALTER COLUMN DeptCode int NULL;
IF OBJECT_ID(N'dbo.F03EquipmentAssets',N'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.F03EquipmentAssets ALTER COLUMN DeptCode int NOT NULL;
    ALTER TABLE dbo.F03EquipmentAssets ALTER COLUMN OperatingResponsibleDeptCode int NULL;
END;
IF OBJECT_ID(N'dbo.F03EscalationRules',N'U') IS NOT NULL
    ALTER TABLE dbo.F03EscalationRules ALTER COLUMN DeptCode int NULL;
GO

CREATE UNIQUE INDEX IX_F03Department_Code ON dbo.F03Departments(DeptCode);
CREATE INDEX IX_Approver_Lookup ON dbo.F03Approvers(RequestType,ApproveForDeptCode);
IF OBJECT_ID(N'dbo.F03ApprovalPolicies',N'U') IS NOT NULL
BEGIN
    CREATE UNIQUE INDEX UX_F03ApprovalPolicies_Request_Dept_Position_Level
        ON dbo.F03ApprovalPolicies(RequestType,DeptCode,PositionCode,Level);
    CREATE INDEX IX_F03ApprovalPolicies_Route
        ON dbo.F03ApprovalPolicies(RequestType,DeptCode,PositionCode,IsActive,Sequence,Level);
END;
IF OBJECT_ID(N'dbo.F03OTLimitRules',N'U') IS NOT NULL
    CREATE INDEX IX_OTLimitRule_Lookup ON dbo.F03OTLimitRules(LimitType,PositionCode,DeptCode);
IF OBJECT_ID(N'dbo.F03EquipmentAssets',N'U') IS NOT NULL
    CREATE INDEX IX_F03EquipmentAsset_Dept ON dbo.F03EquipmentAssets(DeptCode);
IF OBJECT_ID(N'dbo.F03EscalationRules',N'U') IS NOT NULL
    CREATE INDEX IX_EscalationRule_Lookup ON dbo.F03EscalationRules(RequestModule,Level,DeptCode);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03Employees_F03Departments')
BEGIN
    ALTER TABLE dbo.F03Employees
        ADD CONSTRAINT FK_F03Employees_F03Departments
        FOREIGN KEY (DeptCode) REFERENCES dbo.F03Departments(DeptCode);
END;
IF OBJECT_ID(N'dbo.F03ApprovalPolicies',N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03ApprovalPolicies_F03Departments')
BEGIN
    ALTER TABLE dbo.F03ApprovalPolicies
        ADD CONSTRAINT FK_F03ApprovalPolicies_F03Departments
        FOREIGN KEY (DeptCode) REFERENCES dbo.F03Departments(DeptCode);
END;
GO

PRINT N'DepartmentCode standardized to INT across HRM/FVN department-scope columns.';
GO
