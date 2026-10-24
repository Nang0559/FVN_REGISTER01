/*
  FVN_REGISTER AI Platform - Phase 1-3 lifecycle/representation migration
  Target: dedicated AI database only. Apply after 007_UniversalDocumentLifecycle.sql.
  Safe to execute repeatedly.
*/
IF COL_LENGTH(N'ai.AiDocuments', N'ContentPurgedAtUtc') IS NULL
    ALTER TABLE ai.AiDocuments ADD ContentPurgedAtUtc DATETIMEOFFSET(7) NULL;
GO

IF COL_LENGTH(N'ai.AiExtractions', N'RepresentationJson') IS NULL
    ALTER TABLE ai.AiExtractions ADD RepresentationJson NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_AiExtractions_RepresentationJson' AND parent_object_id=OBJECT_ID(N'ai.AiExtractions'))
    ALTER TABLE ai.AiExtractions ADD CONSTRAINT CK_AiExtractions_RepresentationJson
        CHECK (RepresentationJson IS NULL OR ISJSON(RepresentationJson)=1);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AiDocuments_Retention' AND object_id=OBJECT_ID(N'ai.AiDocuments'))
    CREATE INDEX IX_AiDocuments_Retention
        ON ai.AiDocuments(Status, RetentionUntilUtc, ContentPurgedAtUtc)
        INCLUDE (TenantCode, StorageKey);
GO
