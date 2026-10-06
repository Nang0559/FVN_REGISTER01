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

        /* OperatingResponsibleDeptCode is added later by 45_EquipmentResponsibilityAndRepair.sql.
           Never reference it statically here (Msg 207 at batch compile); it is handled
           dynamically through @DeptTargets below when it already exists as text. */
    END;

    IF OBJECT_ID(N'dbo.F03EscalationRules',N'U') IS NOT NULL
        UPDATE E
        SET E.DeptCode = CONVERT(nvarchar(20), M.HrmDeptCode)
        FROM dbo.F03EscalationRules E
        INNER JOIN @Map M ON M.LegacyCode = CONVERT(nvarchar(20), E.DeptCode);

    /*
      Normalize additional module department-code columns introduced by later
      module migrations. Only textual legacy columns are altered; canonical INT
      columns are left untouched. Unknown values fail closed.
    */
    DECLARE @DeptTargets TABLE
    (
        TableName sysname NOT NULL,
        ColumnName sysname NOT NULL,
        PRIMARY KEY (TableName, ColumnName)
    );

    INSERT INTO @DeptTargets(TableName, ColumnName)
    VALUES
        (N'F03Departments', N'ParentDeptCode'),
        (N'F03EquipmentAssets', N'OperatingResponsibleDeptCode'),
        (N'F03AccessChangeRequests', N'DeptCode'),
        (N'F03PasswordResetRequests', N'DeptCode'),
        (N'F03OTEmployees', N'DeptCode'),
        (N'F03EquipmentInspectionTemplates', N'DeptCode'),
        (N'F03EquipmentRepairHistory', N'ResponsibleDeptCode'),
        (N'F03EquipmentRequests', N'DeptCode'),
        (N'F03EquipmentRequests', N'RepairResponsibleDeptCode'),
        (N'F03AttendanceStaging', N'DeptCode'),
        (N'F03HrmAttendanceCalculated', N'DeptCode'),
        (N'F03SyncReviewFlag', N'OldDeptCode'),
        (N'F03SyncReviewFlag', N'NewDeptCode'),
        (N'F03SyncReviewFlag', N'CurrentApproveForDeptCode'),
        (N'F03SyncReviewFlag', N'SuggestedApproveForDeptCode');

    DECLARE @TargetTable sysname, @TargetColumn sysname, @Sql nvarchar(max), @Nullable nvarchar(10);

    DECLARE dept_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName, ColumnName
        FROM @DeptTargets
        ORDER BY TableName, ColumnName;

    OPEN dept_cursor;
    FETCH NEXT FROM dept_cursor INTO @TargetTable, @TargetColumn;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF OBJECT_ID(N'dbo.' + @TargetTable, N'U') IS NOT NULL
           AND COL_LENGTH(N'dbo.' + @TargetTable, @TargetColumn) IS NOT NULL
           AND EXISTS
           (
               SELECT 1
               FROM sys.columns c
               JOIN sys.types t ON t.user_type_id = c.user_type_id
               WHERE c.object_id = OBJECT_ID(N'dbo.' + @TargetTable)
                 AND c.name = @TargetColumn
                 AND t.name IN (N'varchar',N'char',N'nvarchar',N'nchar')
           )
        BEGIN
            SET @Sql = N'
                UPDATE T
                SET ' + QUOTENAME(@TargetColumn) + N' =
                    CASE UPPER(LTRIM(RTRIM(CONVERT(nvarchar(20), T.' + QUOTENAME(@TargetColumn) + N')))
                        WHEN N''FIN''  THEN N''14''
                        WHEN N''HR''   THEN N''13''
                        WHEN N''PROD'' THEN N''12''
                        WHEN N''QA''   THEN N''10''
                        WHEN N''IT''   THEN N''57''
                        ELSE T.' + QUOTENAME(@TargetColumn) + N'
                    END
                FROM dbo.' + QUOTENAME(@TargetTable) + N' AS T;

                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.' + QUOTENAME(@TargetTable) + N'
                    WHERE ' + QUOTENAME(@TargetColumn) + N' IS NOT NULL
                      AND TRY_CONVERT(int, ' + QUOTENAME(@TargetColumn) + N') IS NULL
                )
                    THROW 51405, N''Unmapped textual DepartmentCode remains in ' + REPLACE(@TargetTable,'''','''''') + N'.' + REPLACE(@TargetColumn,'''','''''') + N'.'', 1;';
            EXEC sp_executesql @Sql;

            SELECT @Nullable =
                CASE WHEN c.is_nullable = 1 THEN N'NULL' ELSE N'NOT NULL' END
            FROM sys.columns c
            WHERE c.object_id = OBJECT_ID(N'dbo.' + @TargetTable)
              AND c.name = @TargetColumn;

            SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@TargetTable)
                     + N' ALTER COLUMN ' + QUOTENAME(@TargetColumn)
                     + N' int ' + @Nullable + N';';
            EXEC sp_executesql @Sql;
        END;

        FETCH NEXT FROM dept_cursor INTO @TargetTable, @TargetColumn;
    END;

    CLOSE dept_cursor;
    DEALLOCATE dept_cursor;

    IF OBJECT_ID(N'dbo.F03Approvers',N'U') IS NOT NULL UPDATE dbo.F03Approvers SET ApproveForDeptCode=N'0' WHERE UPPER(LTRIM(RTRIM(CONVERT(nvarchar(20),ApproveForDeptCode))))=N'ALL';

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
    /* Nested IFs on purpose: "IF OBJECT_ID(..) IS NOT NULL AND EXISTS (SELECT .. FROM <table>)" is ONE
       statement and fails with Msg 208 at compile time when the table does not exist yet. */
    IF OBJECT_ID(N'dbo.F03ApprovalPolicies',N'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.F03ApprovalPolicies WHERE TRY_CONVERT(int, DeptCode) IS NULL)
            SET @Bad += N'F03ApprovalPolicies.DeptCode; ';
    END;
    IF OBJECT_ID(N'dbo.F03HrmUserRoleRules',N'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.F03HrmUserRoleRules WHERE DeptCode IS NOT NULL AND TRY_CONVERT(int, DeptCode) IS NULL)
            SET @Bad += N'F03HrmUserRoleRules.DeptCode; ';
    END;
    IF OBJECT_ID(N'dbo.F03ManagedScopes',N'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.F03ManagedScopes WHERE DeptCode IS NOT NULL AND TRY_CONVERT(int, DeptCode) IS NULL)
            SET @Bad += N'F03ManagedScopes.DeptCode; ';
        IF EXISTS (SELECT 1 FROM dbo.F03ManagedScopes WHERE SubDepartmentCode IS NOT NULL AND TRY_CONVERT(int, SubDepartmentCode) IS NULL)
            SET @Bad += N'F03ManagedScopes.SubDepartmentCode; ';
    END;

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

    /* Convert to INT only where the column exists and is not already INT/nullability-correct.
       Dynamic SQL keeps columns that are created by later scripts out of this batch's
       compile-time name resolution (avoids Msg 207). */
    DECLARE @IntTargets TABLE
    (
        TableName  sysname NOT NULL,
        ColumnName sysname NOT NULL,
        IsNullable bit     NOT NULL,
        PRIMARY KEY (TableName, ColumnName)
    );

    INSERT INTO @IntTargets (TableName, ColumnName, IsNullable)
    VALUES
        (N'F03Departments',                N'DeptCode',                      0),
        (N'F03Departments',                N'ParentDeptCode',                1),
        (N'F03Employees',                  N'DeptCode',                      0),
        (N'F03Users',                      N'DeptCode',                      1),
        (N'F03Approvers',                  N'ApproverDeptCode',              0),
        (N'F03Approvers',                  N'ApproveForDeptCode',            0),
        (N'F03ApprovalPolicies',           N'DeptCode',                      0),
        (N'F03HrmUserRoleRules',           N'DeptCode',                      1),
        (N'F03ManagedScopes',              N'DeptCode',                      1),
        (N'F03ManagedScopes',              N'SubDepartmentCode',             1),
        (N'F03HrmAttendanceCalculated',    N'DeptCode',                      1),
        (N'F03HrmOTActual',                N'DeptCode',                      1),
        (N'F03HrmAttendanceCalculationRun',N'DeptCode',                      1),
        (N'F03OTLimitRules',               N'DeptCode',                      1),
        (N'F03EquipmentAssets',            N'DeptCode',                      0),
        (N'F03EquipmentAssets',            N'OperatingResponsibleDeptCode',  1),
        (N'F03EscalationRules',            N'DeptCode',                      1);

    DECLARE @IntNullable bit;

    DECLARE int_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName, ColumnName, IsNullable
        FROM @IntTargets
        ORDER BY TableName, ColumnName;

    OPEN int_cursor;
    FETCH NEXT FROM int_cursor INTO @TargetTable, @TargetColumn, @IntNullable;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF EXISTS
        (
            SELECT 1
            FROM sys.columns c
            JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(N'dbo.' + @TargetTable)
              AND c.name = @TargetColumn
              AND (t.name <> N'int' OR c.is_nullable <> @IntNullable)
        )
        BEGIN
            SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@TargetTable)
                     + N' ALTER COLUMN ' + QUOTENAME(@TargetColumn)
                     + N' int ' + CASE WHEN @IntNullable = 1 THEN N'NULL' ELSE N'NOT NULL' END + N';';
            EXEC sp_executesql @Sql;
        END;

        FETCH NEXT FROM int_cursor INTO @TargetTable, @TargetColumn, @IntNullable;
    END;

    CLOSE int_cursor;
    DEALLOCATE int_cursor;

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
