/* 018 - Recover user turns abandoned in Pending after an API process crash. */
CREATE OR ALTER PROCEDURE ai.usp_RecoverStaleConversationTurns
    @StaleMinutes INT = 15,
    @BatchSize INT = 500
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @StaleMinutes < 1 OR @StaleMinutes > 1440 THROW 51018, 'Invalid stale turn age.', 1;
    IF @BatchSize < 1 OR @BatchSize > 5000 THROW 51019, 'Invalid recovery batch size.', 1;
    ;WITH stale AS
    (
        SELECT TOP (@BatchSize) Id
        FROM ai.AiConversationMessages WITH (READPAST, UPDLOCK, ROWLOCK)
        WHERE Role = N'user' AND Status = N'Pending'
          AND CreatedAtUtc <= DATEADD(MINUTE, -@StaleMinutes, TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'))
        ORDER BY CreatedAtUtc
    )
    UPDATE message SET Status = N'Failed'
    FROM ai.AiConversationMessages AS message
    INNER JOIN stale ON stale.Id = message.Id;
    SELECT @@ROWCOUNT AS RecoveredTurns;
END;
GO
