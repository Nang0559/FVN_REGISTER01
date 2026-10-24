/* 022 - Rejected/archived documents must not block re-uploading the same file.
   Migration 006 created UX_AiDocuments_Tenant_Sha256_Module_Schema as an UNFILTERED unique index.
   Migration 014 introduced DedupActive and the filtered UX_AiDocuments_Tenant_Fingerprint_Active, but only
   dropped the older UX_AiDocuments_Tenant_Sha256 index, so the 006 index kept rejecting re-uploads with
   "Cannot insert duplicate key row ... UX_AiDocuments_Tenant_Sha256_Module_Schema".
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH(N'ai.AiDocuments', N'DedupActive') IS NULL
    THROW 51025, 'Migration 014 (DedupActive) must be applied before migration 022.', 1;
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiDocuments') AND name=N'UX_AiDocuments_Tenant_Sha256_Module_Schema')
    DROP INDEX UX_AiDocuments_Tenant_Sha256_Module_Schema ON ai.AiDocuments;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiDocuments') AND name=N'UX_AiDocuments_Tenant_Fingerprint_Active')
    CREATE UNIQUE INDEX UX_AiDocuments_Tenant_Fingerprint_Active
      ON ai.AiDocuments(TenantCode,Sha256,ModuleCode,SchemaCode)
      WHERE DedupActive=1;
GO
