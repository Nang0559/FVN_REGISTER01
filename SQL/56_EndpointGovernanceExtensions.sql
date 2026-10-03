/*
===============================================================================
56 - Endpoint Governance extensions
Canonical SQL deployment owner migrated from Database/EndpointGovernance/001-002.
Runs after Endpoint governance foundation (54/55) and before verification.
===============================================================================
*/

IF OBJECT_ID(N'dbo.F03EndpointDevices', N'U') IS NOT NULL
AND OBJECT_ID(N'dbo.F03EndpointAntivirusInventory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EndpointAntivirusInventory
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointAntivirusInventory PRIMARY KEY,
        EndpointDeviceId BIGINT NOT NULL,
        ProductName NVARCHAR(255) NOT NULL,
        ProductVersion NVARCHAR(100) NULL,
        EngineVersion NVARCHAR(100) NULL,
        DefinitionVersion NVARCHAR(100) NULL,
        DefinitionUpdatedAtUtc DATETIME2 NULL,
        AntivirusEnabled BIT NULL,
        RealTimeProtectionEnabled BIT NULL,
        ProtectionStatus NVARCHAR(50) NULL,
        RunningMode NVARCHAR(50) NULL,
        Source NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointAntivirusInventory_Source DEFAULT(N'FVNAgent'),
        DetectedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_F03EndpointAntivirusInventory_DetectedAtUtc DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_F03EndpointAntivirusInventory_Endpoint FOREIGN KEY(EndpointDeviceId)
            REFERENCES dbo.F03EndpointDevices(Id) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'dbo.F03EndpointAntivirusInventory', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EndpointAntivirusInventory_Endpoint_Product' AND object_id=OBJECT_ID(N'dbo.F03EndpointAntivirusInventory'))
        CREATE INDEX IX_F03EndpointAntivirusInventory_Endpoint_Product ON dbo.F03EndpointAntivirusInventory(EndpointDeviceId,ProductName);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EndpointAntivirusInventory_Endpoint_Definition' AND object_id=OBJECT_ID(N'dbo.F03EndpointAntivirusInventory'))
        CREATE INDEX IX_F03EndpointAntivirusInventory_Endpoint_Definition ON dbo.F03EndpointAntivirusInventory(EndpointDeviceId,DefinitionUpdatedAtUtc);
END;
GO

IF OBJECT_ID(N'dbo.F03EndpointGovernancePolicies', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'ChecklistCompleted') IS NULL
        ALTER TABLE dbo.F03EndpointGovernancePolicies ADD ChecklistCompleted BIT NOT NULL CONSTRAINT DF_F03EndpointGovernancePolicies_ChecklistCompleted DEFAULT(0);
    IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'ChecklistCompletedAtUtc') IS NULL
        ALTER TABLE dbo.F03EndpointGovernancePolicies ADD ChecklistCompletedAtUtc DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'ChecklistCompletedBy') IS NULL
        ALTER TABLE dbo.F03EndpointGovernancePolicies ADD ChecklistCompletedBy INT NULL;
    IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'ChecklistNote') IS NULL
        ALTER TABLE dbo.F03EndpointGovernancePolicies ADD ChecklistNote NVARCHAR(2000) NULL;
END;
GO
