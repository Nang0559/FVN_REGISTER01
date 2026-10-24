/*
 FVN_REGISTER AI Platform - migration 012.
 Moves knowledge chunk ordinal uniqueness out of historical migrations so
 existing installations converge on the same schema without rewriting history.
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'ai.AiKnowledgeChunks', N'U') IS NULL
    THROW 51012, 'Required table ai.AiKnowledgeChunks is missing. Apply migrations 001-011 first.', 1;
GO

IF EXISTS
(
    SELECT 1 FROM ai.AiKnowledgeChunks
    GROUP BY TenantCode, DocumentId, ChunkOrdinal
    HAVING COUNT_BIG(*) > 1
)
    THROW 51013, 'Duplicate knowledge chunk ordinals exist. Reconcile duplicates before migration 012.', 1;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id=OBJECT_ID(N'ai.AiKnowledgeChunks')
      AND name=N'UX_AiKnowledgeChunks_Tenant_Document_Ordinal'
)
    CREATE UNIQUE INDEX UX_AiKnowledgeChunks_Tenant_Document_Ordinal
        ON ai.AiKnowledgeChunks(TenantCode, DocumentId, ChunkOrdinal);
GO

IF OBJECT_ID(N'ai.AiSchemaMigrations', N'U') IS NULL
    THROW 51014, 'Migration ledger ai.AiSchemaMigrations is missing. Apply migration 010 first.', 1;
GO

IF COL_LENGTH(N'ai.AiSchemaMigrations', N'MigrationFileName') IS NULL
    ALTER TABLE ai.AiSchemaMigrations ADD MigrationFileName NVARCHAR(260) NULL;
GO
IF COL_LENGTH(N'ai.AiSchemaMigrations', N'Sha256Checksum') IS NULL
    ALTER TABLE ai.AiSchemaMigrations ADD Sha256Checksum CHAR(64) NULL;
GO

UPDATE ai.AiSchemaMigrations
   SET MigrationFileName = CASE MigrationId
       WHEN N'010_Extraction_Deduplication_And_MigrationLedger' THEN N'010_Extraction_Deduplication_And_MigrationLedger.sql'
       WHEN N'011_Persistent_Request_Replay_Protection' THEN N'011_Persistent_Request_Replay_Protection.sql'
       ELSE MigrationFileName END
 WHERE MigrationFileName IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM ai.AiSchemaMigrations WHERE MigrationId=N'012_KnowledgeChunkConstraint_And_ChecksumLedger')
    INSERT INTO ai.AiSchemaMigrations(MigrationId, MigrationFileName, Sha256Checksum)
    VALUES(N'012_KnowledgeChunkConstraint_And_ChecksumLedger', N'012_KnowledgeChunkConstraint_And_ChecksumLedger.sql', NULL);
GO
