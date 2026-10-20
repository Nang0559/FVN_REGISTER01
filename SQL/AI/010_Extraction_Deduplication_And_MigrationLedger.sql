/*
 FVN_REGISTER AI Platform - Phase 1-6 hardening migration.
 Apply after 009_Phase4_5_Knowledge_ActiveLearning.sql to the dedicated FVN_AI database.
 Safe to execute repeatedly. Existing duplicate extractions are reported for manual review;
 this migration never deletes extraction/audit data.
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'ai') IS NULL EXEC(N'CREATE SCHEMA ai');
GO

/* Prevent a retry after lease expiry from creating another extraction for the same
   document/schema version. NULL SchemaVersion rows are legacy/unversioned and excluded. */
IF OBJECT_ID(N'ai.AiExtractions', N'U') IS NULL
    THROW 51010, 'Required table ai.AiExtractions is missing. Apply migrations 001-009 first.', 1;
GO

IF EXISTS
(
    SELECT 1
    FROM ai.AiExtractions
    WHERE SchemaVersion IS NOT NULL
    GROUP BY DocumentId, SchemaVersion
    HAVING COUNT_BIG(*) > 1
)
    THROW 51011, 'Duplicate AiExtractions exist for DocumentId/SchemaVersion. Review and reconcile them before applying migration 010.', 1;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'ai.AiExtractions')
      AND name = N'UX_AiExtractions_Document_SchemaVersion'
)
    CREATE UNIQUE INDEX UX_AiExtractions_Document_SchemaVersion
        ON ai.AiExtractions(DocumentId, SchemaVersion)
        WHERE SchemaVersion IS NOT NULL;
GO

/* Lightweight migration ledger. Register 010 only; do not imply that this table
   proves older migrations ran, since it is introduced by this migration. */
IF OBJECT_ID(N'ai.AiSchemaMigrations', N'U') IS NULL
BEGIN
    CREATE TABLE ai.AiSchemaMigrations
    (
        MigrationId NVARCHAR(160) NOT NULL CONSTRAINT PK_AiSchemaMigrations PRIMARY KEY,
        AppliedAtUtc DATETIMEOFFSET(7) NOT NULL
            CONSTRAINT DF_AiSchemaMigrations_AppliedAtUtc DEFAULT(SYSUTCDATETIME())
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM ai.AiSchemaMigrations WHERE MigrationId = N'010_Extraction_Deduplication_And_MigrationLedger')
    INSERT INTO ai.AiSchemaMigrations(MigrationId)
    VALUES(N'010_Extraction_Deduplication_And_MigrationLedger');
GO
