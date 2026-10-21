/* 020 - Serialize the short queue-claim critical section.
   CI exposed repeatable SQL Server deadlocks when many workers updated the same
   ordered nonclustered queue index concurrently. A transaction-owned application
   lock serializes only dequeue/claim; document processing remains fully parallel.
*/
CREATE OR ALTER PROCEDURE ai.usp_ClaimAiIngestionJob
    @WorkerId NVARCHAR(128),
    @LeaseSeconds INT = 120,
    @NowUtc DATETIMEOFFSET(7) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@WorkerId)), N'') IS NULL
        THROW 51001, 'WorkerId is required.', 1;
    IF @LeaseSeconds < 15 OR @LeaseSeconds > 3600
        THROW 51002, 'LeaseSeconds must be between 15 and 3600.', 1;

    SET @NowUtc = COALESCE(@NowUtc, TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'));

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @LockResult INT;
        EXEC @LockResult = sys.sp_getapplock
            @Resource = N'ai:ingestion:claim',
            @LockMode = N'Exclusive',
            @LockOwner = N'Transaction',
            @LockTimeout = 15000;

        IF @LockResult < 0
            THROW 51007, 'Queue claim lock could not be acquired.', 1;

        ;WITH Candidate AS
        (
            SELECT TOP (1) Id
            FROM ai.AiIngestionJobs WITH (UPDLOCK, READPAST, ROWLOCK, INDEX(IX_AiIngestionJobs_Queue))
            WHERE Status = N'Queued' AND NextAttemptAtUtc <= @NowUtc
            ORDER BY NextAttemptAtUtc, CreatedAtUtc, Id
        )
        UPDATE job
           SET Status = N'Running',
               AttemptCount = job.AttemptCount + 1,
               StartedAtUtc = @NowUtc,
               LastAttemptAtUtc = @NowUtc,
               LeaseOwner = @WorkerId,
               LeaseUntilUtc = DATEADD(SECOND, @LeaseSeconds, @NowUtc),
               FailureCode = NULL,
               CompletedAtUtc = NULL
        OUTPUT inserted.Id AS JobId, inserted.DocumentId, inserted.AttemptCount,
               inserted.LeaseOwner, inserted.LeaseUntilUtc
        FROM ai.AiIngestionJobs AS job
        INNER JOIN Candidate AS candidate ON candidate.Id = job.Id;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
