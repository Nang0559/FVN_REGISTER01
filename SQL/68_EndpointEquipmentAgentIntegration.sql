/*
 FVN_REGISTER - Equipment is the business entry point for Endpoint Agent.
 Eligibility is explicit; OS identity is discovered by the installed agent.
*/
IF COL_LENGTH('dbo.F03EquipmentAssets','EndpointAgentEligible') IS NULL
    ALTER TABLE dbo.F03EquipmentAssets ADD EndpointAgentEligible bit NOT NULL CONSTRAINT DF_F03EquipmentAssets_EndpointAgentEligible DEFAULT(0);
GO
IF COL_LENGTH('dbo.F03EquipmentAssets','EndpointOsFamily') IS NULL
    ALTER TABLE dbo.F03EquipmentAssets ADD EndpointOsFamily nvarchar(30) NULL;
GO
IF COL_LENGTH('dbo.F03EquipmentRequests','EndpointAgentEligible') IS NULL
    ALTER TABLE dbo.F03EquipmentRequests ADD EndpointAgentEligible bit NOT NULL CONSTRAINT DF_F03EquipmentRequests_EndpointAgentEligible DEFAULT(0);
GO
IF COL_LENGTH('dbo.F03EquipmentRequests','EndpointOsFamily') IS NULL
    ALTER TABLE dbo.F03EquipmentRequests ADD EndpointOsFamily nvarchar(30) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.F03EquipmentAssets') AND name='IX_F03EquipmentAssets_EndpointAgentEligible')
    CREATE INDEX IX_F03EquipmentAssets_EndpointAgentEligible ON dbo.F03EquipmentAssets(EndpointAgentEligible, IsActive);
GO
