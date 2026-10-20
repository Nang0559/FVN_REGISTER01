/*
 Phase 1-5 platform extension.
 Run after 004_HR_Reconciliation_Cases.sql on the dedicated FVN_AI database.
 Safe to re-run. Does not alter FVN_REGISTER operational tables.
*/
IF SCHEMA_ID(N'ai') IS NULL EXEC(N'CREATE SCHEMA ai');
GO

IF OBJECT_ID(N'ai.AiExtractionSchemas',N'U') IS NULL
BEGIN
 CREATE TABLE ai.AiExtractionSchemas(
  Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiExtractionSchemas PRIMARY KEY,
  TenantCode NVARCHAR(64) NOT NULL,
  ModuleCode NVARCHAR(64) NOT NULL,
  SchemaCode NVARCHAR(128) NOT NULL,
  DisplayName NVARCHAR(256) NOT NULL,
  CurrentVersion INT NOT NULL CONSTRAINT DF_AiExtractionSchemas_CurrentVersion DEFAULT(1),
  IsActive BIT NOT NULL CONSTRAINT DF_AiExtractionSchemas_IsActive DEFAULT(1),
  CreatedBy NVARCHAR(128) NOT NULL,
  CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
  UpdatedAtUtc DATETIMEOFFSET(7) NOT NULL,
  CONSTRAINT CK_AiExtractionSchemas_Version CHECK(CurrentVersion>0),
  CONSTRAINT UQ_AiExtractionSchemas_Tenant_Module_Code UNIQUE(TenantCode,ModuleCode,SchemaCode)
 );
END;
GO

IF OBJECT_ID(N'ai.AiExtractionSchemaVersions',N'U') IS NULL
BEGIN
 CREATE TABLE ai.AiExtractionSchemaVersions(
  Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiExtractionSchemaVersions PRIMARY KEY,
  SchemaId UNIQUEIDENTIFIER NOT NULL,
  Version INT NOT NULL,
  DefinitionJson NVARCHAR(MAX) NOT NULL,
  Status NVARCHAR(16) NOT NULL,
  CreatedBy NVARCHAR(128) NOT NULL,
  CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
  PublishedAtUtc DATETIMEOFFSET(7) NULL,
  CONSTRAINT FK_AiExtractionSchemaVersions_Schema FOREIGN KEY(SchemaId) REFERENCES ai.AiExtractionSchemas(Id),
  CONSTRAINT CK_AiExtractionSchemaVersions_Version CHECK(Version>0),
  CONSTRAINT CK_AiExtractionSchemaVersions_Status CHECK(Status IN(N'Draft',N'Published',N'Retired')),
  CONSTRAINT CK_AiExtractionSchemaVersions_Json CHECK(ISJSON(DefinitionJson)=1),
  CONSTRAINT UQ_AiExtractionSchemaVersions_Schema_Version UNIQUE(SchemaId,Version)
 );
END;
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiKnowledgeChunks') AND name=N'IX_AiKnowledgeChunks_Tenant_Active_Document')
 CREATE INDEX IX_AiKnowledgeChunks_Tenant_Active_Document
 ON ai.AiKnowledgeChunks(TenantCode,IsActive,DocumentId) INCLUDE(ChunkOrdinal,EmbeddingModel,PageNumber);
GO


IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiKnowledgeChunks') AND name=N'UX_AiKnowledgeChunks_Tenant_Document_Ordinal')
 CREATE UNIQUE INDEX UX_AiKnowledgeChunks_Tenant_Document_Ordinal
 ON ai.AiKnowledgeChunks(TenantCode,DocumentId,ChunkOrdinal);
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiFeedback') AND name=N'IX_AiFeedback_Tenant_Created_Versions')
 CREATE INDEX IX_AiFeedback_Tenant_Created_Versions
 ON ai.AiFeedback(TenantCode,CreatedAtUtc)
 INCLUDE(FeedbackType,ParserVersion,ModelVersion,PromptVersion);
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiExtractionSchemaVersions') AND name=N'IX_AiExtractionSchemaVersions_Published')
 CREATE INDEX IX_AiExtractionSchemaVersions_Published
 ON ai.AiExtractionSchemaVersions(SchemaId,Status,Version DESC);
GO
