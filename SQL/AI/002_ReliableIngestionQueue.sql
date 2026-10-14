/*
 FVN AI Platform - Phase 1.1 reliability migration
 Target: dedicated FVN_AI database. Apply after 001_CreateAiIngestion.sql.
 Safe to execute repeatedly.
*/
IF COL_LENGTH(N'ai.AiIngestionJobs', N'LeaseOwner') IS NULL
    ALTER TABLE ai.AiIngestionJobs ADD LeaseOwner NVARCHAR(128) NULL;
IF COL_LENGTH(N'ai.AiIngestionJobs', N'LeaseUntilUtc') IS NULL
    ALTER TABLE ai.AiIngestionJobs ADD LeaseUntilUtc DATETIMEOFFSET(7) NULL;
IF COL_LENGTH(N'ai.AiIngestionJobs', N'NextAttemptAtUtc') IS NULL
    ALTER TABLE ai.AiIngestionJobs ADD NextAttemptAtUtc DATETIMEOFFSET(7) NOT NULL
        CONSTRAINT DF_AiIngestionJobs_NextAttemptAtUtc DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00');
IF COL_LENGTH(N'ai.AiIngestionJobs', N'LastAttemptAtUtc') IS NULL
    ALTER TABLE ai.AiIngestionJobs ADD LastAttemptAtUtc DATETIMEOFFSET(7) NULL;
IF COL_LENGTH(N'ai.AiIngestionJobs', N'MaxAttempts') IS NULL
    ALTER TABLE ai.AiIngestionJobs ADD MaxAttempts INT NOT NULL
        CONSTRAINT DF_AiIngestionJobs_MaxAttempts DEFAULT 5;
IF COL_LENGTH(N'ai.AiIngestionJobs', N'LastErrorAtUtc') IS NULL
    ALTER TABLE ai.AiIngestionJobs ADD LastErrorAtUtc DATETIMEOFFSET(7) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_AiIngestionJobs_Queue' AND object_id=OBJECT_ID(N'ai.AiIngestionJobs'))
    CREATE INDEX IX_AiIngestionJobs_Queue
      ON ai.AiIngestionJobs(Status, NextAttemptAtUtc, CreatedAtUtc)
      INCLUDE (DocumentId, AttemptCount, MaxAttempts, LeaseUntilUtc);
GO

CREATE OR ALTER PROCEDURE ai.usp_RecoverExpiredAiIngestionLeases
    @NowUtc DATETIMEOFFSET(7) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @NowUtc = COALESCE(@NowUtc, TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'));

    ;WITH Expired AS
    (
        SELECT TOP (500) *
        FROM ai.AiIngestionJobs WITH (UPDLOCK, READPAST, ROWLOCK)
        WHERE Status = N'Running' AND LeaseUntilUtc IS NOT NULL AND LeaseUntilUtc < @NowUtc
        ORDER BY LeaseUntilUtc, CreatedAtUtc
    )
    UPDATE Expired
       SET Status = CASE WHEN AttemptCount >= MaxAttempts THEN N'DeadLetter' ELSE N'Queued' END,
           LeaseOwner = NULL,
           LeaseUntilUtc = NULL,
           NextAttemptAtUtc = @NowUtc,
           FailureCode = CASE WHEN AttemptCount >= MaxAttempts THEN N'LEASE_EXPIRED_MAX_ATTEMPTS' ELSE N'LEASE_EXPIRED_REQUEUED' END,
           CompletedAtUtc = CASE WHEN AttemptCount >= MaxAttempts THEN @NowUtc ELSE NULL END;
END;
GO

CREATE OR ALTER PROCEDURE ai.usp_ClaimAiIngestionJob
    @WorkerId NVARCHAR(128),
    @LeaseSeconds INT = 120,
    @NowUtc DATETIMEOFFSET(7) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@WorkerId)), N'') IS NULL THROW 51001, 'WorkerId is required.', 1;
    IF @LeaseSeconds < 15 OR @LeaseSeconds > 3600 THROW 51002, 'LeaseSeconds must be between 15 and 3600.', 1;
    SET @NowUtc = COALESCE(@NowUtc, TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'));

    ;WITH Candidate AS
    (
        SELECT TOP (1) *
        FROM ai.AiIngestionJobs WITH (UPDLOCK, READPAST, ROWLOCK, INDEX(IX_AiIngestionJobs_Queue))
        WHERE Status = N'Queued' AND NextAttemptAtUtc <= @NowUtc
        ORDER BY NextAttemptAtUtc, CreatedAtUtc
    )
    UPDATE Candidate
       SET Status = N'Running',
           AttemptCount = AttemptCount + 1,
           StartedAtUtc = @NowUtc,
           LastAttemptAtUtc = @NowUtc,
           LeaseOwner = @WorkerId,
           LeaseUntilUtc = DATEADD(SECOND, @LeaseSeconds, @NowUtc),
           FailureCode = NULL,
           CompletedAtUtc = NULL
    OUTPUT inserted.Id AS JobId, inserted.DocumentId, inserted.AttemptCount,
           inserted.LeaseOwner, inserted.LeaseUntilUtc;
END;
GO

CREATE OR ALTER PROCEDURE ai.usp_CompleteAiIngestionJob
    @JobId UNIQUEIDENTIFIER,
    @WorkerId NVARCHAR(128),
    @DocumentStatus NVARCHAR(32) = N'AwaitingHumanReview'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    UPDATE ai.AiIngestionJobs
       SET Status=N'Completed', CompletedAtUtc=TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
           LeaseOwner=NULL, LeaseUntilUtc=NULL, FailureCode=NULL
     WHERE Id=@JobId AND Status=N'Running' AND LeaseOwner=@WorkerId;
    IF @@ROWCOUNT <> 1 BEGIN ROLLBACK; THROW 51003, 'Job lease is not owned by this worker.', 1; END;
    UPDATE ai.AiDocuments SET Status=@DocumentStatus
     WHERE Id=(SELECT DocumentId FROM ai.AiIngestionJobs WHERE Id=@JobId);
    COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE ai.usp_FailAiIngestionJob
    @JobId UNIQUEIDENTIFIER,
    @WorkerId NVARCHAR(128),
    @FailureCode NVARCHAR(128),
    @Retryable BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Attempts INT, @Maximum INT, @NextStatus NVARCHAR(24), @DelaySeconds INT;
    SELECT @Attempts=AttemptCount, @Maximum=MaxAttempts
      FROM ai.AiIngestionJobs WITH (UPDLOCK, ROWLOCK)
     WHERE Id=@JobId AND Status=N'Running' AND LeaseOwner=@WorkerId;
    IF @Attempts IS NULL THROW 51004, 'Job lease is not owned by this worker.', 1;
    SET @NextStatus = CASE WHEN @Retryable=1 AND @Attempts < @Maximum THEN N'Queued' ELSE N'DeadLetter' END;
    SET @DelaySeconds = CASE WHEN @Attempts >= 10 THEN 3600 ELSE CONVERT(INT, POWER(2.0, @Attempts) * 15) END;
    UPDATE ai.AiIngestionJobs
       SET Status=@NextStatus,
           NextAttemptAtUtc=CASE WHEN @NextStatus=N'Queued' THEN DATEADD(SECOND,@DelaySeconds,TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')) ELSE NextAttemptAtUtc END,
           CompletedAtUtc=CASE WHEN @NextStatus=N'DeadLetter' THEN TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00') ELSE NULL END,
           LeaseOwner=NULL, LeaseUntilUtc=NULL, FailureCode=LEFT(@FailureCode,128),
           LastErrorAtUtc=TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
     WHERE Id=@JobId;
END;
GO
