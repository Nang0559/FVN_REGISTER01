IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'ChecklistCompleted') IS NULL
    ALTER TABLE dbo.F03EndpointGovernancePolicies ADD ChecklistCompleted BIT NOT NULL CONSTRAINT DF_F03EndpointGovernancePolicies_ChecklistCompleted DEFAULT(0);

IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'ChecklistCompletedAtUtc') IS NULL
    ALTER TABLE dbo.F03EndpointGovernancePolicies ADD ChecklistCompletedAtUtc DATETIME2 NULL;

IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'ChecklistCompletedBy') IS NULL
    ALTER TABLE dbo.F03EndpointGovernancePolicies ADD ChecklistCompletedBy INT NULL;

IF COL_LENGTH(N'dbo.F03EndpointGovernancePolicies', N'ChecklistNote') IS NULL
    ALTER TABLE dbo.F03EndpointGovernancePolicies ADD ChecklistNote NVARCHAR(2000) NULL;
