/*
  FVN_REGISTER AI Platform - Phase 1 schema
  Target: dedicated AI database (do not run against the FVN_REGISTER business database).
  Safe to execute repeatedly.
*/
IF SCHEMA_ID(N'ai') IS NULL
    EXEC(N'CREATE SCHEMA [ai] AUTHORIZATION [dbo]');
GO

IF OBJECT_ID(N'ai.AiDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE [ai].[AiDocuments]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_AiDocuments] PRIMARY KEY,
        [TenantCode] NVARCHAR(64) NOT NULL,
        [FileName] NVARCHAR(260) NOT NULL,
        [Extension] NVARCHAR(12) NOT NULL,
        [MediaType] NVARCHAR(128) NOT NULL,
        [Length] BIGINT NOT NULL,
        [Sha256] NVARCHAR(64) NOT NULL,
        [StorageKey] NVARCHAR(512) NOT NULL,
        [UploadedBy] NVARCHAR(128) NOT NULL,
        [ReceivedAtUtc] DATETIMEOFFSET(7) NOT NULL,
        [Status] NVARCHAR(32) NOT NULL CONSTRAINT [DF_AiDocuments_Status] DEFAULT N'Quarantined',
        CONSTRAINT [CK_AiDocuments_Length] CHECK ([Length] > 0),
        CONSTRAINT [CK_AiDocuments_Status] CHECK ([Status] IN (N'Quarantined',N'ReadyForProcessing',N'Processing',N'AwaitingHumanReview',N'Approved',N'Rejected',N'Failed',N'Archived'))
    );
    CREATE UNIQUE INDEX [UX_AiDocuments_Tenant_Sha256] ON [ai].[AiDocuments]([TenantCode],[Sha256]);
    CREATE INDEX [IX_AiDocuments_Tenant_ReceivedAt] ON [ai].[AiDocuments]([TenantCode],[ReceivedAtUtc] DESC);
END;
GO

IF OBJECT_ID(N'ai.AiIngestionJobs', N'U') IS NULL
BEGIN
    CREATE TABLE [ai].[AiIngestionJobs]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_AiIngestionJobs] PRIMARY KEY,
        [DocumentId] UNIQUEIDENTIFIER NOT NULL,
        [Status] NVARCHAR(24) NOT NULL CONSTRAINT [DF_AiIngestionJobs_Status] DEFAULT N'Queued',
        [AttemptCount] INT NOT NULL CONSTRAINT [DF_AiIngestionJobs_AttemptCount] DEFAULT 0,
        [CreatedAtUtc] DATETIMEOFFSET(7) NOT NULL,
        [StartedAtUtc] DATETIMEOFFSET(7) NULL,
        [CompletedAtUtc] DATETIMEOFFSET(7) NULL,
        [FailureCode] NVARCHAR(128) NULL,
        CONSTRAINT [FK_AiIngestionJobs_AiDocuments] FOREIGN KEY ([DocumentId]) REFERENCES [ai].[AiDocuments]([Id]),
        CONSTRAINT [CK_AiIngestionJobs_AttemptCount] CHECK ([AttemptCount] >= 0),
        CONSTRAINT [CK_AiIngestionJobs_Status] CHECK ([Status] IN (N'Queued',N'Running',N'Completed',N'Failed',N'DeadLetter'))
    );
    CREATE UNIQUE INDEX [UX_AiIngestionJobs_DocumentId] ON [ai].[AiIngestionJobs]([DocumentId]);
    CREATE INDEX [IX_AiIngestionJobs_Status_CreatedAt] ON [ai].[AiIngestionJobs]([Status],[CreatedAtUtc]);
END;
GO