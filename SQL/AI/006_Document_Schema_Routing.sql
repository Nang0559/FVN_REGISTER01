/* Store the optional module/schema selection supplied by the trusted application gateway. */
IF COL_LENGTH(N'ai.AiDocuments',N'ModuleCode') IS NULL
 ALTER TABLE ai.AiDocuments ADD ModuleCode NVARCHAR(64) NULL;
GO
IF COL_LENGTH(N'ai.AiDocuments',N'SchemaCode') IS NULL
 ALTER TABLE ai.AiDocuments ADD SchemaCode NVARCHAR(128) NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiDocuments') AND name=N'IX_AiDocuments_Tenant_Module_Schema')
 CREATE INDEX IX_AiDocuments_Tenant_Module_Schema ON ai.AiDocuments(TenantCode,ModuleCode,SchemaCode,ReceivedAtUtc);
GO

/* A byte-identical file may be reused across different module/schema workflows. */
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiDocuments') AND name=N'UX_AiDocuments_Tenant_Sha256')
 DROP INDEX UX_AiDocuments_Tenant_Sha256 ON ai.AiDocuments;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiDocuments') AND name=N'UX_AiDocuments_Tenant_Sha256_Module_Schema')
 CREATE UNIQUE INDEX UX_AiDocuments_Tenant_Sha256_Module_Schema
 ON ai.AiDocuments(TenantCode,Sha256,ModuleCode,SchemaCode);
GO
