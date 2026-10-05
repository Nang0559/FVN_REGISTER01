/*
===============================================================================
71_DepartmentCodeInt.sql
===============================================================================
Canonical department identity (single standard for the whole FVN_REGISTER DB):

    HRM.dbo.tblBoPhan.BPMa  = INT   (department master)
    HRM.dbo.tblNhanVien.NVMaBP = INT
    FVN_REGISTER department-code columns = INT  (same value as BPMa)

What this script does (idempotent, one transaction):
  1. Discovers EVERY string-typed department-code column in dbo
     (DeptCode, ParentDeptCode, SubDepartmentCode, ApproverDeptCode,
      ApproveForDeptCode, OperatingResponsibleDeptCode, RepairResponsibleDeptCode,
      ResponsibleDeptCode, OldDeptCode, NewDeptCode, CurrentApproveForDeptCode,
      SuggestedApproveForDeptCode) - no hard-coded table list.
  2. Maps known legacy text codes to the HRM numeric code:
         FIN -> 14, HR -> 13, PROD -> 12, QA -> 10, IT -> 57
     (add more rows to #LegacyMap below if your data has other text codes).
  3. Fails closed (THROW, full rollback) if any non-numeric value remains.
  4. Drops dependent FKs / indexes / constraints / persisted ScopeKey, converts
     the columns to INT (nullability preserved), then recreates everything.
  5. Upserts the standard HRM department master (BPMa 1..56 + 57 IT) into
     dbo.F03Departments BEFORE foreign keys are recreated.

BPMa 57 (IT, holds SuperAdmin/management users) must also exist in
HRM.dbo.tblBoPhan - see 72_HrmDepartmentITSeed.sql (runs on the HRM database).
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.F03Departments', N'U') IS NULL
    THROW 51400, N'dbo.F03Departments must exist before 71_DepartmentCodeInt.sql.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ------------------------------------------------------------------
       0. Reference data
       ------------------------------------------------------------------ */
    CREATE TABLE #LegacyMap
    (
        LegacyCode  nvarchar(20) NOT NULL PRIMARY KEY,
        HrmDeptCode int          NOT NULL
    );
    INSERT #LegacyMap (LegacyCode, HrmDeptCode)
    VALUES (N'FIN', 14), (N'HR', 13), (N'PROD', 12), (N'QA', 10), (N'IT', 57);

    /* Standard HRM department master (HRM.dbo.tblBoPhan). Names are kept
       verbatim from HRM, including its original spelling. BPMaCha = 0 for all
       rows, so ParentDeptCode stays NULL. 57 = IT (new, management/system). */
    CREATE TABLE #HrmDept
    (
        DeptCode     int           NOT NULL PRIMARY KEY,
        DeptName     nvarchar(100) NOT NULL,
        DisplayPriority int        NULL,
        ShowInReport bit           NOT NULL
    );
    INSERT #HrmDept (DeptCode, DeptName, DisplayPriority, ShowInReport)
    VALUES
    (1,  N'Maintenance',          0, 1), (2,  N'Disk Clutch',          0, 1),
    (4,  N'Diecast135',           0, 1), (5,  N'Diecast',              0, 1),
    (6,  N'Press',                0, 1), (7,  N'CNC',                  0, 1),
    (8,  N'Projection',           0, 1), (9,  N'EBW',                  0, 1),
    (10, N'Quality Control',      0, 1), (11, N'Shoe Clutch',          0, 1),
    (12, N'Production Control',  0, 1), (13, N'General Affairs',      0, 1),
    (14, N'Finance& Account',     0, 1), (15, N'Parts Control',        0, 1),
    (19, N'LV',                   0, 1), (20, N'OUT',                  0, 1),
    (21, N'PL',                   0, 1), (25, N'Pulley',               0, 1),
    (26, N'Hàn Tig',              0, 1), (27, N'Bosscam',              0, 1),
    (28, N'GSN',                  0, 1), (29, N'Tenryu',               0, 1),
    (30, N'Inner',                0, 1), (31, N'4W- CNC',              0, 1),
    (32, N'4W- Diecast',          0, 1), (33, N'4W- Quality Control',  0, 1),
    (34, N'CL-Assy',              0, 1), (35, N'4W- Maintenance',      0, 1),
    (36, N'4W- Production Control', 0, 1), (37, N'4W- Parts Control',  0, 1),
    (43, N'Nhà sạch',             0, 1), (44, N'4W- Diecast L',        0, 1),
    (45, N'Face Assy',            0, 1), (46, N'Newmodel',             0, 1),
    (49, N'4W- Diecast K',        0, 1), (50, N'Automation',           0, 1),
    (51, N'K1B',                  0, 1), (52, N'4W-CNC',               0, 1),
    (53, N'4W-Diecast K',         0, 1), (54, N'HCM General Affairs',  0, 1),
    (55, N'Parst Control',        0, 1), (56, N'4W-Quality Control',   0, 1),
    (57, N'IT',                   0, 1);

    /* Code 57 is reserved for IT. Never silently take over another unit. */
    IF EXISTS
    (
        SELECT 1
        FROM dbo.F03Departments
        WHERE TRY_CONVERT(int, DeptCode) = 57
          AND LTRIM(RTRIM(ISNULL(DeptName, N''))) NOT IN (N'IT', N'Information Technology')
    )
        THROW 51404, N'Department code 57 is already used by a non-IT department.', 1;

    /* ------------------------------------------------------------------
       1. Discover string-typed department-code columns
       ------------------------------------------------------------------ */
    CREATE TABLE #Cols
    (
        ObjectId   int     NOT NULL,
        ColumnId   int     NOT NULL,
        SchemaName sysname NOT NULL,
        TableName  sysname NOT NULL,
        ColumnName sysname NOT NULL,
        IsNullable bit     NOT NULL,
        PRIMARY KEY (ObjectId, ColumnId)
    );

    INSERT #Cols (ObjectId, ColumnId, SchemaName, TableName, ColumnName, IsNullable)
    SELECT t.object_id, c.column_id, s.name, t.name, c.name, c.is_nullable
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.columns c ON c.object_id = t.object_id
    JOIN sys.types  ty ON ty.user_type_id = c.user_type_id
    WHERE t.is_ms_shipped = 0
      AND s.name = N'dbo'
      AND c.is_computed = 0
      AND ty.name IN (N'char', N'varchar', N'nchar', N'nvarchar')
      AND c.name IN (N'DeptCode', N'ParentDeptCode', N'SubDepartmentCode',
                     N'ApproverDeptCode', N'ApproveForDeptCode',
                     N'OperatingResponsibleDeptCode', N'RepairResponsibleDeptCode',
                     N'ResponsibleDeptCode', N'OldDeptCode', N'NewDeptCode',
                     N'CurrentApproveForDeptCode', N'SuggestedApproveForDeptCode');

    DECLARE @ColCount int = (SELECT COUNT(*) FROM #Cols);

    CREATE TABLE #Ops
    (
        Id    int IDENTITY(1,1) PRIMARY KEY,
        Phase tinyint NOT NULL,
        Seq   int     NOT NULL,
        Stmt  nvarchar(max) NOT NULL
    );

    IF @ColCount > 0
    BEGIN
        PRINT CONCAT(N'DepartmentCodeInt: converting ', @ColCount, N' string column(s) to INT.');

        /* -------- 2. Normalize + validate data (still text) -------- */
        DECLARE @obj int, @cid int, @sch sysname, @tbl sysname, @col sysname, @nullable bit;
        DECLARE @fq nvarchar(400), @qc nvarchar(300), @sql nvarchar(max), @n int;
        DECLARE @Bad nvarchar(max) = N'';

        DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT ObjectId, ColumnId, SchemaName, TableName, ColumnName, IsNullable FROM #Cols;
        OPEN cur;
        FETCH NEXT FROM cur INTO @obj, @cid, @sch, @tbl, @col, @nullable;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @fq = QUOTENAME(@sch) + N'.' + QUOTENAME(@tbl);
            SET @qc = QUOTENAME(@col);

            /* legacy text code -> HRM numeric code */
            SET @sql = N'UPDATE T SET {C} = CONVERT(nvarchar(20), M.HrmDeptCode)
                         FROM {T} AS T
                         JOIN #LegacyMap AS M
                           ON M.LegacyCode COLLATE DATABASE_DEFAULT
                            = UPPER(LTRIM(RTRIM(T.{C}))) COLLATE DATABASE_DEFAULT;';
            SET @sql = REPLACE(REPLACE(@sql, N'{T}', @fq), N'{C}', @qc);
            EXEC sys.sp_executesql @sql;

            /* blank -> NULL for nullable columns (blank must never become 0) */
            IF @nullable = 1
            BEGIN
                SET @sql = N'UPDATE {T} SET {C} = NULL WHERE {C} IS NOT NULL AND LTRIM(RTRIM({C})) = N'''';';
                SET @sql = REPLACE(REPLACE(@sql, N'{T}', @fq), N'{C}', @qc);
                EXEC sys.sp_executesql @sql;
            END;

            /* anything still non-numeric (or blank in a NOT NULL column) */
            SET @sql = N'SELECT @n = COUNT(*) FROM {T}
                         WHERE {C} IS NOT NULL
                           AND (LTRIM(RTRIM({C})) = N'''' OR TRY_CONVERT(int, {C}) IS NULL);';
            SET @sql = REPLACE(REPLACE(@sql, N'{T}', @fq), N'{C}', @qc);
            EXEC sys.sp_executesql @sql, N'@n int OUTPUT', @n = @n OUTPUT;

            IF @n > 0
            BEGIN
                DECLARE @Sample nvarchar(400) = N'';
                SET @sql = N'SELECT @s = STUFF((SELECT DISTINCT TOP (5) N'', '' + CONVERT(nvarchar(50), {C})
                                                 FROM {T}
                                                 WHERE {C} IS NOT NULL
                                                   AND (LTRIM(RTRIM({C})) = N'''' OR TRY_CONVERT(int, {C}) IS NULL)
                                                 FOR XML PATH('''')), 1, 2, N'''');';
                SET @sql = REPLACE(REPLACE(@sql, N'{T}', @fq), N'{C}', @qc);
                EXEC sys.sp_executesql @sql, N'@s nvarchar(400) OUTPUT', @s = @Sample OUTPUT;
                SET @Bad += @fq + N'.' + @qc + N' [' + CONVERT(nvarchar(20), @n) + N' row(s): ' + ISNULL(@Sample, N'') + N']; ';
            END;

            FETCH NEXT FROM cur INTO @obj, @cid, @sch, @tbl, @col, @nullable;
        END;
        CLOSE cur;
        DEALLOCATE cur;

        IF @Bad <> N''
        BEGIN
            DECLARE @Msg nvarchar(2048) =
                LEFT(N'Cannot convert department codes to INT. Unmapped values remain (add them to #LegacyMap or fix the data): ' + @Bad, 2040);
            THROW 51401, @Msg, 1;
        END;

        /* Mapping can collapse two legacy rows onto one HRM code. */
        IF EXISTS (SELECT 1 FROM #Cols WHERE ObjectId = OBJECT_ID(N'dbo.F03Departments') AND ColumnName = N'DeptCode')
           AND EXISTS
           (
               SELECT TRY_CONVERT(int, DeptCode)
               FROM dbo.F03Departments
               GROUP BY TRY_CONVERT(int, DeptCode)
               HAVING COUNT(*) > 1
           )
            THROW 51402, N'dbo.F03Departments would contain duplicate DeptCode after conversion; merge the duplicate rows first.', 1;

        /* -------- 3. Pre-checks for dependents we cannot rebuild safely -------- */
        CREATE TABLE #Computed
        (
            ObjectId int NOT NULL, ColumnId int NOT NULL, SchemaName sysname NOT NULL,
            TableName sysname NOT NULL, ColumnName sysname NOT NULL, PRIMARY KEY (ObjectId, ColumnId)
        );
        INSERT #Computed (ObjectId, ColumnId, SchemaName, TableName, ColumnName)
        SELECT DISTINCT cc.object_id, cc.column_id, k.SchemaName, k.TableName, cc.name
        FROM sys.computed_columns cc
        JOIN #Cols k ON k.ObjectId = cc.object_id
        WHERE CHARINDEX(QUOTENAME(k.ColumnName), cc.definition) > 0;

        IF EXISTS (SELECT 1 FROM #Computed WHERE NOT (TableName = N'F03ManagedScopes' AND ColumnName = N'ScopeKey'))
        BEGIN
            SET @Bad = N'';
            SELECT @Bad += QUOTENAME(SchemaName) + N'.' + QUOTENAME(TableName) + N'.' + QUOTENAME(ColumnName) + N'; '
            FROM #Computed WHERE NOT (TableName = N'F03ManagedScopes' AND ColumnName = N'ScopeKey');
            SET @Msg = LEFT(N'Computed column(s) depend on a department-code column; rebuild them manually: ' + @Bad, 2040);
            THROW 51403, @Msg, 1;
        END;

        IF EXISTS
        (
            SELECT 1 FROM sys.check_constraints ck
            JOIN #Cols k ON k.ObjectId = ck.parent_object_id
            WHERE ck.parent_column_id = k.ColumnId
               OR CHARINDEX(QUOTENAME(k.ColumnName), ck.definition) > 0
        )
        BEGIN
            SET @Bad = N'';
            SELECT @Bad += ck.name + N'; '
            FROM sys.check_constraints ck
            JOIN #Cols k ON k.ObjectId = ck.parent_object_id
            WHERE ck.parent_column_id = k.ColumnId OR CHARINDEX(QUOTENAME(k.ColumnName), ck.definition) > 0;
            SET @Msg = LEFT(N'CHECK constraint(s) reference a department-code column; review/drop them first: ' + @Bad, 2040);
            THROW 51405, @Msg, 1;
        END;

        IF EXISTS
        (
            SELECT 1
            FROM sys.sql_expression_dependencies d
            JOIN #Cols k ON k.ObjectId = d.referenced_id AND k.ColumnId = d.referenced_minor_id
            WHERE d.is_schema_bound_reference = 1
        )
            THROW 51406, N'Schema-bound view/function depends on a department-code column; drop it before running this script.', 1;

        CREATE TABLE #Affected (ObjectId int NOT NULL, ColumnId int NOT NULL, PRIMARY KEY (ObjectId, ColumnId));
        INSERT #Affected SELECT ObjectId, ColumnId FROM #Cols;
        INSERT #Affected SELECT ObjectId, ColumnId FROM #Computed;

        IF EXISTS
        (
            SELECT 1
            FROM sys.indexes i
            WHERE i.type NOT IN (0, 1, 2)
              AND EXISTS (SELECT 1 FROM sys.index_columns ic
                          JOIN #Affected a ON a.ObjectId = ic.object_id AND a.ColumnId = ic.column_id
                          WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id)
        )
            THROW 51407, N'A columnstore/XML/spatial index depends on a department-code column; drop it before running this script.', 1;

        /* -------- 4. Build the operation plan -------- */

        /* Phase 1 (drop) / 8 (create): foreign keys touching the columns */
        CREATE TABLE #Fk (FkId int PRIMARY KEY);
        INSERT #Fk
        SELECT DISTINCT fk.object_id
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = fk.object_id
        JOIN #Cols k
          ON (k.ObjectId = fc.parent_object_id     AND k.ColumnId = fc.parent_column_id)
          OR (k.ObjectId = fc.referenced_object_id AND k.ColumnId = fc.referenced_column_id);

        INSERT #Ops (Phase, Seq, Stmt)
        SELECT 1, ROW_NUMBER() OVER (ORDER BY fk.name),
               N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
             + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
        FROM sys.foreign_keys fk JOIN #Fk f ON f.FkId = fk.object_id;

        INSERT #Ops (Phase, Seq, Stmt)
        SELECT 8, ROW_NUMBER() OVER (ORDER BY fk.name),
               N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
             + CASE WHEN fk.is_not_trusted = 1 THEN N' WITH NOCHECK' ELSE N' WITH CHECK' END
             + N' ADD CONSTRAINT ' + QUOTENAME(fk.name) + N' FOREIGN KEY ('
             + STUFF((SELECT N',' + QUOTENAME(pc.name)
                      FROM sys.foreign_key_columns fc
                      JOIN sys.columns pc ON pc.object_id = fc.parent_object_id AND pc.column_id = fc.parent_column_id
                      WHERE fc.constraint_object_id = fk.object_id
                      ORDER BY fc.constraint_column_id
                      FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, N'')
             + N') REFERENCES ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.referenced_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.referenced_object_id)) + N' ('
             + STUFF((SELECT N',' + QUOTENAME(rc.name)
                      FROM sys.foreign_key_columns fc
                      JOIN sys.columns rc ON rc.object_id = fc.referenced_object_id AND rc.column_id = fc.referenced_column_id
                      WHERE fc.constraint_object_id = fk.object_id
                      ORDER BY fc.constraint_column_id
                      FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, N'')
             + N')'
             + CASE fk.delete_referential_action WHEN 1 THEN N' ON DELETE CASCADE' WHEN 2 THEN N' ON DELETE SET NULL' WHEN 3 THEN N' ON DELETE SET DEFAULT' ELSE N'' END
             + CASE fk.update_referential_action WHEN 1 THEN N' ON UPDATE CASCADE' WHEN 2 THEN N' ON UPDATE SET NULL' WHEN 3 THEN N' ON UPDATE SET DEFAULT' ELSE N'' END
             + N';'
             + CASE WHEN fk.is_disabled = 1
                    THEN N' ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
                       + N' NOCHECK CONSTRAINT ' + QUOTENAME(fk.name) + N';'
                    ELSE N'' END
        FROM sys.foreign_keys fk JOIN #Fk f ON f.FkId = fk.object_id;

        /* Phase 2 (drop) / 7 (create): indexes, PK and UNIQUE constraints */
        CREATE TABLE #Ix (ObjectId int NOT NULL, IndexId int NOT NULL, PRIMARY KEY (ObjectId, IndexId));
        INSERT #Ix
        SELECT DISTINCT i.object_id, i.index_id
        FROM sys.indexes i
        JOIN sys.tables t ON t.object_id = i.object_id
        WHERE i.type IN (1, 2) AND i.is_hypothetical = 0
          AND (
                EXISTS (SELECT 1 FROM sys.index_columns ic
                        JOIN #Affected a ON a.ObjectId = ic.object_id AND a.ColumnId = ic.column_id
                        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id)
             OR (i.filter_definition IS NOT NULL
                 AND EXISTS (SELECT 1 FROM #Affected a
                             JOIN sys.columns c ON c.object_id = a.ObjectId AND c.column_id = a.ColumnId
                             WHERE a.ObjectId = i.object_id
                               AND CHARINDEX(QUOTENAME(c.name), i.filter_definition) > 0))
              );

        INSERT #Ops (Phase, Seq, Stmt)
        SELECT 2, ROW_NUMBER() OVER (ORDER BY i.type DESC, i.name),
               CASE WHEN i.is_primary_key = 1 OR i.is_unique_constraint = 1
                    THEN N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id)) + N'.' + QUOTENAME(OBJECT_NAME(i.object_id))
                       + N' DROP CONSTRAINT ' + QUOTENAME(i.name) + N';'
                    ELSE N'DROP INDEX ' + QUOTENAME(i.name) + N' ON '
                       + QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id)) + N'.' + QUOTENAME(OBJECT_NAME(i.object_id)) + N';' END
        FROM sys.indexes i JOIN #Ix x ON x.ObjectId = i.object_id AND x.IndexId = i.index_id;

        INSERT #Ops (Phase, Seq, Stmt)
        SELECT 7, ROW_NUMBER() OVER (ORDER BY i.type ASC, i.name),
               CASE
                 WHEN i.is_primary_key = 1 OR i.is_unique_constraint = 1 THEN
                      N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id)) + N'.' + QUOTENAME(OBJECT_NAME(i.object_id))
                    + N' ADD CONSTRAINT ' + QUOTENAME(i.name)
                    + CASE WHEN i.is_primary_key = 1 THEN N' PRIMARY KEY ' ELSE N' UNIQUE ' END
                    + CASE WHEN i.type = 1 THEN N'CLUSTERED' ELSE N'NONCLUSTERED' END + N' ('
                 ELSE N'CREATE ' + CASE WHEN i.is_unique = 1 THEN N'UNIQUE ' ELSE N'' END
                    + CASE WHEN i.type = 1 THEN N'CLUSTERED' ELSE N'NONCLUSTERED' END
                    + N' INDEX ' + QUOTENAME(i.name) + N' ON '
                    + QUOTENAME(OBJECT_SCHEMA_NAME(i.object_id)) + N'.' + QUOTENAME(OBJECT_NAME(i.object_id)) + N' ('
               END
             + STUFF((SELECT N',' + QUOTENAME(c.name) + CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N' ASC' END
                      FROM sys.index_columns ic
                      JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                      ORDER BY ic.key_ordinal
                      FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, N'')
             + N')'
             + ISNULL(N' INCLUDE (' + NULLIF(STUFF((SELECT N',' + QUOTENAME(c.name)
                      FROM sys.index_columns ic
                      JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
                      ORDER BY ic.index_column_id
                      FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, N''), N'') + N')', N'')
             + ISNULL(N' WHERE ' + i.filter_definition, N'')
             + N';'
        FROM sys.indexes i JOIN #Ix x ON x.ObjectId = i.object_id AND x.IndexId = i.index_id;

        /* Phase 3 (drop) / 9 (create numeric-only): default constraints */
        INSERT #Ops (Phase, Seq, Stmt)
        SELECT 3, ROW_NUMBER() OVER (ORDER BY dc.name),
               N'ALTER TABLE ' + QUOTENAME(k.SchemaName) + N'.' + QUOTENAME(k.TableName) + N' DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
        FROM sys.default_constraints dc
        JOIN #Cols k ON k.ObjectId = dc.parent_object_id AND k.ColumnId = dc.parent_column_id;

        INSERT #Ops (Phase, Seq, Stmt)
        SELECT 9, ROW_NUMBER() OVER (ORDER BY dc.name),
               N'ALTER TABLE ' + QUOTENAME(k.SchemaName) + N'.' + QUOTENAME(k.TableName)
             + N' ADD CONSTRAINT ' + QUOTENAME(dc.name) + N' DEFAULT (' + x.Cleaned + N') FOR ' + QUOTENAME(k.ColumnName) + N';'
        FROM sys.default_constraints dc
        JOIN #Cols k ON k.ObjectId = dc.parent_object_id AND k.ColumnId = dc.parent_column_id
        CROSS APPLY (SELECT LTRIM(RTRIM(REPLACE(REPLACE(REPLACE(REPLACE(dc.definition, N'(', N''), N')', N''), N'N''', N''), N'''', N''))) AS Cleaned) x
        WHERE x.Cleaned <> N'' AND TRY_CONVERT(int, x.Cleaned) IS NOT NULL;

        /* Phase 4 (drop) / 6 (add): persisted ScopeKey on F03ManagedScopes */
        INSERT #Ops (Phase, Seq, Stmt)
        SELECT 4, 1, N'ALTER TABLE ' + QUOTENAME(SchemaName) + N'.' + QUOTENAME(TableName) + N' DROP COLUMN ' + QUOTENAME(ColumnName) + N';'
        FROM #Computed;

        INSERT #Ops (Phase, Seq, Stmt)
        SELECT 6, 1,
               N'ALTER TABLE dbo.F03ManagedScopes ADD ScopeKey AS ('
             + N'UPPER(LTRIM(RTRIM(ISNULL(EmployeeCode,N''''))))+N''|''+'
             + N'UPPER(LTRIM(RTRIM(ISNULL(NodeType,N''''))))+N''|''+'
             + N'UPPER(LTRIM(RTRIM(ISNULL(NodeCode,N''''))))+N''|''+'
             + N'UPPER(LTRIM(RTRIM(ISNULL(FactoryCode,N''''))))+N''|''+'
             + N'ISNULL(CONVERT(nvarchar(20),DeptCode),N'''')+N''|''+'
             + N'ISNULL(CONVERT(nvarchar(20),SubDepartmentCode),N'''')) PERSISTED;'
        FROM #Computed;

        /* Phase 5: the column conversion itself */
        INSERT #Ops (Phase, Seq, Stmt)
        SELECT 5, ROW_NUMBER() OVER (ORDER BY SchemaName, TableName, ColumnName),
               N'ALTER TABLE ' + QUOTENAME(SchemaName) + N'.' + QUOTENAME(TableName)
             + N' ALTER COLUMN ' + QUOTENAME(ColumnName) + N' int ' + CASE WHEN IsNullable = 1 THEN N'NULL' ELSE N'NOT NULL' END + N';'
        FROM #Cols;

        /* -------- 5. Execute phases 1..7 -------- */
        DECLARE @Stmt nvarchar(max);
        DECLARE ops1 CURSOR LOCAL FAST_FORWARD FOR
            SELECT Stmt FROM #Ops WHERE Phase BETWEEN 1 AND 7 ORDER BY Phase, Seq;
        OPEN ops1;
        FETCH NEXT FROM ops1 INTO @Stmt;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            EXEC sys.sp_executesql @Stmt;
            FETCH NEXT FROM ops1 INTO @Stmt;
        END;
        CLOSE ops1;
        DEALLOCATE ops1;
    END;

    /* ------------------------------------------------------------------
       6. Standard HRM department master (must exist before FKs come back)
       ------------------------------------------------------------------ */
    IF EXISTS
    (
        SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.F03Departments') AND c.name = N'DeptCode' AND ty.name <> N'int'
    )
        THROW 51408, N'dbo.F03Departments.DeptCode is still not INT after conversion.', 1;

    UPDATE d
    SET d.DeptName           = h.DeptName,
        d.DisplayPriority    = h.DisplayPriority,
        d.ShowInReport       = h.ShowInReport,
        d.ModifiedBy         = 0,
        d.ModifiedAt         = GETDATE(),
        d.LastModifiedSource = N'HRM_STANDARD'
    FROM dbo.F03Departments d
    JOIN #HrmDept h ON h.DeptCode = d.DeptCode
    WHERE d.DeptName <> h.DeptName
       OR ISNULL(d.ShowInReport, 1) <> h.ShowInReport;

    INSERT dbo.F03Departments
        (IsActive, CreatedBy, LastModifiedSource, CreatedAt,
         DeptCode, DeptName, ParentDeptCode, DisplayPriority, ShowInReport)
    SELECT 1, 0, N'HRM_STANDARD', GETDATE(),
           h.DeptCode, h.DeptName, NULL, h.DisplayPriority, h.ShowInReport
    FROM #HrmDept h
    WHERE NOT EXISTS (SELECT 1 FROM dbo.F03Departments d WHERE d.DeptCode = h.DeptCode);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.F03Departments') AND name = N'UX_F03Departments_DeptCode')
        CREATE UNIQUE INDEX UX_F03Departments_DeptCode ON dbo.F03Departments(DeptCode);

    /* ------------------------------------------------------------------
       7. Recreate foreign keys / defaults (phases 8..9)
       ------------------------------------------------------------------ */
    IF OBJECT_ID(N'tempdb..#Ops') IS NOT NULL
    BEGIN
        DECLARE @Stmt2 nvarchar(max);
        DECLARE ops2 CURSOR LOCAL FAST_FORWARD FOR
            SELECT Stmt FROM #Ops WHERE Phase >= 8 ORDER BY Phase, Seq;
        OPEN ops2;
        FETCH NEXT FROM ops2 INTO @Stmt2;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            EXEC sys.sp_executesql @Stmt2;
            FETCH NEXT FROM ops2 INTO @Stmt2;
        END;
        CLOSE ops2;
        DEALLOCATE ops2;
    END;

    /* Canonical FKs must exist even on installs that never had them. */
    IF OBJECT_ID(N'dbo.F03Employees', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_F03Employees_F03Departments')
        ALTER TABLE dbo.F03Employees
            ADD CONSTRAINT FK_F03Employees_F03Departments
            FOREIGN KEY (DeptCode) REFERENCES dbo.F03Departments(DeptCode);

    IF OBJECT_ID(N'dbo.F03ApprovalPolicies', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.F03ApprovalPolicies', N'DeptCode') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_F03ApprovalPolicies_F03Departments')
        ALTER TABLE dbo.F03ApprovalPolicies
            ADD CONSTRAINT FK_F03ApprovalPolicies_F03Departments
            FOREIGN KEY (DeptCode) REFERENCES dbo.F03Departments(DeptCode);

    COMMIT TRANSACTION;
    PRINT N'DepartmentCode is INT everywhere; HRM standard departments (1..56 + 57 IT) are in dbo.F03Departments.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* Refresh view metadata after the type change (best effort, outside the tx). */
SET XACT_ABORT OFF;
DECLARE @ViewName nvarchar(400);
DECLARE vw CURSOR LOCAL FAST_FORWARD FOR
    SELECT QUOTENAME(s.name) + N'.' + QUOTENAME(v.name)
    FROM sys.views v JOIN sys.schemas s ON s.schema_id = v.schema_id
    WHERE v.is_ms_shipped = 0 AND OBJECTPROPERTY(v.object_id, 'IsSchemaBound') = 0;
OPEN vw;
FETCH NEXT FROM vw INTO @ViewName;
WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        EXEC sys.sp_refreshview @ViewName;
    END TRY
    BEGIN CATCH
        PRINT N'sp_refreshview skipped for ' + @ViewName + N': ' + ERROR_MESSAGE();
    END CATCH;
    FETCH NEXT FROM vw INTO @ViewName;
END;
CLOSE vw;
DEALLOCATE vw;
SET XACT_ABORT ON;
