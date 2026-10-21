/*
  Remove the legacy UNFILTERED unique index on ai.AiDocuments.
  It duplicates UX_AiDocuments_Tenant_Fingerprint_Active (filtered by DedupActive = 1)
  and blocks re-uploading a document whose previous copy has DedupActive = 0.
  Safe to run multiple times.
*/
IF EXISTS (SELECT 1
           FROM sys.indexes
           WHERE name = N'UX_AiDocuments_Tenant_Sha256_Module_Schema'
             AND object_id = OBJECT_ID(N'[ai].[AiDocuments]'))
BEGIN
    DROP INDEX [UX_AiDocuments_Tenant_Sha256_Module_Schema] ON [ai].[AiDocuments];
END
GO
