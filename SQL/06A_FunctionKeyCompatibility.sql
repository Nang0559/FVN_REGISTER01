USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
===============================================================================
F03Functions / FunctionKey compatibility
-------------------------------------------------------------------------------
06_Seed.sql inserts F03Functions without FunctionKey. Existing installations
may already have a NOT NULL FunctionKey plus UX_F03Functions_FunctionKey.

A default of N'' alone is not sufficient when the unique index is active:
multiple seed rows would all receive the same empty value and SQL Server would
raise Msg 2601. Therefore the compatibility migration temporarily removes the
FunctionKey unique index. 06B backfills deterministic FN_<FunctionCode> values
and recreates the unique index after the seed has completed.
===============================================================================
*/

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 50071, 'Missing dbo.F03Functions. Run 03_Tables.sql before 06A_FunctionKeyCompatibility.sql.', 1;

/* New/older installations: add the compatibility column if it is missing. */
IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NULL
BEGIN
    ALTER TABLE dbo.F03Functions
        ADD FunctionKey nvarchar(100) NULL;
END;
GO

/*
   06_Seed.sql does not provide FunctionKey. Keep a compatibility default so
   NOT NULL legacy schemas can accept the insert. The value is deliberately
   temporary and is replaced by 06B using FunctionCode.
*/
IF EXISTS
(
    SELECT 1
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.F03Functions')
      AND c.name = N'FunctionKey'
      AND c.is_nullable = 0
)
AND NOT EXISTS
(
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.F03Functions')
      AND c.name = N'FunctionKey'
)
BEGIN
    ALTER TABLE dbo.F03Functions
        ADD CONSTRAINT DF_F03Functions_FunctionKey
        DEFAULT (N'') FOR FunctionKey;
END;
GO

/*
   Existing rows must be normalized before the unique index is removed/rebuilt.
   FunctionCode is the canonical stable authorization identity.
*/
UPDATE f
SET f.FunctionKey = CONCAT(N'FN_', CONVERT(nvarchar(20), f.FunctionCode))
FROM dbo.F03Functions f
WHERE NULLIF(LTRIM(RTRIM(f.FunctionKey)), N'') IS NULL
  AND f.FunctionCode IS NOT NULL;
GO

/*
   Defer the unique FunctionKey index until after 06_Seed.sql.
   This is the critical fix for Msg 2601: every seed row currently receives
   the temporary default N'', so uniqueness cannot be enforced during seed.
*/
IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.F03Functions')
      AND name = N'UX_F03Functions_FunctionKey'
)
BEGIN
    DROP INDEX [UX_F03Functions_FunctionKey] ON dbo.F03Functions;
END;
GO

IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NULL
    THROW 50072, 'F03Functions.FunctionKey was not created.', 1;

PRINT N'06A_FunctionKeyCompatibility: FunctionKey ready; unique index deferred until 06B.';
GO
