/*
FVN_REGISTER - FEATURE OPERATOR ASSIGNMENTS
Canonical schema for F03FeatureOperatorAssignments. Idempotent.
*/
IF OBJECT_ID(N'dbo.F03FeatureOperatorAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03FeatureOperatorAssignments
    (
        Id int IDENTITY(1,1) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_F03FeatureOperatorAssignments_IsActive DEFAULT(1),
        CreatedBy int NULL,
        LastModifiedSource nvarchar(100) NULL,
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_F03FeatureOperatorAssignments_CreatedAt DEFAULT(SYSUTCDATETIME()),
        ModifiedBy int NULL,
        ModifiedAt datetime2(0) NULL,
        EmployeeCode nvarchar(50) NOT NULL,
        FunctionCode int NOT NULL,
        ResourceType nvarchar(50) NOT NULL,
        ResourceId int NULL,
        Remark nvarchar(1000) NULL,
        CONSTRAINT PK_F03FeatureOperatorAssignments PRIMARY KEY CLUSTERED(Id)
    );
END;
GO
/* Data scope of an assignment-granted capability (Execution.Review): All / Department / Own. NULL = function default. */
IF COL_LENGTH(N'dbo.F03FeatureOperatorAssignments', N'ScopeCode') IS NULL
    ALTER TABLE dbo.F03FeatureOperatorAssignments ADD ScopeCode nvarchar(30) NULL;
GO
/* Keep databases created by older revisions compatible with the current Contract/Core model. */
IF COL_LENGTH(N'dbo.F03FeatureOperatorAssignments', N'Remark') IS NOT NULL
   AND EXISTS
   (
       SELECT 1
       FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.F03FeatureOperatorAssignments')
         AND name = N'Remark'
         AND max_length < 2000
   )
BEGIN
    ALTER TABLE dbo.F03FeatureOperatorAssignments ALTER COLUMN Remark nvarchar(1000) NULL;
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.F03FeatureOperatorAssignments') AND name=N'IX_F03FeatureOperatorAssignments_FunctionResource')
    CREATE INDEX IX_F03FeatureOperatorAssignments_FunctionResource ON dbo.F03FeatureOperatorAssignments(FunctionCode,ResourceType,ResourceId,IsActive,EmployeeCode);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.F03FeatureOperatorAssignments') AND name=N'IX_F03FeatureOperatorAssignments_EmployeeCode')
    CREATE INDEX IX_F03FeatureOperatorAssignments_EmployeeCode ON dbo.F03FeatureOperatorAssignments(EmployeeCode,IsActive);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.F03FeatureOperatorAssignments') AND name=N'UX_F03FeatureOperatorAssignments_Active')
    CREATE UNIQUE INDEX UX_F03FeatureOperatorAssignments_Active ON dbo.F03FeatureOperatorAssignments(EmployeeCode,FunctionCode,ResourceType,ResourceId) WHERE IsActive=1;
GO
