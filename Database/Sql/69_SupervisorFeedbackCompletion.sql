/* Complete the calendar supervisor-feedback lifecycle when the linked OT request is finally approved. */
SET NOCOUNT ON;
GO

CREATE OR ALTER TRIGGER dbo.trg_F03OTRequests_CompleteSupervisorFeedback
ON dbo.F03OTRequests
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE f
       SET f.Status = 4,
           f.ResolvedAt = COALESCE(f.ResolvedAt, SYSDATETIME()),
           f.ModifiedAt = SYSDATETIME()
    FROM dbo.F03SupervisorFeedbacks f
    INNER JOIN inserted i ON i.Id = f.OTRequestId
    WHERE i.RequestStatus = 3
      AND f.Status = 3;
END;
GO
