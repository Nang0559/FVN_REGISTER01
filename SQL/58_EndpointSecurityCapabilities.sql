USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
===============================================================================
Endpoint capability registry

Security model:
- F03Functions remains the single capability/function catalog.
- Endpoint Governance does NOT create a second RBAC system.
- SuperAdmin keeps the administrative baseline.
- IT receives a dedicated role for Endpoint Governance.
- Endpoint function assignment is managed through F03RoleFunctions /
  Security Center.
- Existing active F03Users in HRM department IT are provisioned with the IT
  Endpoint Operator role; this is idempotent and does not remove other roles.

Role-code policy:
- Do NOT assume RoleCode 7 is free. Existing installations may already use it.
- Reuse an existing role named 'IT Endpoint Operator' when present.
- Otherwise allocate the next unused positive RoleCode.
- The role's database Id, not a hard-coded RoleCode, is used for assignments.
===============================================================================
*/

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 51580, N'F03Functions is required before applying Endpoint capabilities.', 1;
IF OBJECT_ID(N'dbo.F03Roles', N'U') IS NULL
    THROW 51582, N'F03Roles is required before applying Endpoint capabilities.', 1;
IF OBJECT_ID(N'dbo.F03RoleFunctions', N'U') IS NULL
    THROW 51583, N'F03RoleFunctions is required before applying Endpoint capabilities.', 1;
IF OBJECT_ID(N'dbo.F03UserRoles', N'U') IS NULL
    THROW 51585, N'F03UserRoles is required before applying Endpoint capabilities.', 1;

DECLARE @Functions TABLE
(
    FunctionKey NVARCHAR(150) NOT NULL,
    FunctionCode INT NOT NULL,
    FunctionName NVARCHAR(150) NOT NULL,
    Detail NVARCHAR(500) NOT NULL,
    ActionCode NVARCHAR(50) NOT NULL,
    ScopeCode NVARCHAR(30) NOT NULL,
    DisplayOrder INT NOT NULL
);

