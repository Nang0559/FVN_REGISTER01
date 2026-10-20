/*
 FVN_REGISTER AI Platform - migration 014.
 Phase 5 release/provenance, retention, replay and tenant hardening.
 Apply after 013. Safe to execute repeatedly; never rewrite prior migrations.
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'ai') IS NULL EXEC(N'CREATE SCHEMA [ai] AUTHORIZATION [dbo]');
GO

/* 1. Release-bound extraction provenance and model-aware extraction uniqueness. */
IF COL_LENGTH(N'ai.AiExtractions', N'ReleaseId') IS NULL
    ALTER TABLE ai.AiExtractions ADD ReleaseId UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH(N'ai.AiFeedback', N'ReleaseId') IS NULL
    ALTER TABLE ai.AiFeedback ADD ReleaseId UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH(N'ai.AiExtractions', N'CandidateJson') IS NOT NULL
    ALTER TABLE ai.AiExtractions ALTER COLUMN CandidateJson NVARCHAR(MAX) NULL;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiExtractions') AND name=N'UX_AiExtractions_Document_SchemaVersion')
    DROP INDEX UX_AiExtractions_Document_SchemaVersion ON ai.AiExtractions;
GO
IF EXISTS
(
    SELECT 1 FROM ai.AiExtractions
    WHERE SchemaVersion IS NOT NULL
    GROUP BY DocumentId,SchemaVersion,ModelProvider,ModelVersion,PromptVersion
    HAVING COUNT_BIG(*) > 1
)
    THROW 51020, 'Duplicate extraction provenance exists for DocumentId/SchemaVersion/ModelProvider/ModelVersion/PromptVersion.', 1;
GO
IF EXISTS
(
    SELECT 1 FROM ai.AiExtractions
    WHERE SchemaVersion IS NULL
    GROUP BY DocumentId,ModelProvider,ModelVersion,PromptVersion
    HAVING COUNT_BIG(*) > 1
)
    THROW 51021, 'Duplicate unversioned extraction provenance exists. Reconcile legacy rows before migration 014.', 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiExtractions') AND name=N'UX_AiExtractions_Document_Schema_Model')
    CREATE UNIQUE INDEX UX_AiExtractions_Document_Schema_Model
      ON ai.AiExtractions(DocumentId,SchemaVersion,ModelProvider,ModelVersion,PromptVersion)
      WHERE SchemaVersion IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiExtractions') AND name=N'UX_AiExtractions_Document_Unversioned_Model')
    CREATE UNIQUE INDEX UX_AiExtractions_Document_Unversioned_Model
      ON ai.AiExtractions(DocumentId,ModelProvider,ModelVersion,PromptVersion)
      WHERE SchemaVersion IS NULL;
GO

/* 2. Tenant-bound validation issues and golden evaluations. */
IF COL_LENGTH(N'ai.AiValidationIssues', N'TenantCode') IS NULL
    ALTER TABLE ai.AiValidationIssues ADD TenantCode NVARCHAR(64) NULL;
GO
UPDATE vi SET TenantCode=e.TenantCode
FROM ai.AiValidationIssues vi INNER JOIN ai.AiExtractions e ON e.Id=vi.ExtractionId
WHERE vi.TenantCode IS NULL;
GO
IF EXISTS (SELECT 1 FROM ai.AiValidationIssues WHERE TenantCode IS NULL)
    THROW 51022, 'AiValidationIssues contains rows without a tenant; reconcile them before migration 014.', 1;
GO
ALTER TABLE ai.AiValidationIssues ALTER COLUMN TenantCode NVARCHAR(64) NOT NULL;
GO

IF COL_LENGTH(N'ai.AiGoldenDatasetEvaluations', N'TenantCode') IS NULL
    ALTER TABLE ai.AiGoldenDatasetEvaluations ADD TenantCode NVARCHAR(64) NULL;
GO
UPDATE ge SET TenantCode=c.TenantCode
FROM ai.AiGoldenDatasetEvaluations ge INNER JOIN ai.AiGoldenDatasetCases c ON c.Id=ge.CaseId
WHERE ge.TenantCode IS NULL;
GO
IF EXISTS (SELECT 1 FROM ai.AiGoldenDatasetEvaluations WHERE TenantCode IS NULL)
    THROW 51023, 'AiGoldenDatasetEvaluations contains rows without a tenant; reconcile them before migration 014.', 1;
