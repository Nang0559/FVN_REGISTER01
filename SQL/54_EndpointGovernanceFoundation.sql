/*
===============================================================================
FVN_REGISTER - Endpoint Governance foundation
===============================================================================
Canonical shared model for software/service policy, approval requests and
version-pinned compliance findings. Does not create a second approval engine
and does not extend the Equipment domain.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.F03EndpointGovernancePolicies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EndpointGovernancePolicies
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointGovernancePolicies PRIMARY KEY,
        PolicyCode NVARCHAR(100) NOT NULL,
        PolicyName NVARCHAR(200) NOT NULL,
        ItemType INT NOT NULL,
        TargetType INT NOT NULL,
        Version INT NOT NULL,
        IsPublished BIT NOT NULL CONSTRAINT DF_F03EndpointGovernancePolicies_IsPublished DEFAULT (0),
        EffectiveFromUtc DATETIME2(0) NOT NULL,
        EffectiveToUtc DATETIME2(0) NULL,
        Remark NVARCHAR(1000) NULL,
        IsActive BIT NULL CONSTRAINT DF_F03EndpointGovernancePolicies_IsActive DEFAULT (1),
        CreatedBy INT NOT NULL CONSTRAINT DF_F03EndpointGovernancePolicies_CreatedBy DEFAULT (0),
        LastModifiedSource NVARCHAR(100) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointGovernancePolicies_CreatedAt DEFAULT (GETDATE()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2(0) NULL,
        CONSTRAINT UQ_F03EndpointGovernancePolicies_CodeVersion UNIQUE (PolicyCode, Version),
        CONSTRAINT CK_F03EndpointGovernancePolicies_Version CHECK (Version > 0),
        CONSTRAINT CK_F03EndpointGovernancePolicies_ItemType CHECK (ItemType IN (1,2)),
        CONSTRAINT CK_F03EndpointGovernancePolicies_TargetType CHECK (TargetType IN (1,2,3))
    );
END;
GO

IF OBJECT_ID(N'dbo.F03EndpointGovernancePolicyItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EndpointGovernancePolicyItems
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointGovernancePolicyItems PRIMARY KEY,
        PolicyId INT NOT NULL,
        ItemType INT NOT NULL,
        NormalizedName NVARCHAR(255) NOT NULL,
        DisplayName NVARCHAR(255) NULL,
        Publisher NVARCHAR(255) NULL,
        VersionConstraint NVARCHAR(100) NULL,
        AliasNames NVARCHAR(2000) NULL,
        IsAllowed BIT NOT NULL CONSTRAINT DF_F03EndpointGovernancePolicyItems_IsAllowed DEFAULT (1),
        Remark NVARCHAR(1000) NULL,
        IsActive BIT NULL CONSTRAINT DF_F03EndpointGovernancePolicyItems_IsActive DEFAULT (1),
        CreatedBy INT NOT NULL CONSTRAINT DF_F03EndpointGovernancePolicyItems_CreatedBy DEFAULT (0),
        LastModifiedSource NVARCHAR(100) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointGovernancePolicyItems_CreatedAt DEFAULT (GETDATE()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2(0) NULL,
        CONSTRAINT FK_F03EndpointGovernancePolicyItems_Policy FOREIGN KEY (PolicyId) REFERENCES dbo.F03EndpointGovernancePolicies(Id),
        CONSTRAINT CK_F03EndpointGovernancePolicyItems_ItemType CHECK (ItemType IN (1,2)),
        CONSTRAINT UQ_F03EndpointGovernancePolicyItems_PolicyName UNIQUE (PolicyId, NormalizedName, ItemType)
    );
END;
GO

IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicyItems', N'AliasNames') IS NULL
    ALTER TABLE dbo.F03EndpointGovernancePolicyItems ADD AliasNames NVARCHAR(2000) NULL;
GO

IF OBJECT_ID(N'dbo.F03EndpointGovernanceRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EndpointGovernanceRequests
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointGovernanceRequests PRIMARY KEY,
        EmployeeCode NVARCHAR(50) NOT NULL,
        DeptCode NVARCHAR(20) NULL,
        RequestStatus INT NOT NULL CONSTRAINT DF_F03EndpointGovernanceRequests_RequestStatus DEFAULT (0),
        RequestType INT NOT NULL,
        EndpointDeviceId BIGINT NULL,
        PolicyId INT NULL,
        PolicyVersion INT NULL,
        PolicyItemId INT NULL,
        PolicyItemType INT NULL,
        ItemName NVARCHAR(255) NULL,
        Publisher NVARCHAR(255) NULL,
        RequestedVersion NVARCHAR(100) NULL,
        Reason NVARCHAR(2000) NOT NULL,
        SecurityReviewStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointGovernanceRequests_SecurityReview DEFAULT ('Pending'),
        WorkflowStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointGovernanceRequests_Workflow DEFAULT ('Draft'),
        ApprovalSnapshotId INT NULL,
        ReviewedAtUtc DATETIME2(0) NULL,
        ReviewedBy INT NULL,
        IsActive BIT NULL CONSTRAINT DF_F03EndpointGovernanceRequests_IsActive DEFAULT (1),
        CreatedBy INT NOT NULL CONSTRAINT DF_F03EndpointGovernanceRequests_CreatedBy DEFAULT (0),
        LastModifiedSource NVARCHAR(100) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointGovernanceRequests_CreatedAt DEFAULT (GETDATE()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2(0) NULL,
        CONSTRAINT FK_F03EndpointGovernanceRequests_Device FOREIGN KEY (EndpointDeviceId) REFERENCES dbo.F03EndpointDevices(Id),
        CONSTRAINT FK_F03EndpointGovernanceRequests_Policy FOREIGN KEY (PolicyId) REFERENCES dbo.F03EndpointGovernancePolicies(Id),
        CONSTRAINT FK_F03EndpointGovernanceRequests_PolicyItem FOREIGN KEY (PolicyItemId) REFERENCES dbo.F03EndpointGovernancePolicyItems(Id),
        CONSTRAINT FK_F03EndpointGovernanceRequests_ApprovalSnapshot FOREIGN KEY (ApprovalSnapshotId) REFERENCES dbo.F03ApprovalSnapshots(Id),
        CONSTRAINT CK_F03EndpointGovernanceRequests_RequestType CHECK (RequestType IN (1,2,3,4,5,6))
    );
END;
GO

IF OBJECT_ID(N'dbo.F03EndpointComplianceFindings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EndpointComplianceFindings
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointComplianceFindings PRIMARY KEY,
        EndpointDeviceId BIGINT NOT NULL,
        PolicyId INT NULL,
        PolicyVersion INT NOT NULL,
        PolicyItemId INT NULL,
        ItemType INT NOT NULL,
        InventoryItemId BIGINT NULL,
        Result INT NOT NULL,
        ObservedName NVARCHAR(255) NOT NULL,
        ObservedVersion NVARCHAR(100) NULL,
        FindingCode NVARCHAR(50) NOT NULL,
        FindingMessage NVARCHAR(2000) NULL,
        EvaluatedAtUtc DATETIME2(0) NOT NULL,
        ResolvedAtUtc DATETIME2(0) NULL,
        IsActive BIT NULL CONSTRAINT DF_F03EndpointComplianceFindings_IsActive DEFAULT (1),
        CreatedBy INT NOT NULL CONSTRAINT DF_F03EndpointComplianceFindings_CreatedBy DEFAULT (0),
        LastModifiedSource NVARCHAR(100) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointComplianceFindings_CreatedAt DEFAULT (GETDATE()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2(0) NULL,
        CONSTRAINT FK_F03EndpointComplianceFindings_Device FOREIGN KEY (EndpointDeviceId) REFERENCES dbo.F03EndpointDevices(Id),
        CONSTRAINT FK_F03EndpointComplianceFindings_Policy FOREIGN KEY (PolicyId) REFERENCES dbo.F03EndpointGovernancePolicies(Id),
        CONSTRAINT FK_F03EndpointComplianceFindings_PolicyItem FOREIGN KEY (PolicyItemId) REFERENCES dbo.F03EndpointGovernancePolicyItems(Id),
        CONSTRAINT CK_F03EndpointComplianceFindings_Result CHECK (Result IN (1,2,3,4)),
        CONSTRAINT CK_F03EndpointComplianceFindings_ItemType CHECK (ItemType IN (1,2))
    );
END;
GO

IF COL_LENGTH(N'dbo.F03EndpointGovernanceRequests', N'PolicyId') IS NULL
    ALTER TABLE dbo.F03EndpointGovernanceRequests ADD PolicyId INT NULL;
IF COL_LENGTH(N'dbo.F03EndpointGovernanceRequests', N'PolicyVersion') IS NULL
    ALTER TABLE dbo.F03EndpointGovernanceRequests ADD PolicyVersion INT NULL;
IF COL_LENGTH(N'dbo.F03EndpointGovernanceRequests', N'PolicyItemType') IS NULL
    ALTER TABLE dbo.F03EndpointGovernanceRequests ADD PolicyItemType INT NULL;
IF COL_LENGTH(N'dbo.F03EndpointComplianceFindings', N'PolicyId') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.F03EndpointComplianceFindings') AND name = N'PolicyId' AND is_nullable = 0)
        ALTER TABLE dbo.F03EndpointComplianceFindings ALTER COLUMN PolicyId INT NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_F03EndpointGovernanceRequests_Policy' AND parent_object_id = OBJECT_ID(N'dbo.F03EndpointGovernanceRequests'))
    ALTER TABLE dbo.F03EndpointGovernanceRequests ADD CONSTRAINT FK_F03EndpointGovernanceRequests_Policy FOREIGN KEY (PolicyId) REFERENCES dbo.F03EndpointGovernancePolicies(Id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03EndpointGovernancePolicies_Published' AND object_id = OBJECT_ID(N'dbo.F03EndpointGovernancePolicies'))
    CREATE INDEX IX_F03EndpointGovernancePolicies_Published ON dbo.F03EndpointGovernancePolicies(ItemType, TargetType, IsPublished, EffectiveFromUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03EndpointGovernanceRequests_DeviceStatus' AND object_id = OBJECT_ID(N'dbo.F03EndpointGovernanceRequests'))
    CREATE INDEX IX_F03EndpointGovernanceRequests_DeviceStatus ON dbo.F03EndpointGovernanceRequests(EndpointDeviceId, WorkflowStatus, RequestStatus);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03EndpointComplianceFindings_DevicePolicy' AND object_id = OBJECT_ID(N'dbo.F03EndpointComplianceFindings'))
    CREATE INDEX IX_F03EndpointComplianceFindings_DevicePolicy ON dbo.F03EndpointComplianceFindings(EndpointDeviceId, PolicyId, PolicyVersion, EvaluatedAtUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03EndpointGovernancePolicyItems_Alias' AND object_id = OBJECT_ID(N'dbo.F03EndpointGovernancePolicyItems'))
    CREATE INDEX IX_F03EndpointGovernancePolicyItems_Alias ON dbo.F03EndpointGovernancePolicyItems(PolicyId, ItemType, IsActive);

COMMIT TRANSACTION;
GO
