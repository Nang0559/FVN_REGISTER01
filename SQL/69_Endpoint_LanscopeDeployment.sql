USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH('dbo.F03EndpointDevices','LanscopeClientId') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD LanscopeClientId NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.F03EndpointDevices','IpAddress') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD IpAddress NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.F03EndpointDevices','MacAddress') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD MacAddress NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.F03EndpointDevices','WindowsUser') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD WindowsUser NVARCHAR(255) NULL;
IF COL_LENGTH('dbo.F03EndpointDevices','DomainName') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD DomainName NVARCHAR(255) NULL;
IF COL_LENGTH('dbo.F03EndpointDevices','OrganizationalUnit') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD OrganizationalUnit NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.F03EndpointDevices','LanscopeGroup') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD LanscopeGroup NVARCHAR(255) NULL;
IF COL_LENGTH('dbo.F03EndpointDevices','Manufacturer') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD Manufacturer NVARCHAR(255) NULL;
IF COL_LENGTH('dbo.F03EndpointDevices','Model') IS NULL ALTER TABLE dbo.F03EndpointDevices ADD Model NVARCHAR(255) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EndpointDevices_LanscopeClientId' AND object_id=OBJECT_ID(N'dbo.F03EndpointDevices'))
 CREATE INDEX IX_F03EndpointDevices_LanscopeClientId ON dbo.F03EndpointDevices(LanscopeClientId);
GO
IF OBJECT_ID('dbo.F03EndpointDeployments','U') IS NULL
BEGIN
 CREATE TABLE dbo.F03EndpointDeployments(
  Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointDeployments PRIMARY KEY,
  DeploymentCode NVARCHAR(100) NOT NULL,
  PackageVersion NVARCHAR(50) NULL,
  Status NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointDeployments_Status DEFAULT('Draft'),
  CreatedBy INT NOT NULL,
  CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointDeployments_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
  ExpiresAtUtc DATETIME2(0) NULL,
  CONSTRAINT UQ_F03EndpointDeployments_Code UNIQUE(DeploymentCode)
 );
END;
GO
IF OBJECT_ID('dbo.F03EndpointDeploymentTargets','U') IS NULL
BEGIN
 CREATE TABLE dbo.F03EndpointDeploymentTargets(
  Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointDeploymentTargets PRIMARY KEY,
  DeploymentId INT NOT NULL,
  TargetKey NVARCHAR(100) NOT NULL,
  ClientId NVARCHAR(100) NULL, ComputerName NVARCHAR(255) NULL, IpAddress NVARCHAR(100) NULL, MacAddress NVARCHAR(100) NULL,
  SerialNumber NVARCHAR(255) NULL, WindowsUser NVARCHAR(255) NULL, DomainName NVARCHAR(255) NULL, OrganizationalUnit NVARCHAR(500) NULL,
  LanscopeGroup NVARCHAR(255) NULL, OsName NVARCHAR(255) NULL, Manufacturer NVARCHAR(255) NULL, Model NVARCHAR(255) NULL,
  Status NVARCHAR(30) NOT NULL CONSTRAINT DF_F03EndpointDeploymentTargets_Status DEFAULT('Pending'),
  EndpointDeviceId BIGINT NULL, CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointDeploymentTargets_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
  EnrolledAtUtc DATETIME2(0) NULL,
  CONSTRAINT FK_F03EndpointDeploymentTargets_Deployment FOREIGN KEY(DeploymentId) REFERENCES dbo.F03EndpointDeployments(Id)
 );
 CREATE UNIQUE INDEX UX_F03EndpointDeploymentTargets_Deployment_Target ON dbo.F03EndpointDeploymentTargets(DeploymentId,TargetKey);
 CREATE INDEX IX_F03EndpointDeploymentTargets_Client ON dbo.F03EndpointDeploymentTargets(ClientId);
END;
GO
IF OBJECT_ID('dbo.F03EndpointEnrollmentTokens','U') IS NULL
BEGIN
 CREATE TABLE dbo.F03EndpointEnrollmentTokens(
  Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EndpointEnrollmentTokens PRIMARY KEY,
  DeploymentId INT NOT NULL, TargetId INT NOT NULL, TokenHash NVARCHAR(128) NOT NULL,
  ExpiresAtUtc DATETIME2(0) NOT NULL, CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_F03EndpointEnrollmentTokens_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
  UsedAtUtc DATETIME2(0) NULL,
  CONSTRAINT FK_F03EndpointEnrollmentTokens_Deployment FOREIGN KEY(DeploymentId) REFERENCES dbo.F03EndpointDeployments(Id),
  CONSTRAINT FK_F03EndpointEnrollmentTokens_Target FOREIGN KEY(TargetId) REFERENCES dbo.F03EndpointDeploymentTargets(Id),
  CONSTRAINT UQ_F03EndpointEnrollmentTokens_Hash UNIQUE(TokenHash)
 );
 CREATE INDEX IX_F03EndpointEnrollmentTokens_Target ON dbo.F03EndpointEnrollmentTokens(TargetId,UsedAtUtc,ExpiresAtUtc);
