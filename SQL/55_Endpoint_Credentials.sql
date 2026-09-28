/* Per-device credentials. Store only a SHA-256 hash of the secret. */
IF OBJECT_ID('dbo.F03EndpointCredentials','U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EndpointCredentials
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointCredentials PRIMARY KEY,
        EndpointDeviceId BIGINT NOT NULL,
        SecretHash VARBINARY(32) NOT NULL,
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointCredentials_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        ExpiresAtUtc DATETIME2(0) NOT NULL,
        RevokedAtUtc DATETIME2(0) NULL,
        CreatedBy INT NULL,
        CONSTRAINT FK_F03EndpointCredentials_Device FOREIGN KEY(EndpointDeviceId) REFERENCES dbo.F03EndpointDevices(Id)
    );
    CREATE UNIQUE INDEX UX_F03EndpointCredentials_ActiveDevice
        ON dbo.F03EndpointCredentials(EndpointDeviceId)
        WHERE RevokedAtUtc IS NULL;
    CREATE INDEX IX_F03EndpointCredentials_Hash ON dbo.F03EndpointCredentials(SecretHash);
END;
GO