INSERT INTO @Functions VALUES
(N'Endpoint.View',3101,N'Endpoint.View',N'Xem module Endpoint',N'View',N'Global',3101),
(N'Endpoint.InventoryView',3102,N'Endpoint.InventoryView',N'Xem inventory từ Endpoint Agent',N'View',N'Global',3102),
(N'Endpoint.ComplianceView',3103,N'Endpoint.ComplianceView',N'Xem kết quả đối chiếu Compliance',N'View',N'Global',3103),
(N'Endpoint.AlertView',3104,N'Endpoint.AlertView',N'Xem cảnh báo Endpoint',N'View',N'Global',3104),
(N'Endpoint.AlertResolve',3105,N'Endpoint.AlertResolve',N'Xử lý cảnh báo Endpoint',N'Resolve',N'Global',3105),
(N'Endpoint.CredentialProvision',3106,N'Endpoint.CredentialProvision',N'Cấp credential cho Endpoint Agent',N'Provision',N'Global',3106),
(N'Endpoint.CredentialRotate',3107,N'Endpoint.CredentialRotate',N'Rotate credential Endpoint Agent',N'Rotate',N'Global',3107),
(N'Endpoint.CredentialRevoke',3108,N'Endpoint.CredentialRevoke',N'Thu hồi credential Endpoint Agent',N'Revoke',N'Global',3108),
(N'Endpoint.SoftwareCatalogView',3109,N'Endpoint.SoftwareCatalogView',N'Xem Software Catalog',N'View',N'Global',3109),
(N'Endpoint.SoftwareCatalogManage',3110,N'Endpoint.SoftwareCatalogManage',N'Tạo/sửa/import Software Catalog draft',N'Manage',N'Global',3110),
(N'Endpoint.SoftwarePolicySubmit',3111,N'Endpoint.SoftwarePolicySubmit',N'Submit Software Catalog qua Common Approval',N'Submit',N'Global',3111),
(N'Endpoint.SoftwarePolicyApprove',3112,N'Endpoint.SoftwarePolicyApprove',N'Publish Software Catalog sau khi approved',N'Approve',N'Global',3112),
(N'Endpoint.SoftwareSecurityReview',3113,N'Endpoint.SoftwareSecurityReview',N'Security Review Software request',N'SecurityReview',N'Global',3113),
(N'Endpoint.ServiceCatalogView',3114,N'Endpoint.ServiceCatalogView',N'Xem Windows Service Catalog',N'View',N'Global',3114),
(N'Endpoint.ServiceCatalogManage',3115,N'Endpoint.ServiceCatalogManage',N'Tạo/sửa/import Windows Service Catalog draft',N'Manage',N'Global',3115),
(N'Endpoint.ServicePolicySubmit',3116,N'Endpoint.ServicePolicySubmit',N'Submit Windows Service Catalog qua Common Approval',N'Submit',N'Global',3116),
(N'Endpoint.ServicePolicyApprove',3117,N'Endpoint.ServicePolicyApprove',N'Publish Windows Service Catalog sau khi approved',N'Approve',N'Global',3117),
(N'Endpoint.ServiceSecurityReview',3118,N'Endpoint.ServiceSecurityReview',N'Security Review Service request',N'SecurityReview',N'Global',3118),
(N'Endpoint.InstallRequestCreate',3119,N'Endpoint.InstallRequestCreate',N'Tạo yêu cầu cài Software',N'Create',N'Own',3119),
(N'Endpoint.InstallRequestView',3120,N'Endpoint.InstallRequestView',N'Xem Installation Request',N'View',N'Global',3120),
(N'Endpoint.InstallRequestApprove',3121,N'Endpoint.InstallRequestApprove',N'Phê duyệt Installation Request',N'Approve',N'Global',3121),
(N'Endpoint.ExceptionCreate',3122,N'Endpoint.ExceptionCreate',N'Tạo Compliance Exception',N'Create',N'Own',3122),
(N'Endpoint.ExceptionView',3123,N'Endpoint.ExceptionView',N'Xem Compliance Exception',N'View',N'Global',3123),
(N'Endpoint.ExceptionApprove',3124,N'Endpoint.ExceptionApprove',N'Phê duyệt Compliance Exception',N'Approve',N'Global',3124);

MERGE dbo.F03Functions AS target
USING @Functions AS source ON target.FunctionCode = source.FunctionCode
WHEN MATCHED THEN UPDATE SET
    FunctionKey = source.FunctionKey,
    FunctionName = source.FunctionName,
    Detail = source.Detail,
    ModuleCode = N'Endpoint',
    ActionCode = source.ActionCode,
    ScopeCode = source.ScopeCode,
    DisplayOrder = source.DisplayOrder,
    IsActive = 1
WHEN NOT MATCHED THEN INSERT
(
    FunctionKey, FunctionCode, FunctionName, Detail, ModuleCode, ActionCode,
    ScopeCode, DisplayOrder, IsActive, CreatedBy, CreatedAt
)
VALUES
(
    source.FunctionKey, source.FunctionCode, source.FunctionName, source.Detail,
    N'Endpoint', source.ActionCode, source.ScopeCode, source.DisplayOrder, 1, 0, GETDATE()
);
GO

/* Stable registry entries. */
INSERT INTO dbo.F03SecurityFunctionRegistry
(
    FunctionKey, FunctionCode, DefinitionName, ModuleCode, ActionCode, ScopeCode,
    LifecycleStatus, SourceType, DefinitionHash, FirstDiscoveredAt, LastSeenAt, IsIgnored
)
SELECT f.FunctionKey, f.FunctionCode, f.FunctionName, f.ModuleCode, f.ActionCode, f.ScopeCode,
       N'Active', N'Code',
       CONVERT(varchar(128), HASHBYTES('SHA2_256', CONCAT(f.FunctionKey, N'|', f.FunctionCode, N'|', f.FunctionName)), 2),
       GETDATE(), GETDATE(), 0
