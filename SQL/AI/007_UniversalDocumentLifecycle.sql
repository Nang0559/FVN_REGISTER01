/*
 FVN_REGISTER AI Platform - Universal document lifecycle metadata.
 Target: dedicated FVN_AI database only.
 Safe to execute repeatedly.
*/

IF SCHEMA_ID(N'ai') IS NULL EXEC(N'CREATE SCHEMA ai');
GO

IF COL_LENGTH(N'ai.AiDocuments',N'SourceType') IS NULL
    ALTER TABLE ai.AiDocuments ADD SourceType NVARCHAR(32) NOT NULL CONSTRAINT DF_AiDocuments_SourceType DEFAULT N'Upload';
GO

IF COL_LENGTH(N'ai.AiDocuments',N'SourceReference') IS NULL
    ALTER TABLE ai.AiDocuments ADD SourceReference NVARCHAR(512) NULL;
GO

IF COL_LENGTH(N'ai.AiDocuments',N'RetentionUntilUtc') IS NULL
    ALTER TABLE ai.AiDocuments ADD RetentionUntilUtc DATETIMEOFFSET(7) NULL;
GO

IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'ai.AiDocuments') AND name=N'CK_AiDocuments_SourceType')
    ALTER TABLE ai.AiDocuments ADD CONSTRAINT CK_AiDocuments_SourceType CHECK(SourceType IN(N'Upload',N'Folder',N'Connector',N'Api',N'Unknown'));
GO

IF COL_LENGTH(N'ai.AiExtractions',N'SchemaCode') IS NULL
    ALTER TABLE ai.AiExtractions ADD SchemaCode NVARCHAR(128) NULL;
GO

IF COL_LENGTH(N'ai.AiExtractions',N'SchemaVersion') IS NULL
    ALTER TABLE ai.AiExtractions ADD SchemaVersion INT NULL;
GO

IF COL_LENGTH(N'ai.AiExtractions',N'ValidationStatus') IS NULL
    ALTER TABLE ai.AiExtractions ADD ValidationStatus NVARCHAR(16) NOT NULL CONSTRAINT DF_AiExtractions_ValidationStatus DEFAULT N'NotRun';
GO

IF COL_LENGTH(N'ai.AiExtractions',N'ValidationSummaryJson') IS NULL
    ALTER TABLE ai.AiExtractions ADD ValidationSummaryJson NVARCHAR(MAX) NULL;
GO

IF COL_LENGTH(N'ai.AiExtractions',N'ProvenanceJson') IS NULL
    ALTER TABLE ai.AiExtractions ADD ProvenanceJson NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'ai.AiExtractions') AND name=N'CK_AiExtractions_ValidationStatus')
    ALTER TABLE ai.AiExtractions ADD CONSTRAINT CK_AiExtractions_ValidationStatus CHECK(ValidationStatus IN(N'NotRun',N'Valid',N'Invalid'));
GO

IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'ai.AiExtractions') AND name=N'CK_AiExtractions_ProvenanceJson')
    ALTER TABLE ai.AiExtractions ADD CONSTRAINT CK_AiExtractions_ProvenanceJson CHECK(ProvenanceJson IS NULL OR ISJSON(ProvenanceJson)=1);
GO

IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'ai.AiExtractions') AND name=N'CK_AiExtractions_ValidationSummaryJson')
    ALTER TABLE ai.AiExtractions ADD CONSTRAINT CK_AiExtractions_ValidationSummaryJson CHECK(ValidationSummaryJson IS NULL OR ISJSON(ValidationSummaryJson)=1);
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiDocuments') AND name=N'IX_AiDocuments_Tenant_Status_Retention')
    CREATE INDEX IX_AiDocuments_Tenant_Status_Retention ON ai.AiDocuments(TenantCode,Status,RetentionUntilUtc);
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiExtractions') AND name=N'IX_AiExtractions_Tenant_Schema_Version')
    CREATE INDEX IX_AiExtractions_Tenant_Schema_Version ON ai.AiExtractions(TenantCode,SchemaCode,SchemaVersion,CreatedAtUtc);
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiExtractions') AND name=N'IX_AiExtractions_Tenant_Document_Created')
    CREATE INDEX IX_AiExtractions_Tenant_Document_Created ON ai.AiExtractions(TenantCode,DocumentId,CreatedAtUtc DESC);
GO
