/*
 015 - Generic Document Data Entry.
 Reusable for HR/FVN_REGISTER now and production/quality documents later.
 It stores templates, candidate rows, human-confirmed rows and durable worker state.
 No operational FVN_REGISTER business tables are modified.
*/
IF SCHEMA_ID(N'ai') IS NULL EXEC(N'CREATE SCHEMA ai');
GO

IF OBJECT_ID(N'ai.AiDataEntryTemplates',N'U') IS NULL
BEGIN
 CREATE TABLE ai.AiDataEntryTemplates(
  Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiDataEntryTemplates PRIMARY KEY,
  TenantCode NVARCHAR(64) NOT NULL,
  TemplateCode NVARCHAR(128) NOT NULL,
  DisplayName NVARCHAR(256) NOT NULL,
  ModuleCode NVARCHAR(64) NOT NULL,
  SchemaCode NVARCHAR(128) NOT NULL,
  Version INT NOT NULL CONSTRAINT DF_AiDataEntryTemplates_Version DEFAULT(1),
  StorageKey NVARCHAR(512) NOT NULL,
  WorksheetName NVARCHAR(128) NOT NULL,
  HeaderRow INT NOT NULL,
  DataStartRow INT NOT NULL,
  DefinitionJson NVARCHAR(MAX) NOT NULL,
  IsActive BIT NOT NULL CONSTRAINT DF_AiDataEntryTemplates_IsActive DEFAULT(1),
  CreatedBy NVARCHAR(128) NOT NULL,
  CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
  UpdatedAtUtc DATETIMEOFFSET(7) NOT NULL,
  CONSTRAINT CK_AiDataEntryTemplates_Version CHECK(Version>0),
  CONSTRAINT CK_AiDataEntryTemplates_HeaderRow CHECK(HeaderRow>0),
  CONSTRAINT CK_AiDataEntryTemplates_DataStartRow CHECK(DataStartRow>0),
  CONSTRAINT CK_AiDataEntryTemplates_Json CHECK(ISJSON(DefinitionJson)=1),
  CONSTRAINT UQ_AiDataEntryTemplates_Tenant_Code_Version UNIQUE(TenantCode,TemplateCode,Version)
 );
END;
GO

IF OBJECT_ID(N'ai.AiDataEntryRuns',N'U') IS NULL
BEGIN
 CREATE TABLE ai.AiDataEntryRuns(
  Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AiDataEntryRuns PRIMARY KEY,
  TenantCode NVARCHAR(64) NOT NULL,
  DocumentId UNIQUEIDENTIFIER NOT NULL,
  TemplateId UNIQUEIDENTIFIER NOT NULL,
  Status NVARCHAR(32) NOT NULL CONSTRAINT DF_AiDataEntryRuns_Status DEFAULT(N'WaitingForDocument'),
  AttemptCount INT NOT NULL CONSTRAINT DF_AiDataEntryRuns_AttemptCount DEFAULT(0),
  MaxAttempts INT NOT NULL CONSTRAINT DF_AiDataEntryRuns_MaxAttempts DEFAULT(5),
  CreatedAtUtc DATETIMEOFFSET(7) NOT NULL,
  UpdatedAtUtc DATETIMEOFFSET(7) NOT NULL,
  StartedAtUtc DATETIMEOFFSET(7) NULL,
  NextAttemptAtUtc DATETIMEOFFSET(7) NULL,
  LastAttemptAtUtc DATETIMEOFFSET(7) NULL,
  LastErrorAtUtc DATETIMEOFFSET(7) NULL,
  LeaseUntilUtc DATETIMEOFFSET(7) NULL,
  LeaseOwner NVARCHAR(128) NULL,
  FailureCode NVARCHAR(128) NULL,
  CandidateRowsJson NVARCHAR(MAX) NULL,
  ValidationJson NVARCHAR(MAX) NULL,
  ConfirmedRowsJson NVARCHAR(MAX) NULL,
  Comment NVARCHAR(4000) NULL,
  ModelProvider NVARCHAR(64) NULL,
  ModelVersion NVARCHAR(128) NULL,
  OutputStorageKey NVARCHAR(512) NULL,
  OutputFileName NVARCHAR(260) NULL,
  CompletedAtUtc DATETIMEOFFSET(7) NULL,
  CreatedBy NVARCHAR(128) NOT NULL,
  CONSTRAINT FK_AiDataEntryRuns_Document FOREIGN KEY(DocumentId) REFERENCES ai.AiDocuments(Id),
  CONSTRAINT FK_AiDataEntryRuns_Template FOREIGN KEY(TemplateId) REFERENCES ai.AiDataEntryTemplates(Id),
  CONSTRAINT CK_AiDataEntryRuns_Attempts CHECK(AttemptCount>=0 AND MaxAttempts>0),
  CONSTRAINT CK_AiDataEntryRuns_CandidateJson CHECK(CandidateRowsJson IS NULL OR ISJSON(CandidateRowsJson)=1),
  CONSTRAINT CK_AiDataEntryRuns_ValidationJson CHECK(ValidationJson IS NULL OR ISJSON(ValidationJson)=1),
  CONSTRAINT CK_AiDataEntryRuns_ConfirmedJson CHECK(ConfirmedRowsJson IS NULL OR ISJSON(ConfirmedRowsJson)=1)
 );
END;
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiDataEntryTemplates') AND name=N'IX_AiDataEntryTemplates_Tenant_Active')
 CREATE INDEX IX_AiDataEntryTemplates_Tenant_Active ON ai.AiDataEntryTemplates(TenantCode,IsActive,TemplateCode,Version DESC);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiDataEntryRuns') AND name=N'IX_AiDataEntryRuns_Queue')
 CREATE INDEX IX_AiDataEntryRuns_Queue ON ai.AiDataEntryRuns(Status,NextAttemptAtUtc,CreatedAtUtc) INCLUDE(TenantCode,DocumentId,TemplateId,AttemptCount,MaxAttempts,LeaseOwner,LeaseUntilUtc,FailureCode);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiDataEntryRuns') AND name=N'IX_AiDataEntryRuns_Tenant_Document')
 CREATE INDEX IX_AiDataEntryRuns_Tenant_Document ON ai.AiDataEntryRuns(TenantCode,DocumentId,CreatedAtUtc DESC);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiDataEntryRuns') AND name=N'IX_AiDataEntryRuns_Tenant_Template')
 CREATE INDEX IX_AiDataEntryRuns_Tenant_Template ON ai.AiDataEntryRuns(TenantCode,TemplateId,CreatedAtUtc DESC);
GO
