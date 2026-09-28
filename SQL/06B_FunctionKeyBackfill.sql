USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 50073, 'Missing dbo.F03Functions.', 1;

IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NULL
    THROW 50074, 'Missing dbo.F03Functions.FunctionKey. Run 06A_FunctionKeyCompatibility.sql first.', 1;

UPDATE f
SET f.FunctionKey = CONCAT(N'FN_', CONVERT(nvarchar(20), f.FunctionCode))
FROM dbo.F03Functions f
WHERE NULLIF(LTRIM(RTRIM(f.FunctionKey)), N'') IS NULL
  AND f.FunctionCode IS NOT NULL;

PRINT N'06B_FunctionKeyBackfill: seeded F03Functions.FunctionKey values synchronized from FunctionCode.';
GO
