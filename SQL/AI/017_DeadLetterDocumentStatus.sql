/* 017 - Keep document lifecycle status consistent when an ingestion job reaches DeadLetter. */
CREATE OR ALTER TRIGGER ai.trg_AiIngestionJobs_DeadLetterDocumentStatus
ON ai.AiIngestionJobs
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE d
       SET Status = N'Failed'
    FROM ai.AiDocuments AS d
    INNER JOIN inserted AS i ON i.DocumentId = d.Id
    INNER JOIN deleted AS old ON old.Id = i.Id
    WHERE i.Status = N'DeadLetter'
      AND old.Status <> N'DeadLetter'
      AND d.Status <> N'Failed';
END;
GO