GO
ALTER TABLE ai.AiGoldenDatasetEvaluations ALTER COLUMN TenantCode NVARCHAR(64) NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiExtractions') AND name=N'UX_AiExtractions_Tenant_Id')
    CREATE UNIQUE INDEX UX_AiExtractions_Tenant_Id ON ai.AiExtractions(TenantCode,Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiModelReleases') AND name=N'UX_AiModelReleases_Tenant_Id')
    CREATE UNIQUE INDEX UX_AiModelReleases_Tenant_Id ON ai.AiModelReleases(TenantCode,Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiGoldenDatasetCases') AND name=N'UX_AiGoldenDatasetCases_Tenant_Id')
    CREATE UNIQUE INDEX UX_AiGoldenDatasetCases_Tenant_Id ON ai.AiGoldenDatasetCases(TenantCode,Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AiValidationIssues_TenantExtraction')
    ALTER TABLE ai.AiValidationIssues ADD CONSTRAINT FK_AiValidationIssues_TenantExtraction
      FOREIGN KEY(TenantCode,ExtractionId) REFERENCES ai.AiExtractions(TenantCode,Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AiGoldenDatasetEvaluations_TenantCase')
    ALTER TABLE ai.AiGoldenDatasetEvaluations ADD CONSTRAINT FK_AiGoldenDatasetEvaluations_TenantCase
      FOREIGN KEY(TenantCode,CaseId) REFERENCES ai.AiGoldenDatasetCases(TenantCode,Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AiGoldenDatasetEvaluations_TenantRelease')
    ALTER TABLE ai.AiGoldenDatasetEvaluations ADD CONSTRAINT FK_AiGoldenDatasetEvaluations_TenantRelease
      FOREIGN KEY(TenantCode,ReleaseId) REFERENCES ai.AiModelReleases(TenantCode,Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AiExtractions_TenantRelease')
    ALTER TABLE ai.AiExtractions ADD CONSTRAINT FK_AiExtractions_TenantRelease
      FOREIGN KEY(TenantCode,ReleaseId) REFERENCES ai.AiModelReleases(TenantCode,Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_AiFeedback_TenantRelease')
    ALTER TABLE ai.AiFeedback ADD CONSTRAINT FK_AiFeedback_TenantRelease
      FOREIGN KEY(TenantCode,ReleaseId) REFERENCES ai.AiModelReleases(TenantCode,Id);
GO

/* 3. Allow expired/rejected documents to be physically purged and re-uploaded. */
IF COL_LENGTH(N'ai.AiDocuments', N'DedupActive') IS NULL
BEGIN
    ALTER TABLE ai.AiDocuments ADD DedupActive BIT NOT NULL CONSTRAINT DF_AiDocuments_DedupActive DEFAULT(1) WITH VALUES;
END;
GO
UPDATE ai.AiDocuments SET DedupActive=0 WHERE Status IN(N'Archived',N'Rejected');
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiDocuments') AND name=N'UX_AiDocuments_Tenant_Sha256')
    DROP INDEX UX_AiDocuments_Tenant_Sha256 ON ai.AiDocuments;
GO
IF EXISTS
(
    SELECT 1 FROM ai.AiDocuments
    WHERE DedupActive=1
    GROUP BY TenantCode,Sha256,ModuleCode,SchemaCode
    HAVING COUNT_BIG(*)>1
)
    THROW 51024, 'Duplicate active document fingerprints exist. Reconcile them before migration 014.', 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'ai.AiDocuments') AND name=N'UX_AiDocuments_Tenant_Fingerprint_Active')
    CREATE UNIQUE INDEX UX_AiDocuments_Tenant_Fingerprint_Active
      ON ai.AiDocuments(TenantCode,Sha256,ModuleCode,SchemaCode)
      WHERE DedupActive=1;
GO

/* Knowledge and extraction payloads are purgeable retention material. */
IF COL_LENGTH(N'ai.AiKnowledgeChunks', N'Content') IS NOT NULL
    ALTER TABLE ai.AiKnowledgeChunks ALTER COLUMN Content NVARCHAR(MAX) NULL;
GO

/* 4. Effective replay retention is at least ten minutes, matching the ±5 minute clock window. */
CREATE OR ALTER TRIGGER ai.trg_AiRequestNonces_MinReplayWindow
ON ai.AiRequestNonces
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE n
       SET ExpiresAtUtc = CASE
            WHEN n.ExpiresAtUtc < DATEADD(MINUTE,10,n.CreatedAtUtc)
            THEN DATEADD(MINUTE,10,n.CreatedAtUtc)
            ELSE n.ExpiresAtUtc END
    FROM ai.AiRequestNonces n INNER JOIN inserted i
      ON i.TenantCode=n.TenantCode AND i.Nonce=n.Nonce;
END;
GO

/* 5. Dead-letter/lease state must be reflected on the document as well as the job. */
CREATE OR ALTER PROCEDURE ai.usp_ClaimAiIngestionJob
    @WorkerId NVARCHAR(128), @LeaseSeconds INT=120, @NowUtc DATETIMEOFFSET(7)=NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@WorkerId)),N'') IS NULL THROW 51001,'WorkerId is required.',1;
    IF @LeaseSeconds<15 OR @LeaseSeconds>3600 THROW 51002,'LeaseSeconds must be between 15 and 3600.',1;
    SET @NowUtc=COALESCE(@NowUtc,TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00'));
    ;WITH Candidate AS
    (
      SELECT TOP(1) * FROM ai.AiIngestionJobs WITH(UPDLOCK,READPAST,ROWLOCK,INDEX(IX_AiIngestionJobs_Queue))
      WHERE Status=N'Queued' AND NextAttemptAtUtc<=@NowUtc ORDER BY NextAttemptAtUtc,CreatedAtUtc
    )
    UPDATE Candidate SET Status=N'Running',AttemptCount=AttemptCount+1,StartedAtUtc=@NowUtc,LastAttemptAtUtc=@NowUtc,LeaseOwner=@WorkerId,LeaseUntilUtc=DATEADD(SECOND,@LeaseSeconds,@NowUtc),FailureCode=NULL,CompletedAtUtc=NULL
    OUTPUT inserted.Id AS JobId,inserted.DocumentId,inserted.AttemptCount,inserted.LeaseOwner,inserted.LeaseUntilUtc;
    UPDATE d SET Status=N'Processing'
    FROM ai.AiDocuments d INNER JOIN ai.AiIngestionJobs j ON j.DocumentId=d.Id
    WHERE j.LeaseOwner=@WorkerId AND j.Status=N'Running' AND j.StartedAtUtc=@NowUtc;
END;
GO

CREATE OR ALTER PROCEDURE ai.usp_FailAiIngestionJob
    @JobId UNIQUEIDENTIFIER,@WorkerId NVARCHAR(128),@FailureCode NVARCHAR(128),@Retryable BIT=1
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @Attempts INT,@Maximum INT,@NextStatus NVARCHAR(24),@DelaySeconds INT,@DocumentId UNIQUEIDENTIFIER;
    SELECT @Attempts=AttemptCount,@Maximum=MaxAttempts,@DocumentId=DocumentId FROM ai.AiIngestionJobs WITH(UPDLOCK,ROWLOCK) WHERE Id=@JobId AND Status=N'Running' AND LeaseOwner=@WorkerId;
    IF @Attempts IS NULL THROW 51004,'Job lease is not owned by this worker.',1;
    SET @NextStatus=CASE WHEN @Retryable=1 AND @Attempts<@Maximum THEN N'Queued' ELSE N'DeadLetter' END;
    SET @DelaySeconds=CASE WHEN @Attempts>=10 THEN 3600 ELSE CONVERT(INT,POWER(2.0,@Attempts)*15) END;
    UPDATE ai.AiIngestionJobs SET Status=@NextStatus,NextAttemptAtUtc=CASE WHEN @NextStatus=N'Queued' THEN DATEADD(SECOND,@DelaySeconds,TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00')) ELSE NextAttemptAtUtc END,CompletedAtUtc=CASE WHEN @NextStatus=N'DeadLetter' THEN TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00') ELSE NULL END,LeaseOwner=NULL,LeaseUntilUtc=NULL,FailureCode=LEFT(@FailureCode,128),LastErrorAtUtc=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00') WHERE Id=@JobId;
    UPDATE ai.AiDocuments SET Status=CASE WHEN @NextStatus=N'DeadLetter' THEN N'Failed' ELSE N'Quarantined' END WHERE Id=@DocumentId AND Status<>N'Archived';
END;
GO

CREATE OR ALTER PROCEDURE ai.usp_RecoverExpiredAiIngestionLeases
    @NowUtc DATETIMEOFFSET(7)=NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON; SET @NowUtc=COALESCE(@NowUtc,TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00'));
    ;WITH Expired AS
    (
      SELECT TOP(500) * FROM ai.AiIngestionJobs WITH(UPDLOCK,READPAST,ROWLOCK)
      WHERE Status=N'Running' AND LeaseUntilUtc IS NOT NULL AND LeaseUntilUtc<@NowUtc
      ORDER BY LeaseUntilUtc,CreatedAtUtc
    )
    UPDATE Expired SET Status=CASE WHEN AttemptCount>=MaxAttempts THEN N'DeadLetter' ELSE N'Queued' END,LeaseOwner=NULL,LeaseUntilUtc=NULL,NextAttemptAtUtc=@NowUtc,FailureCode=CASE WHEN AttemptCount>=MaxAttempts THEN N'LEASE_EXPIRED_MAX_ATTEMPTS' ELSE N'LEASE_EXPIRED_REQUEUED' END,CompletedAtUtc=CASE WHEN AttemptCount>=MaxAttempts THEN @NowUtc ELSE NULL END;
    UPDATE d SET Status=CASE WHEN j.Status=N'DeadLetter' THEN N'Failed' ELSE N'Quarantined' END
    FROM ai.AiDocuments d INNER JOIN ai.AiIngestionJobs j ON j.DocumentId=d.Id
    WHERE j.FailureCode IN(N'LEASE_EXPIRED_MAX_ATTEMPTS',N'LEASE_EXPIRED_REQUEUED') AND j.NextAttemptAtUtc=@NowUtc AND d.Status<>N'Archived';
END;
GO

IF OBJECT_ID(N'ai.AiSchemaMigrations',N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS(SELECT 1 FROM ai.AiSchemaMigrations WHERE MigrationId=N'014_Phase5_ReleaseRetentionTenantHardening')
        INSERT INTO ai.AiSchemaMigrations(MigrationId,MigrationFileName) VALUES(N'014_Phase5_ReleaseRetentionTenantHardening',N'014_Phase5_ReleaseRetentionTenantHardening.sql');
END;
GO
