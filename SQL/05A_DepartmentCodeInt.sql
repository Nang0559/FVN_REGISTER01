/*
===============================================================================
05A_DepartmentCodeInt.sql
===============================================================================
Canonical department identity migration:
    HRM.tblNhanVien.NVMaBP / HRM.tblBoPhan.BPMa = INT
    FVN_REGISTER department-code columns = INT

Legacy FVN databases may contain textual department codes. This migration
normalizes the known legacy codes to the HRM/FVN numeric identity and then
converts the affected columns to INT.

Known mapping:
    FIN  -> 14 Finance& Account
    HR   -> 13 General Affairs
    PROD -> 12 Production Control
    QA   -> 10 Quality Control
    IT   -> 57 IT (new management/system department)

IMPORTANT:
    BPMa 57 must be created in HRM.tblBoPhan as the IT department before HRM
    employee synchronization is expected to supply employees for this unit.
    FVN creates/normalizes its own F03Departments row to the same code.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Map TABLE
    (
        LegacyCode nvarchar(20) NOT NULL PRIMARY KEY,
        HrmDeptCode int NOT NULL,
        DeptName nvarchar(100) NOT NULL
    );

    INSERT INTO @Map (LegacyCode, HrmDeptCode, DeptName)
    VALUES
        (N'FIN',  14, N'Finance& Account'),
        (N'HR',   13, N'General Affairs'),
        (N'PROD', 12, N'Production Control'),
        (N'QA',   10, N'Quality Control'),
        (N'IT',   57, N'IT');

    /* 57 is intentionally reserved for the new IT management/system unit.
       Do not silently overwrite an existing department using another code. */
    IF EXISTS
    (
        SELECT 1
        FROM dbo.F03Departments
        WHERE TRY_CONVERT(int, DeptCode) = 57
          AND ISNULL(DeptName, N'') <> N'IT'
    )
    BEGIN
        THROW 51404, N'Department code 57 is already used by a non-IT department.', 1;
    END;

    /* Normalize legacy department values while the columns are still nvarchar. */
    IF COL_LENGTH(N'dbo.F03Departments', N'DeptCode') IS NOT NULL
        UPDATE D
        SET D.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode),
            D.DeptName = M.DeptName
        FROM dbo.F03Departments D
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), D.DeptCode);

    /* If the legacy IT row was absent, create the canonical FVN department.
       This is idempotent and does not create a duplicate code 57. */
    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.F03Departments
        WHERE TRY_CONVERT(int, DeptCode) = 57
    )
    BEGIN
        INSERT INTO dbo.F03Departments
        (
            IsActive, CreatedBy, LastModifiedSource, CreatedAt,
            DeptCode, DeptName, DisplayPriority, ShowInReport
        )
        VALUES
        (
            1, 0, N'DepartmentCodeInt', GETDATE(),
            57, N'IT', 0, 1
        );
    END;

    /* Normalize all dependent department references before ALTER COLUMN. */
    IF COL_LENGTH(N'dbo.F03Employees', N'DeptCode') IS NOT NULL
        UPDATE E
        SET E.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03Employees E
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), E.DeptCode);

    IF COL_LENGTH(N'dbo.F03Users', N'DeptCode') IS NOT NULL
        UPDATE U
        SET U.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03Users U
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), U.DeptCode);

    IF COL_LENGTH(N'dbo.F03Approvers', N'ApproverDeptCode') IS NOT NULL
        UPDATE A
        SET A.ApproverDeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03Approvers A
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), A.ApproverDeptCode);

    IF COL_LENGTH(N'dbo.F03Approvers', N'ApproveForDeptCode') IS NOT NULL
        UPDATE A
        SET A.ApproveForDeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03Approvers A
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), A.ApproveForDeptCode);

    IF OBJECT_ID(N'dbo.F03ApprovalPolicies',N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.F03ApprovalPolicies',N'DeptCode') IS NOT NULL
       AND EXISTS
       (
           SELECT 1
           FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.F03ApprovalPolicies')
             AND name = N'DeptCode'
             AND system_type_id IN (167,175,231,239)
       )
    BEGIN
        UPDATE P
        SET P.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03ApprovalPolicies P
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), P.DeptCode);
    END;

    IF OBJECT_ID(N'dbo.F03HrmUserRoleRules',N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.F03HrmUserRoleRules',N'DeptCode') IS NOT NULL
    BEGIN
        UPDATE R
        SET R.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03HrmUserRoleRules R
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), R.DeptCode);
    END;

    IF OBJECT_ID(N'dbo.F03ManagedScopes',N'U') IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'dbo.F03ManagedScopes',N'DeptCode') IS NOT NULL
        BEGIN
            UPDATE S
            SET S.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
            FROM dbo.F03ManagedScopes S
            INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), S.DeptCode);
        END;
        IF COL_LENGTH(N'dbo.F03ManagedScopes',N'SubDepartmentCode') IS NOT NULL
        BEGIN
            UPDATE S
            SET S.SubDepartmentCode = CONVERT(nvarchar(20), M.HrmDeptCode)
            FROM dbo.F03ManagedScopes S
            INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), S.SubDepartmentCode);
        END;
    END;

    IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculated',N'U') IS NOT NULL
        UPDATE A
        SET A.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03HrmAttendanceCalculated A
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), A.DeptCode);

    IF OBJECT_ID(N'dbo.F03HrmOTActual',N'U') IS NOT NULL
        UPDATE O
        SET O.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03HrmOTActual O
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), O.DeptCode);

    IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculationRun',N'U') IS NOT NULL
        UPDATE R
        SET R.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03HrmAttendanceCalculationRun R
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), R.DeptCode);

    IF OBJECT_ID(N'dbo.F03OTLimitRules',N'U') IS NOT NULL
        UPDATE R
        SET R.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03OTLimitRules R
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), R.DeptCode);

    IF OBJECT_ID(N'dbo.F03EquipmentAssets',N'U') IS NOT NULL
    BEGIN
        UPDATE E
        SET E.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03EquipmentAssets E
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), E.DeptCode);

        UPDATE E
        SET E.OperatingResponsibleDeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03EquipmentAssets E
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), E.OperatingResponsibleDeptCode);
    END;

    IF OBJECT_ID(N'dbo.F03EscalationRules',N'U') IS NOT NULL
        UPDATE E
        SET E.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03EscalationRules E
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), E.DeptCode);

    /* Fail closed if any non-numeric legacy department code remains. */
    DECLARE @Bad nvarchar(max) = N'';

    IF EXISTS (SELECT 1 FROM dbo.F03Departments WHERE TRY_CONVERT(int, DeptCode) IS NULL)
        SET @Bad += N'F03Departments.DeptCode; ';
    IF EXISTS (SELECT 1 FROM dbo.F03Employees WHERE TRY_CONVERT(int, DeptCode) IS NULL)
        SET @Bad += N'F03Employees.DeptCode; ';
    IF EXISTS (SELECT 1 FROM dbo.F03Users WHERE DeptCode IS NOT NULL AND TRY_CONVERT(int, DeptCode) IS NULL)
        SET @Bad += N'F03Users.DeptCode; ';
    IF EXISTS (SELECT 1 FROM dbo.F03Approvers WHERE TRY_CONVERT(int, ApproverDeptCode) IS NULL OR TRY_CONVERT(int, ApproveForDeptCode) IS NULL)
        SET @Bad += N'F03Approvers.ApproverDeptCode/ApproveForDeptCode; ';
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

    IF @Bad <> N''
    BEGIN
        DECLARE @Message nvarchar(2048) =
            N'Cannot convert DepartmentCode to INT; unmapped legacy values remain: ' + @Bad;
        THROW 51401, @Message, 1;
    END;

    /* Drop the known foreign keys before converting their dependent columns. */
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03Employees_F03Departments')
        ALTER TABLE dbo.F03Employees DROP CONSTRAINT FK_F03Employees_F03Departments;
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03ApprovalPolicies_F03Departments')
        ALTER TABLE dbo.F03ApprovalPolicies DROP CONSTRAINT FK_F03ApprovalPolicies_F03Departments;

    /* Recreate the canonical indexes after conversion. */
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03Department_Code' AND object_id=OBJECT_ID(N'dbo.F03Departments'))
        DROP INDEX IX_F03Department_Code ON dbo.F03Departments;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03Departments_DeptCode' AND object_id=OBJECT_ID(N'dbo.F03Departments'))
        DROP INDEX UX_F03Departments_DeptCode ON dbo.F03Departments;
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

    CREATE UNIQUE INDEX UX_F03Departments_DeptCode ON dbo.F03Departments(DeptCode);
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

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03Employees_F03Departments')
        ALTER TABLE dbo.F03Employees
            ADD CONSTRAINT FK_F03Employees_F03Departments
            FOREIGN KEY (DeptCode) REFERENCES dbo.F03Departments(DeptCode);

    IF OBJECT_ID(N'dbo.F03ApprovalPolicies',N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03ApprovalPolicies_F03Departments')
        ALTER TABLE dbo.F03ApprovalPolicies
            ADD CONSTRAINT FK_F03ApprovalPolicies_F03Departments
            FOREIGN KEY (DeptCode) REFERENCES dbo.F03Departments(DeptCode);

    COMMIT TRANSACTION;
    PRINT N'DepartmentCode migration to INT completed. Legacy FIN/HR/PROD/QA/IT values were normalized; IT uses department code 57.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
