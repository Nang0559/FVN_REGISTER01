/* 021 - Persist required ANSI/quoted-identifier settings for dead-letter updates.
   The document table owns a filtered unique index. A procedure created with
   QUOTED_IDENTIFIER OFF cannot update that table and raises SQL Server error 1934.
   Keep the lease check, queue transition and document transition in one transaction.
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
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

    DECLARE @Attempts INT, @Maximum INT, @NextStatus NVARCHAR(24),
            @DelaySeconds INT, @DocumentId UNIQUEIDENTIFIER;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @Attempts=AttemptCount, @Maximum=MaxAttempts, @DocumentId=DocumentId
        FROM ai.AiIngestionJobs WITH (UPDLOCK, ROWLOCK)
        WHERE Id=@JobId AND Status=N'Running' AND LeaseOwner=@WorkerId;

        IF @Attempts IS NULL
            THROW 51004, 'Job lease is not owned by this worker.', 1;

        SET @NextStatus = CASE
            WHEN @Retryable=1 AND @Attempts < @Maximum THEN N'Queued'
            ELSE N'DeadLetter'
        END;
        SET @DelaySeconds = CASE
            WHEN @Attempts >= 10 THEN 3600
            ELSE CONVERT(INT, POWER(2.0, @Attempts) * 15)
        END;

        UPDATE ai.AiIngestionJobs
        SET Status=@NextStatus,
            NextAttemptAtUtc=CASE WHEN @NextStatus=N'Queued'
                THEN DATEADD(SECOND,@DelaySeconds,TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'))
                ELSE NextAttemptAtUtc END,
            CompletedAtUtc=CASE WHEN @NextStatus=N'DeadLetter'
                THEN TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00') ELSE NULL END,
            LeaseOwner=NULL,
            LeaseUntilUtc=NULL,
            FailureCode=LEFT(@FailureCode,128),
            LastErrorAtUtc=TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
        WHERE Id=@JobId AND Status=N'Running' AND LeaseOwner=@WorkerId;

        IF @@ROWCOUNT <> 1
            THROW 51004, 'Job lease is not owned by this worker.', 1;

        IF @NextStatus=N'DeadLetter'
            UPDATE ai.AiDocuments
            SET Status=N'Failed'
            WHERE Id=@DocumentId AND Status<>N'Failed';

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
