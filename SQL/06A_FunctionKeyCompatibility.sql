USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
===============================================================================
F03Functions / FunctionKey compatibility
-------------------------------------------------------------------------------
06_Seed.sql seeds F03Functions by FunctionCode. Some existing FVN_REGISTER
installations already contain a NOT NULL FunctionKey column from the security
schema. SQL Server therefore rejects the seed when FunctionKey is omitted.

This migration makes the column compatible with the canonical seed without
changing FunctionCode, which remains the stable authorization identifier.
===============================================================================
*/

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 50071, 'Missing dbo.F03Functions. Run 03_Tables.sql before 06A_FunctionKeyCompatibility.sql.', 1;

/* New/older installations: add the missing compatibility column. */
IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NULL
BEGIN
    ALTER TABLE dbo.F03Functions
        ADD FunctionKey nvarchar(100) NULL;
END;
GO

/*
   The seed INSERT intentionally supplies FunctionCode but not FunctionKey.
   Give FunctionKey a deterministic default so both old and new databases
   accept that INSERT. Existing values are preserved.
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
   For rows already present, derive a deterministic key from FunctionCode.
   This is only a compatibility fallback; application authorization continues
   to use FunctionCode as the stable numeric identity.
*/
UPDATE f
SET f.FunctionKey = CONCAT(N'FN_', CONVERT(nvarchar(20), f.FunctionCode))
FROM dbo.F03Functions f
WHERE NULLIF(LTRIM(RTRIM(f.FunctionKey)), N'') IS NULL
  AND f.FunctionCode IS NOT NULL;
GO

/* Verify the compatibility contract before 06_Seed.sql starts. */
IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NULL
    THROW 50072, 'F03Functions.FunctionKey was not created.', 1;

PRINT N'06A_FunctionKeyCompatibility: F03Functions.FunctionKey is ready for 06_Seed.sql.';
GO