FROM dbo.F03Functions f
WHERE f.ModuleCode = N'Endpoint'
  AND f.FunctionCode BETWEEN 3101 AND 3124
  AND NOT EXISTS (SELECT 1 FROM dbo.F03SecurityFunctionRegistry r WHERE r.FunctionKey = f.FunctionKey);
GO

/*
Dedicated IT role for Endpoint Governance.
Never fail merely because an existing installation already uses RoleCode 7.
The stable identity of this role is its RoleName; RoleCode is only a legacy
numeric identifier and must remain unique.
*/
DECLARE @EndpointRoleId INT;
DECLARE @EndpointRoleCode INT;

SELECT TOP (1)
    @EndpointRoleId = Id,
    @EndpointRoleCode = RoleCode
FROM dbo.F03Roles
WHERE RoleName = N'IT Endpoint Operator'
ORDER BY Id;

IF @EndpointRoleId IS NULL
BEGIN
    SELECT @EndpointRoleCode = ISNULL(MAX(RoleCode), 0) + 1
    FROM dbo.F03Roles;

    WHILE EXISTS (SELECT 1 FROM dbo.F03Roles WHERE RoleCode = @EndpointRoleCode)
        SET @EndpointRoleCode = @EndpointRoleCode + 1;

    INSERT INTO dbo.F03Roles
    (
        RoleCode, RoleName, Detail, IsSystem, IsActive, CreatedBy, CreatedAt
    )
    VALUES
    (
        @EndpointRoleCode,
        N'IT Endpoint Operator',
        N'Quản trị Endpoint Governance: inventory, compliance, software/service catalog, installation request và exception theo Security Center.',
        0,
        1,
        0,
        GETDATE()
    );

    SET @EndpointRoleId = CONVERT(INT, SCOPE_IDENTITY());
END;

IF @EndpointRoleId IS NULL
    THROW 51584, N'Could not resolve the IT Endpoint Operator role.', 1;

/* Initial baseline for SuperAdmin and IT Endpoint Operator. */
INSERT INTO dbo.F03RoleFunctions (IdRole, IdFunction, IsActive, CreatedBy, CreatedAt)
SELECT r.Id, f.Id, 1, 0, GETDATE()
FROM dbo.F03Roles r
JOIN dbo.F03Functions f
  ON f.FunctionCode BETWEEN 3101 AND 3124
 AND f.ModuleCode = N'Endpoint'
 AND f.IsActive = 1
WHERE r.IsActive = 1
  AND (r.RoleCode = 1 OR r.Id = @EndpointRoleId)
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.F03RoleFunctions rf
      WHERE rf.IdRole = r.Id
        AND rf.IdFunction = f.Id
  );
GO

/* Provision the dedicated Endpoint role to existing active HRM-IT users. */
DECLARE @EndpointRoleId2 INT;
SELECT TOP (1) @EndpointRoleId2 = Id
FROM dbo.F03Roles
WHERE RoleName = N'IT Endpoint Operator'
ORDER BY Id;

INSERT INTO dbo.F03UserRoles
(
    IdUser, IdRole, IsPrimary, IsActive, CreatedBy, CreatedAt
)
SELECT u.Id, @EndpointRoleId2, 0, 1, 0, GETDATE()
FROM dbo.F03Users u
WHERE @EndpointRoleId2 IS NOT NULL
  AND u.IsActive = 1
  AND u.DeptCode = 57  /* HRM BPMa 57 = IT */
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.F03UserRoles ur
      WHERE ur.IdUser = u.Id
        AND ur.IdRole = @EndpointRoleId2
  );
GO

IF EXISTS
(
    SELECT 1 FROM dbo.F03Functions
    WHERE FunctionCode BETWEEN 3101 AND 3124
      AND ModuleCode = N'Endpoint'
      AND IsActive <> 1
)
    THROW 51581, N'Endpoint capability registry is incomplete.', 1;
GO
