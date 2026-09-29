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

    The selected approval position is resolved against F03Positions.
    Its DefaultApproveLevel supplies Level; RoleName is resolved by the
    application from that level/request type. F03Approvers then supplies
    the actual employee candidates for that position and department.

    Existing v3 rows cannot be safely inferred into a department or an
    approval position. They are therefore retired from the active model.
    Admin must recreate them using the new policy UI.
*/

IF OBJECT_ID(N'dbo.F03ApprovalPolicies', N'U') IS NULL
BEGIN
    THROW 51001, 'F03ApprovalPolicies must exist before applying Approval Policy v4.', 1;
END;
GO

IF COL_LENGTH(N'dbo.F03ApprovalPolicies', N'DeptCode') IS NULL
BEGIN
    ALTER TABLE dbo.F03ApprovalPolicies
        ADD DeptCode nvarchar(20) NULL;
END;
GO

IF COL_LENGTH(N'dbo.F03ApprovalPolicies', N'ApprovalPositionCode') IS NULL
BEGIN
    ALTER TABLE dbo.F03ApprovalPolicies
        ADD ApprovalPositionCode nvarchar(20) NULL;
END;
GO

/*
    IMPORTANT:
    SQL Server will not ALTER COLUMN while ANY index depends on PositionCode.
    Older databases can contain the legacy indexes as well as the canonical
    v4 names. Do not rely only on the expected names: discover every ordinary
    index that actually contains PositionCode and remove it before ALTER.

    Primary keys / unique constraints are intentionally not removed here.
    The approval-policy table is not expected to use PositionCode as its PK;
    if an unexpected PK/constraint depends on it, fail explicitly instead of
    silently changing a key definition.
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

/*
    If a PK/unique constraint unexpectedly depends on PositionCode, do not
    proceed with a partial schema change. The deployment must stop with a
    useful diagnostic rather than SQL Server's generic 5074/4922 message.
*/
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
    THROW 51002, 'PositionCode is still referenced by a primary/unique constraint; migration stopped safely.', 1;
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
          system_type_id <> TYPE_ID(N'nvarchar')
          OR max_length <> 40
          OR is_nullable = 0
      )
)
BEGIN
    ALTER TABLE dbo.F03ApprovalPolicies
        ALTER COLUMN PositionCode nvarchar(20) NULL;
END;
GO

/*
    Existing rows have no reliable department / approver-position information.
    Retire them before the new foreign keys are created.

    Valid v4 rows (both values supplied) are preserved.
    Legacy rows that cannot be mapped safely are deleted from the active
    policy table; they must be recreated through the new policy UI.
*/
DELETE p
FROM dbo.F03ApprovalPolicies AS p
WHERE p.DeptCode IS NULL
   OR LTRIM(RTRIM(p.DeptCode)) = N''
   OR p.ApprovalPositionCode IS NULL
   OR LTRIM(RTRIM(p.ApprovalPositionCode)) = N'';
GO

IF EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_F03ApprovalPolicies_F03Departments'
      AND parent_object_id = OBJECT_ID(N'dbo.F03ApprovalPolicies')
)
BEGIN
    /* Existing FK is retained. */
END
ELSE
BEGIN
    ALTER TABLE dbo.F03ApprovalPolicies
        ADD CONSTRAINT FK_F03ApprovalPolicies_F03Departments
        FOREIGN KEY (DeptCode)
        REFERENCES dbo.F03Departments(DeptCode);
END;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_F03ApprovalPolicies_ApprovalPosition'
      AND parent_object_id = OBJECT_ID(N'dbo.F03ApprovalPolicies')
)
BEGIN
    /* Existing FK is retained. */
END
ELSE
BEGIN
    ALTER TABLE dbo.F03ApprovalPolicies
        ADD CONSTRAINT FK_F03ApprovalPolicies_ApprovalPosition
        FOREIGN KEY (ApprovalPositionCode)
        REFERENCES dbo.F03Positions(PositionCode);
END;
GO

ALTER TABLE dbo.F03ApprovalPolicies
    ALTER COLUMN DeptCode nvarchar(20) NOT NULL;
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
