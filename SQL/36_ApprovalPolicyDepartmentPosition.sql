SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/*
    Approval Policy v4
    ------------------
    Canonical scope:
        RequestType (required)
        DeptCode    (required)
        PositionCode (optional requester position refinement)
        ApprovalPositionCode (required approver position)
*/

IF OBJECT_ID(N'dbo.F03ApprovalPolicies', N'U') IS NULL
BEGIN
    THROW 51001, 'F03ApprovalPolicies must exist before applying Approval Policy v4.', 1;
END;
GO

IF COL_LENGTH(N'dbo.F03ApprovalPolicies', N'DeptCode') IS NULL
BEGIN
    ALTER TABLE dbo.F03ApprovalPolicies
        ADD DeptCode int NULL;
END;
GO

IF COL_LENGTH(N'dbo.F03ApprovalPolicies', N'ApprovalPositionCode') IS NULL
BEGIN
    ALTER TABLE dbo.F03ApprovalPolicies
        ADD ApprovalPositionCode nvarchar(20) NULL;
END;
GO

/*
    SQL Server will not ALTER COLUMN while ANY index depends on PositionCode.
    Discover every ordinary index that actually contains PositionCode instead
    of relying only on historical index names.

    Primary keys / unique constraints are intentionally preserved. If one
    unexpectedly depends on PositionCode, fail explicitly and safely.
*/
DECLARE @IndexName sysname;
DECLARE @Sql nvarchar(max);

DECLARE index_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT DISTINCT i.name
FROM sys.indexes AS i
JOIN sys.index_columns AS ic
  ON ic.object_id = i.object_id
 AND ic.index_id = i.index_id
JOIN sys.columns AS c
  ON c.object_id = ic.object_id
 AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID(N'dbo.F03ApprovalPolicies')
  AND c.name = N'PositionCode'
  AND i.is_primary_key = 0
  AND i.is_unique_constraint = 0;

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO @IndexName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'DROP INDEX ' + QUOTENAME(@IndexName)
             + N' ON dbo.F03ApprovalPolicies;';
    EXEC sys.sp_executesql @Sql;
    FETCH NEXT FROM index_cursor INTO @IndexName;
END;

CLOSE index_cursor;
DEALLOCATE index_cursor;

IF EXISTS
(
    SELECT 1
    FROM sys.indexes AS i
    JOIN sys.index_columns AS ic
      ON ic.object_id = i.object_id
     AND ic.index_id = i.index_id
    JOIN sys.columns AS c
      ON c.object_id = ic.object_id
     AND c.column_id = ic.column_id
    WHERE i.object_id = OBJECT_ID(N'dbo.F03ApprovalPolicies')
      AND c.name = N'PositionCode'
      AND (i.is_primary_key = 1 OR i.is_unique_constraint = 1)
)
BEGIN
    THROW 51002, 'PositionCode is still referenced by a primary/unique constraint; migration stopped safely.', 1;
END;
GO

/* PositionCode is the optional requester-position refinement in v4. */
IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.F03ApprovalPolicies')
      AND name = N'PositionCode'
      AND
      (
          system_type_id <> TYPE_ID(N'int')
          OR is_nullable = 0
      )
)
BEGIN
    ALTER TABLE dbo.F03ApprovalPolicies
        ALTER COLUMN PositionCode nvarchar(20) NULL;
END;
GO

/* Retire legacy policies that cannot be mapped safely to the v4 scope. */
DELETE p
FROM dbo.F03ApprovalPolicies AS p
WHERE p.DeptCode IS NULL
   OR LTRIM(RTRIM(p.DeptCode)) = N''
   OR p.ApprovalPositionCode IS NULL
   OR LTRIM(RTRIM(p.ApprovalPositionCode)) = N'';
GO

/*
    Do not use IF/ELSE here. Separate IF NOT EXISTS statements are deliberately
    used because this migration is executed through SQLCMD/Deploy.ps1 against
    databases with different historical FK states.
*/
IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_F03ApprovalPolicies_F03Departments'
      AND parent_object_id = OBJECT_ID(N'dbo.F03ApprovalPolicies')
)
BEGIN
    ALTER TABLE dbo.F03ApprovalPolicies
        ADD CONSTRAINT FK_F03ApprovalPolicies_F03Departments
        FOREIGN KEY (DeptCode)
        REFERENCES dbo.F03Departments(DeptCode);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_F03ApprovalPolicies_ApprovalPosition'
      AND parent_object_id = OBJECT_ID(N'dbo.F03ApprovalPolicies')
)
BEGIN
    ALTER TABLE dbo.F03ApprovalPolicies
        ADD CONSTRAINT FK_F03ApprovalPolicies_ApprovalPosition
        FOREIGN KEY (ApprovalPositionCode)
        REFERENCES dbo.F03Positions(PositionCode);
END;
GO

ALTER TABLE dbo.F03ApprovalPolicies
    ALTER COLUMN DeptCode int NOT NULL;
GO

ALTER TABLE dbo.F03ApprovalPolicies
    ALTER COLUMN ApprovalPositionCode nvarchar(20) NOT NULL;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_F03ApprovalPolicies_Request_Dept_Position_Level'
      AND object_id = OBJECT_ID(N'dbo.F03ApprovalPolicies')
)
BEGIN
    CREATE UNIQUE INDEX UX_F03ApprovalPolicies_Request_Dept_Position_Level
        ON dbo.F03ApprovalPolicies
        (
            RequestType,
            DeptCode,
            PositionCode,
            Level
        );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_F03ApprovalPolicies_Route'
      AND object_id = OBJECT_ID(N'dbo.F03ApprovalPolicies')
)
BEGIN
    CREATE INDEX IX_F03ApprovalPolicies_Route
        ON dbo.F03ApprovalPolicies
        (
            RequestType,
            DeptCode,
            PositionCode,
            IsActive,
            Sequence,
            Level
        );
END;
GO
