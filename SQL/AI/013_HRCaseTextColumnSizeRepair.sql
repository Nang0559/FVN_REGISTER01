/*
 FVN_REGISTER AI Platform - migration 013.
 Compatibility repair for databases where the HR case text columns were created
 with a shorter supported length before the invalid NVARCHAR(8000) definition
 was corrected in the clean-install script.
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'ai.AiHRCases', N'U') IS NOT NULL
BEGIN
    IF EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id=OBJECT_ID(N'ai.AiHRCases')
          AND name=N'UserStatement'
          AND max_length <> -1
    )
        ALTER TABLE ai.AiHRCases ALTER COLUMN UserStatement NVARCHAR(MAX) NOT NULL;

    IF EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id=OBJECT_ID(N'ai.AiHRCases')
          AND name=N'InternalHRNote'
          AND max_length <> -1
    )
        ALTER TABLE ai.AiHRCases ALTER COLUMN InternalHRNote NVARCHAR(MAX) NOT NULL;
END;
GO

IF OBJECT_ID(N'ai.AiSchemaMigrations', N'U') IS NOT NULL
   AND COL_LENGTH(N'ai.AiSchemaMigrations', N'MigrationFileName') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM ai.AiSchemaMigrations WHERE MigrationId=N'013_HRCaseTextColumnSizeRepair')
    INSERT INTO ai.AiSchemaMigrations(MigrationId, MigrationFileName, Sha256Checksum)
    VALUES(N'013_HRCaseTextColumnSizeRepair', N'013_HRCaseTextColumnSizeRepair.sql', NULL);
GO
