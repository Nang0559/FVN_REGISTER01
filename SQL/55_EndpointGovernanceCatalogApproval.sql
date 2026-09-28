/*
===============================================================================
FVN_REGISTER - Endpoint Governance catalog approval migration

Extends the existing Endpoint Governance foundation. It does not create a new
approval subsystem. Catalog versions become F03EndpointGovernanceRequests and
flow through RequestModule.Endpoint + Common Approval.

Equipment integration is a reference only: F03EndpointDevices.EquipmentAssetId
points to the existing Equipment asset. No Equipment entity or approval model is
duplicated here.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'WorkflowStatus') IS NULL
    ALTER TABLE dbo.F03EndpointGovernancePolicies ADD WorkflowStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointGovernancePolicies_Workflow DEFAULT ('Draft');
IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'ApprovalRequestId') IS NULL
    ALTER TABLE dbo.F03EndpointGovernancePolicies ADD ApprovalRequestId INT NULL;
IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'ApprovalSnapshotId') IS NULL
    ALTER TABLE dbo.F03EndpointGovernancePolicies ADD ApprovalSnapshotId INT NULL;
IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'SubmittedAtUtc') IS NULL
    ALTER TABLE dbo.F03EndpointGovernancePolicies ADD SubmittedAtUtc DATETIME2(0) NULL;
IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'SubmittedBy') IS NULL
    ALTER TABLE dbo.F03EndpointGovernancePolicies ADD SubmittedBy INT NULL;

IF COL_LENGTH(N'dbo.F03EndpointGovernanceRequests', N'PolicyId') IS NULL
    ALTER TABLE dbo.F03EndpointGovernanceRequests ADD PolicyId INT NULL;
IF COL_LENGTH(N'dbo.F03EndpointGovernanceRequests', N'PolicyVersion') IS NULL
    ALTER TABLE dbo.F03EndpointGovernanceRequests ADD PolicyVersion INT NULL;
IF COL_LENGTH(N'dbo.F03EndpointGovernanceRequests', N'PolicyItemType') IS NULL
    ALTER TABLE dbo.F03EndpointGovernanceRequests ADD PolicyItemType INT NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.F03EndpointGovernanceRequests') AND name = N'EndpointDeviceId' AND is_nullable = 0)
BEGIN
    DECLARE @fk sysname;
    SELECT @fk = fk.name
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
    WHERE fk.parent_object_id = OBJECT_ID(N'dbo.F03EndpointGovernanceRequests') AND c.name = N'EndpointDeviceId';
    IF @fk IS NOT NULL EXEC(N'ALTER TABLE dbo.F03EndpointGovernanceRequests DROP CONSTRAINT [' + @fk + N']');
    ALTER TABLE dbo.F03EndpointGovernanceRequests ALTER COLUMN EndpointDeviceId BIGINT NULL;
    ALTER TABLE dbo.F03EndpointGovernanceRequests ADD CONSTRAINT FK_F03EndpointGovernanceRequests_Device FOREIGN KEY (EndpointDeviceId) REFERENCES dbo.F03EndpointDevices(Id);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_F03EndpointGovernancePolicies_ApprovalRequest')
    ALTER TABLE dbo.F03EndpointGovernancePolicies ADD CONSTRAINT FK_F03EndpointGovernancePolicies_ApprovalRequest FOREIGN KEY (ApprovalRequestId) REFERENCES dbo.F03EndpointGovernanceRequests(Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_F03EndpointGovernancePolicies_ApprovalSnapshot')
    ALTER TABLE dbo.F03EndpointGovernancePolicies ADD CONSTRAINT FK_F03EndpointGovernancePolicies_ApprovalSnapshot FOREIGN KEY (ApprovalSnapshotId) REFERENCES dbo.F03ApprovalSnapshots(Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_F03EndpointGovernanceRequests_Policy')
    ALTER TABLE dbo.F03EndpointGovernanceRequests ADD CONSTRAINT FK_F03EndpointGovernanceRequests_Policy FOREIGN KEY (PolicyId) REFERENCES dbo.F03EndpointGovernancePolicies(Id);

/* Existing Endpoint Device -> Equipment Asset relationship. */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_F03EndpointDevices_EquipmentAsset')
    ALTER TABLE dbo.F03EndpointDevices ADD CONSTRAINT FK_F03EndpointDevices_EquipmentAsset FOREIGN KEY (EquipmentAssetId) REFERENCES dbo.F03EquipmentAssets(Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03EndpointDevices_EquipmentAssetId' AND object_id = OBJECT_ID(N'dbo.F03EndpointDevices'))
    CREATE INDEX IX_F03EndpointDevices_EquipmentAssetId ON dbo.F03EndpointDevices(EquipmentAssetId);

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_F03EndpointGovernanceRequests_RequestType' AND parent_object_id = OBJECT_ID(N'dbo.F03EndpointGovernanceRequests'))
    ALTER TABLE dbo.F03EndpointGovernanceRequests DROP CONSTRAINT CK_F03EndpointGovernanceRequests_RequestType;
ALTER TABLE dbo.F03EndpointGovernanceRequests ADD CONSTRAINT CK_F03EndpointGovernanceRequests_RequestType CHECK (RequestType IN (1,2,3,4,5,6));

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03EndpointGovernancePolicies_ApprovalRequest' AND object_id = OBJECT_ID(N'dbo.F03EndpointGovernancePolicies'))
    CREATE INDEX IX_F03EndpointGovernancePolicies_ApprovalRequest ON dbo.F03EndpointGovernancePolicies(ApprovalRequestId, WorkflowStatus);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03EndpointGovernanceRequests_Policy' AND object_id = OBJECT_ID(N'dbo.F03EndpointGovernanceRequests'))
    CREATE INDEX IX_F03EndpointGovernanceRequests_Policy ON dbo.F03EndpointGovernanceRequests(PolicyId, PolicyVersion, RequestStatus);

COMMIT TRANSACTION;
GO
