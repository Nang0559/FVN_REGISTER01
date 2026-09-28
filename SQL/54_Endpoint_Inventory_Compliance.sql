/*
FVN_REGISTER - Endpoint Agent inventory foundation.

Canonical responsibility:
- F03EndpointDevices: technical endpoint identity + EquipmentAssetId reference.
- F03EndpointSoftwareInventory / F03EndpointServiceInventory: latest agent inventory.
- F03EndpointAlerts: technical identity/agent alerts.

Governance policy, approval, exception and compliance findings are defined by
54_EndpointGovernanceFoundation.sql and the existing Common Approval model.
The former F03SoftwarePolicies/F03WindowsServicePolicies/F03EndpointComplianceResults/
F03EndpointComplianceExceptions tables are intentionally not created here because
those responsibilities now belong to the canonical Endpoint Governance model.
*/

IF OBJECT_ID('dbo.F03EndpointDevices','U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EndpointDevices
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointDevices PRIMARY KEY,
        DeviceKey NVARCHAR(100) NOT NULL,
        ComputerName NVARCHAR(255) NULL,
        SerialNumber NVARCHAR(255) NULL,
        HardwareUuid NVARCHAR(255) NULL,
        AgentInstallationId NVARCHAR(100) NULL,
        OsName NVARCHAR(255) NULL,
        OsVersion NVARCHAR(100) NULL,
        EmployeeCode NVARCHAR(50) NULL,
        EquipmentAssetId INT NULL,
        AgentVersion NVARCHAR(50) NULL,
        LastSeenUtc DATETIME2(0) NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointDevices_Status DEFAULT('Unknown'),
        IdentityStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointDevices_IdentityStatus DEFAULT('PendingReview'),
        LastInventoryHash NVARCHAR(128) NULL,
        Source NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointDevices_Source DEFAULT('FVNAgent'),
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointDevices_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointDevices_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_F03EndpointDevices_DeviceKey UNIQUE(DeviceKey)
    );
END;
GO

/* Upgrade compatibility for databases created by the previous endpoint script. */
IF COL_LENGTH('dbo.F03EndpointDevices','HardwareUuid') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD HardwareUuid NVARCHAR(255) NULL;
IF COL_LENGTH('dbo.F03EndpointDevices','AgentInstallationId') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD AgentInstallationId NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.F03EndpointDevices','IdentityStatus') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD IdentityStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointDevices_IdentityStatus_Upgrade DEFAULT('PendingReview');
IF COL_LENGTH('dbo.F03EndpointDevices','LastInventoryHash') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD LastInventoryHash NVARCHAR(128) NULL;
GO

IF OBJECT_ID('dbo.F03EndpointSoftwareInventory','U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EndpointSoftwareInventory
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointSoftwareInventory PRIMARY KEY,
        EndpointDeviceId BIGINT NOT NULL,
        NormalizedName NVARCHAR(255) NOT NULL,
        DisplayName NVARCHAR(255) NULL,
        Publisher NVARCHAR(255) NULL,
        Version NVARCHAR(100) NULL,
        Architecture NVARCHAR(30) NULL,
        InstallDate DATE NULL,
        InstallLocation NVARCHAR(1000) NULL,
        DetectedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointSoftwareInventory_DetectedAtUtc DEFAULT SYSUTCDATETIME(),
        Source NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointSoftwareInventory_Source DEFAULT('FVNAgent'),
        CONSTRAINT FK_F03EndpointSoftwareInventory_Device FOREIGN KEY(EndpointDeviceId) REFERENCES dbo.F03EndpointDevices(Id)
    );
    CREATE INDEX IX_F03EndpointSoftwareInventory_Device_Name ON dbo.F03EndpointSoftwareInventory(EndpointDeviceId, NormalizedName);
END;
GO

IF OBJECT_ID('dbo.F03EndpointServiceInventory','U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EndpointServiceInventory
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointServiceInventory PRIMARY KEY,
        EndpointDeviceId BIGINT NOT NULL,
        ServiceName NVARCHAR(255) NOT NULL,
        DisplayName NVARCHAR(255) NULL,
        State NVARCHAR(30) NULL,
        StartMode NVARCHAR(30) NULL,
        BinaryPathHash NVARCHAR(128) NULL,
        DetectedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointServiceInventory_DetectedAtUtc DEFAULT SYSUTCDATETIME(),
        Source NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointServiceInventory_Source DEFAULT('FVNAgent'),
        CONSTRAINT FK_F03EndpointServiceInventory_Device FOREIGN KEY(EndpointDeviceId) REFERENCES dbo.F03EndpointDevices(Id)
    );
    CREATE INDEX IX_F03EndpointServiceInventory_Device_Name ON dbo.F03EndpointServiceInventory(EndpointDeviceId, ServiceName);
END;
GO

IF OBJECT_ID('dbo.F03EndpointIdentityHistory','U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EndpointIdentityHistory
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointIdentityHistory PRIMARY KEY,
        EndpointDeviceId BIGINT NOT NULL,
        OldComputerName NVARCHAR(255) NULL,
        NewComputerName NVARCHAR(255) NULL,
        OldSerialNumber NVARCHAR(255) NULL,
        NewSerialNumber NVARCHAR(255) NULL,
        OldHardwareUuid NVARCHAR(255) NULL,
        NewHardwareUuid NVARCHAR(255) NULL,
        AgentInstallationId NVARCHAR(100) NULL,
        ChangeType NVARCHAR(50) NOT NULL,
        ChangedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointIdentityHistory_ChangedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_F03EndpointIdentityHistory_Device FOREIGN KEY(EndpointDeviceId) REFERENCES dbo.F03EndpointDevices(Id)
    );
    CREATE INDEX IX_F03EndpointIdentityHistory_Device_Date ON dbo.F03EndpointIdentityHistory(EndpointDeviceId, ChangedAtUtc DESC);
END;
GO

IF OBJECT_ID('dbo.F03EndpointAlerts','U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EndpointAlerts
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointAlerts PRIMARY KEY,
        EndpointDeviceId BIGINT NOT NULL,
        AlertType NVARCHAR(50) NOT NULL,
        Severity NVARCHAR(20) NOT NULL,
        Title NVARCHAR(255) NOT NULL,
        Details NVARCHAR(2000) NULL,
        RelatedPolicyId INT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointAlerts_Status DEFAULT('Open'),
        FirstDetectedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointAlerts_FirstDetectedAtUtc DEFAULT SYSUTCDATETIME(),
        LastDetectedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointAlerts_LastDetectedAtUtc DEFAULT SYSUTCDATETIME(),
        ResolvedAtUtc DATETIME2(0) NULL,
        CONSTRAINT FK_F03EndpointAlerts_Device FOREIGN KEY(EndpointDeviceId) REFERENCES dbo.F03EndpointDevices(Id)
    );
    CREATE INDEX IX_F03EndpointAlerts_Status ON dbo.F03EndpointAlerts(Status, Severity, LastDetectedAtUtc);
END;
GO
