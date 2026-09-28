IF OBJECT_ID(N'dbo.F03EndpointAntivirusInventory', N'U') IS NULL
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
        CONSTRAINT FK_F03EndpointAntivirusInventory_Endpoint FOREIGN KEY (EndpointDeviceId)
            REFERENCES dbo.F03EndpointDevices(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_F03EndpointAntivirusInventory_Endpoint_Product
        ON dbo.F03EndpointAntivirusInventory(EndpointDeviceId, ProductName);

    CREATE INDEX IX_F03EndpointAntivirusInventory_Endpoint_Definition
        ON dbo.F03EndpointAntivirusInventory(EndpointDeviceId, DefinitionUpdatedAtUtc);
END;