END;
GO
IF OBJECT_ID('dbo.F03Functions','U') IS NOT NULL
BEGIN
 MERGE dbo.F03Functions AS t USING (VALUES
 (3125,N'Endpoint.LanscopeDeploymentManage',N'Endpoint.LanscopeDeploymentManage',N'Quản lý deployment và bootstrap LANSCOPE.',N'Endpoint',N'DeploymentManage',N'Global',3125),
 (3126,N'Endpoint.LanscopeDeploymentView',N'Endpoint.LanscopeDeploymentView',N'Xem trạng thái deployment LANSCOPE.',N'Endpoint',N'DeploymentView',N'Global',3126)
 ) s(FunctionCode,FunctionKey,FunctionName,Detail,ModuleCode,ActionCode,ScopeCode,DisplayOrder)
 ON t.FunctionCode=s.FunctionCode
 WHEN MATCHED THEN UPDATE SET FunctionKey=s.FunctionKey,FunctionName=s.FunctionName,Detail=s.Detail,ModuleCode=s.ModuleCode,ActionCode=s.ActionCode,ScopeCode=s.ScopeCode,DisplayOrder=s.DisplayOrder,IsActive=1
 WHEN NOT MATCHED THEN INSERT(FunctionKey,FunctionCode,FunctionName,Detail,ModuleCode,ActionCode,ScopeCode,DisplayOrder,IsActive,CreatedBy,CreatedAt)
 VALUES(s.FunctionKey,s.FunctionCode,s.FunctionName,s.Detail,s.ModuleCode,s.ActionCode,s.ScopeCode,s.DisplayOrder,1,0,GETDATE());
END;
GO
IF OBJECT_ID('dbo.F03SecurityFunctionRegistry','U') IS NOT NULL
 INSERT INTO dbo.F03SecurityFunctionRegistry(FunctionKey,FunctionCode,DefinitionName,ModuleCode,ActionCode,ScopeCode,LifecycleStatus,SourceType,DefinitionHash,FirstDiscoveredAt,LastSeenAt,IsIgnored)
 SELECT f.FunctionKey,f.FunctionCode,f.FunctionName,f.ModuleCode,f.ActionCode,f.ScopeCode,N'Active',N'SQL',CONVERT(varchar(128),HASHBYTES('SHA2_256',CONCAT(f.FunctionKey,N'|',f.FunctionCode,N'|',f.FunctionName)),2),GETDATE(),GETDATE(),0
 FROM dbo.F03Functions f WHERE f.FunctionCode IN(3125,3126)
 AND NOT EXISTS(SELECT 1 FROM dbo.F03SecurityFunctionRegistry r WHERE r.FunctionKey=f.FunctionKey);
GO
IF OBJECT_ID('dbo.F03Roles','U') IS NOT NULL AND OBJECT_ID('dbo.F03RoleFunctions','U') IS NOT NULL
 INSERT INTO dbo.F03RoleFunctions(IdRole,IdFunction,IsActive,CreatedBy,CreatedAt)
 SELECT r.Id,f.Id,1,0,GETDATE() FROM dbo.F03Roles r CROSS JOIN dbo.F03Functions f
 WHERE (r.RoleCode=1 OR r.RoleName=N'IT Endpoint Operator') AND f.FunctionCode IN(3125,3126)
 AND NOT EXISTS(SELECT 1 FROM dbo.F03RoleFunctions rf WHERE rf.IdRole=r.Id AND rf.IdFunction=f.Id);
GO

GO
/* Production schema gate: fail deployment instead of leaving a partially usable LANSCOPE feature. */
IF OBJECT_ID(N'dbo.F03EndpointDeployments',N'U') IS NULL
    THROW 51690, N'F03EndpointDeployments was not created.', 1;
IF OBJECT_ID(N'dbo.F03EndpointDeploymentTargets',N'U') IS NULL
    THROW 51691, N'F03EndpointDeploymentTargets was not created.', 1;
IF OBJECT_ID(N'dbo.F03EndpointEnrollmentTokens',N'U') IS NULL
    THROW 51692, N'F03EndpointEnrollmentTokens was not created.', 1;
IF COL_LENGTH(N'dbo.F03EndpointDevices',N'LanscopeClientId') IS NULL
    THROW 51693, N'F03EndpointDevices.LanscopeClientId is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=3125 AND IsActive=1)
    THROW 51694, N'LANSCOPE deployment manage capability 3125 is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=3126 AND IsActive=1)
    THROW 51695, N'LANSCOPE deployment view capability 3126 is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.F03EndpointEnrollmentTokens') AND name=N'IX_F03EndpointEnrollmentTokens_Target_Created')
    CREATE INDEX IX_F03EndpointEnrollmentTokens_Target_Created ON dbo.F03EndpointEnrollmentTokens(TargetId,CreatedAtUtc DESC,UsedAtUtc,ExpiresAtUtc);
PRINT N'Endpoint LANSCOPE deployment schema verified.';
GO
