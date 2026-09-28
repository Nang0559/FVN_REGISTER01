USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.F03Functions',N'U') IS NULL
    THROW 50073, 'Missing dbo.F03Functions.', 1;

IF COL_LENGTH(N'dbo.F03Functions',N'FunctionKey') IS NULL
    THROW 50074, 'Missing dbo.F03Functions.FunctionKey. Run 06A_FunctionKeyCompatibility.sql first.', 1;

/*
   06_Seed.sql intentionally omits FunctionKey. During seed the compatibility
   default supplies N''; now replace every empty value with the deterministic
   authorization key derived from FunctionCode.
*/
UPDATE f
SET f.FunctionKey = CONCAT(N'FN_', CONVERT(nvarchar(20), f.FunctionCode))
FROM dbo.F03Functions f
WHERE NULLIF(LTRIM(RTRIM(f.FunctionKey)), N'') IS NULL
  AND f.FunctionCode IS NOT NULL;

/* FunctionKey must be unique before the index is restored. */
IF EXISTS
(
    SELECT 1
    FROM dbo.F03Functions
    GROUP BY FunctionKey
    HAVING FunctionKey IS NULL
        OR LTRIM(RTRIM(FunctionKey)) = N''
        OR COUNT(*) > 1
)
    THROW 50075, 'F03Functions.FunctionKey contains NULL/empty/duplicate values after backfill.', 1;

/*
   Restore the canonical unique index after seed/backfill. If an equivalent
   unique index already exists under another name, do not create a duplicate.
*/
IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID(N'dbo.F03Functions')
      AND i.name = N'UX_F03Functions_FunctionKey'
)
BEGIN
    CREATE UNIQUE INDEX [UX_F03Functions_FunctionKey]
        ON dbo.F03Functions(FunctionKey);
END;

PRINT N'06B_FunctionKeyBackfill: FunctionKey values synchronized and UX_F03Functions_FunctionKey restored.';
GO
