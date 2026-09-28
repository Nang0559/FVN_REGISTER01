USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

/*
  FVN_REGISTER - Security Managed Data Scopes
  ---------------------------------------------------------------------------
  F03ManagedScopes is the FVN-owned data-scope catalog consumed by
  AuthorizationService. It grants DATA SCOPE only; it never grants a
  capability by itself. Capability/RoleFunction checks remain mandatory.

  Supported NodeType values:
    Company | Factory | Department | SubDepartment

  HRM remains source-of-truth for organization/employee masters. This table
  stores only the security manager's scope assignment.
*/

IF OBJECT_ID(N'dbo.F03ManagedScopes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ManagedScopes
    (
        Id int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_F03ManagedScopes PRIMARY KEY,
        IsActive bit NULL
            CONSTRAINT DF_F03ManagedScopes_IsActive DEFAULT(1),
        CreatedBy int NOT NULL
            CONSTRAINT DF_F03ManagedScopes_CreatedBy DEFAULT(0),
        LastModifiedSource nvarchar(100) NULL,
        CreatedAt datetime2(0) NOT NULL
            CONSTRAINT DF_F03ManagedScopes_CreatedAt DEFAULT(GETDATE()),
        ModifiedBy int NULL,
        ModifiedAt datetime2(0) NULL,

        EmployeeCode nvarchar(50) NOT NULL,
        NodeType nvarchar(30) NOT NULL,
        NodeCode nvarchar(100) NULL,
        FactoryCode nvarchar(100) NULL,
        DeptCode nvarchar(100) NULL,
        SubDepartmentCode nvarchar(100) NULL,
        IncludeChildren bit NOT NULL
            CONSTRAINT DF_F03ManagedScopes_IncludeChildren DEFAULT(0),
        Remark nvarchar(500) NULL
    );
END;
GO

/* Idempotent upgrade for databases that already contain a partial table. */
IF OBJECT_ID(N'dbo.F03ManagedScopes', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'IsActive') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD IsActive bit NULL
            CONSTRAINT DF_F03ManagedScopes_IsActive DEFAULT(1);

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'CreatedBy') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD CreatedBy int NOT NULL
            CONSTRAINT DF_F03ManagedScopes_CreatedBy DEFAULT(0);

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'LastModifiedSource') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD LastModifiedSource nvarchar(100) NULL;

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'CreatedAt') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD CreatedAt datetime2(0) NOT NULL
            CONSTRAINT DF_F03ManagedScopes_CreatedAt DEFAULT(GETDATE());

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'ModifiedBy') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD ModifiedBy int NULL;

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'ModifiedAt') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD ModifiedAt datetime2(0) NULL;

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'EmployeeCode') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD EmployeeCode nvarchar(50) NULL;

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'NodeType') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD NodeType nvarchar(30) NULL;

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'NodeCode') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD NodeCode nvarchar(100) NULL;

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'FactoryCode') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD FactoryCode nvarchar(100) NULL;

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'DeptCode') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD DeptCode nvarchar(100) NULL;

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'SubDepartmentCode') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD SubDepartmentCode nvarchar(100) NULL;

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'IncludeChildren') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD IncludeChildren bit NOT NULL
            CONSTRAINT DF_F03ManagedScopes_IncludeChildren DEFAULT(0);

    IF COL_LENGTH(N'dbo.F03ManagedScopes',N'Remark') IS NULL
        ALTER TABLE dbo.F03ManagedScopes ADD Remark nvarchar(500) NULL;
END;
GO

/* Canonicalize old/partial rows before enforcing the required fields. */
UPDATE dbo.F03ManagedScopes
SET IsActive = ISNULL(IsActive,1),
    CreatedBy = ISNULL(CreatedBy,0),
    CreatedAt = ISNULL(CreatedAt,GETDATE()),
    IncludeChildren = ISNULL(IncludeChildren,0)
WHERE IsActive IS NULL
   OR CreatedAt IS NULL
   OR IncludeChildren IS NULL;
GO

/* Prevent invalid security-scope node types. */
IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_F03ManagedScopes_NodeType'
      AND parent_object_id = OBJECT_ID(N'dbo.F03ManagedScopes')
)
BEGIN
    ALTER TABLE dbo.F03ManagedScopes
    ADD CONSTRAINT CK_F03ManagedScopes_NodeType
    CHECK (NodeType IN (N'Company',N'Factory',N'Department',N'SubDepartment'));
END;
GO

/* One active assignment may not be duplicated. Nullable node columns are
   normalized through a persisted computed key so SQL Server can enforce
   uniqueness consistently for all node types. */
IF COL_LENGTH(N'dbo.F03ManagedScopes',N'ScopeKey') IS NULL
BEGIN
    ALTER TABLE dbo.F03ManagedScopes
    ADD ScopeKey AS
    (
        UPPER(LTRIM(RTRIM(ISNULL(EmployeeCode,N'')))) + N'|' +
        UPPER(LTRIM(RTRIM(ISNULL(NodeType,N'')))) + N'|' +
        UPPER(LTRIM(RTRIM(ISNULL(NodeCode,N'')))) + N'|' +
        UPPER(LTRIM(RTRIM(ISNULL(FactoryCode,N'')))) + N'|' +
        UPPER(LTRIM(RTRIM(ISNULL(DeptCode,N'')))) + N'|' +
        UPPER(LTRIM(RTRIM(ISNULL(SubDepartmentCode,N''))))
    ) PERSISTED;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_F03ManagedScopes_ActiveScope'
      AND object_id = OBJECT_ID(N'dbo.F03ManagedScopes')
)
BEGIN
    CREATE UNIQUE INDEX UX_F03ManagedScopes_ActiveScope
        ON dbo.F03ManagedScopes(ScopeKey)
        WHERE IsActive = 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_F03ManagedScopes_Employee'
      AND object_id = OBJECT_ID(N'dbo.F03ManagedScopes')
)
BEGIN
    CREATE INDEX IX_F03ManagedScopes_Employee
        ON dbo.F03ManagedScopes(EmployeeCode, IsActive, NodeType);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_F03ManagedScopes_Dept'
      AND object_id = OBJECT_ID(N'dbo.F03ManagedScopes')
)
BEGIN
    CREATE INDEX IX_F03ManagedScopes_Dept
        ON dbo.F03ManagedScopes(DeptCode, SubDepartmentCode, IsActive);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_F03ManagedScopes_Node'
      AND object_id = OBJECT_ID(N'dbo.F03ManagedScopes')
)
BEGIN
    CREATE INDEX IX_F03ManagedScopes_Node
        ON dbo.F03ManagedScopes(NodeType, NodeCode, FactoryCode, IsActive);
END;
GO

PRINT N'F03ManagedScopes schema is ready.';
GO
