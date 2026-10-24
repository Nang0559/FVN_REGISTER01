/* 019 - Avoid an AFTER trigger on the queue table because the lease-claim procedure uses OUTPUT. Keep dead-letter document state inside the failure procedure. */
IF OBJECT_ID(N'ai.trg_AiIngestionJobs_DeadLetterDocumentStatus',N'TR') IS NOT NULL
    DROP TRIGGER ai.trg_AiIngestionJobs_DeadLetterDocumentStatus;
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
    DECLARE @Attempts INT, @Maximum INT, @NextStatus NVARCHAR(24), @DelaySeconds INT, @DocumentId UNIQUEIDENTIFIER;
    SELECT @Attempts=AttemptCount, @Maximum=MaxAttempts, @DocumentId=DocumentId
      FROM ai.AiIngestionJobs WITH (UPDLOCK, ROWLOCK)
     WHERE Id=@JobId AND Status=N'Running' AND LeaseOwner=@WorkerId;
    IF @Attempts IS NULL THROW 51004, 'Job lease is not owned by this worker.', 1;
    SET @NextStatus = CASE WHEN @Retryable=1 AND @Attempts < @Maximum THEN N'Queued' ELSE N'DeadLetter' END;
    SET @DelaySeconds = CASE WHEN @Attempts >= 10 THEN 3600 ELSE CONVERT(INT, POWER(2.0, @Attempts) * 15) END;
    BEGIN TRANSACTION;
    UPDATE ai.AiIngestionJobs
       SET Status=@NextStatus,
           NextAttemptAtUtc=CASE WHEN @NextStatus=N'Queued' THEN DATEADD(SECOND,@DelaySeconds,TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')) ELSE NextAttemptAtUtc END,
           CompletedAtUtc=CASE WHEN @NextStatus=N'DeadLetter' THEN TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00') ELSE NULL END,
           LeaseOwner=NULL, LeaseUntilUtc=NULL, FailureCode=LEFT(@FailureCode,128),
           LastErrorAtUtc=TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
     WHERE Id=@JobId AND Status=N'Running' AND LeaseOwner=@WorkerId;
    IF @@ROWCOUNT <> 1 THROW 51004, 'Job lease is not owned by this worker.', 1;
    IF @NextStatus=N'DeadLetter'
        UPDATE ai.AiDocuments SET Status=N'Failed' WHERE Id=@DocumentId AND Status<>N'Failed';
    COMMIT TRANSACTION;
END;
GO
