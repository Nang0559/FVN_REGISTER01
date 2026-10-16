USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/*
  Security/Admin compatibility patch
  ----------------------------------
  The application now resolves authorization through F03Roles/F03UserRoles/
  F03RoleFunctions, while older installations may still have only
  F03Users.PermissionCode populated.

  This patch bridges the legacy Admin/SuperAdmin identities to the canonical
  RBAC model. It does NOT grant permissions to ordinary users and does not
  change any existing non-admin RoleFunction assignments.
*/

/* Ensure the two built-in administrative roles exist. */
IF OBJECT_ID(N'dbo.F03Roles', N'U') IS NULL
    THROW 51510, N'F03Roles chưa tồn tại. Chạy migration security/RBAC trước.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.F03Roles WHERE RoleCode = 1)
BEGIN
    INSERT INTO dbo.F03Roles
    (
        IsActive, CreatedBy, LastModifiedSource, CreatedAt,
        RoleCode, RoleName, Detail, IsSystem
    )
    VALUES
    (
        1, 0, N'SECURITY_ADMIN_COMPATIBILITY', GETDATE(),
        1, N'SuperAdmin', N'Built-in SuperAdmin compatibility role.', 1
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.F03Roles WHERE RoleCode = 2)
BEGIN
    INSERT INTO dbo.F03Roles
    (
        IsActive, CreatedBy, LastModifiedSource, CreatedAt,
        RoleCode, RoleName, Detail, IsSystem
    )
    VALUES
    (
        1, 0, N'SECURITY_ADMIN_COMPATIBILITY', GETDATE(),
        2, N'Admin', N'Built-in Admin compatibility role.', 1
    );
END;
GO

/*
  Security Center + account-management capabilities required by the Admin UI.
  2401 is the endpoint used to load the account list shown in Security Center.
*/
;WITH RequiredFunctions AS
(
    SELECT FunctionCode
    FROM (VALUES
        (2401), -- UserManagement.View
        (2402), -- UserManagement.Create
        (2403), -- UserManagement.Edit
        (2404), -- UserManagement.Lock
        (2405), -- UserManagement.ResetPassword
        (2406), -- UserManagement.AssignPermission
        (2601), -- Security.View
        (2602), -- Security.ManageRoles
        (2603), -- Security.ManageFunctions
        (2604)  -- Security.Audit
    ) AS v(FunctionCode)
)
INSERT INTO dbo.F03RoleFunctions
(
    IsActive, CreatedBy, LastModifiedSource, CreatedAt,
    IdRole, IdFunction
)
SELECT
    1,
    0,
    N'SECURITY_ADMIN_COMPATIBILITY',
    GETDATE(),
    r.Id,
    f.Id
FROM dbo.F03Roles AS r
CROSS JOIN dbo.F03Functions AS f
INNER JOIN RequiredFunctions AS rf
    ON rf.FunctionCode = f.FunctionCode
WHERE r.RoleCode IN (1, 2)
  AND r.IsActive = 1
  AND (f.IsActive = 1 OR f.IsActive IS NULL)
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.F03RoleFunctions AS existing
      WHERE existing.IdRole = r.Id
        AND existing.IdFunction = f.Id
  );
GO

/*
  Bridge legacy PermissionCode -> F03UserRoles.
  GetSnapshotAsync intentionally reads functions from F03UserRoles, so merely
  adding RoleFunction rows is insufficient for databases that predate the role
  migration.
*/
IF OBJECT_ID(N'dbo.F03UserRoles', N'U') IS NULL
    THROW 51511, N'F03UserRoles chưa tồn tại. Chạy migration security/RBAC trước.', 1;

INSERT INTO dbo.F03UserRoles
(
    IsActive, CreatedBy, LastModifiedSource, CreatedAt,
    IdUser, IdRole, IsPrimary
)
SELECT
    1,
    0,
    N'SECURITY_ADMIN_COMPATIBILITY',
    GETDATE(),
    u.Id,
    r.Id,
    1
FROM dbo.F03Users AS u
INNER JOIN dbo.F03Roles AS r
    ON r.RoleCode = u.PermissionCode
WHERE u.IsActive = 1
  AND u.PermissionCode IN (1, 2)
  AND r.IsActive = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.F03UserRoles AS existing
      WHERE existing.IdUser = u.Id
        AND existing.IdRole = r.Id
  );
GO

/* If an admin already has a role row, ensure it is still marked primary. */
UPDATE ur
SET IsPrimary = 1,
    IsActive = 1,
    ModifiedAt = GETDATE(),
    LastModifiedSource = N'SECURITY_ADMIN_COMPATIBILITY'
FROM dbo.F03UserRoles AS ur
INNER JOIN dbo.F03Users AS u
    ON u.Id = ur.IdUser
INNER JOIN dbo.F03Roles AS r
    ON r.Id = ur.IdRole
WHERE u.IsActive = 1
  AND u.PermissionCode IN (1, 2)
  AND r.RoleCode = u.PermissionCode;
GO

/* Verification: these are the capabilities required by /admin/security. */
SELECT
    u.Id AS UserId,
    u.EmployeeCode,
    u.FullName,
    u.PermissionCode,
    r.RoleCode,
    COUNT(DISTINCT rf.IdFunction) AS SecurityFunctionCount
FROM dbo.F03Users AS u
LEFT JOIN dbo.F03UserRoles AS ur ON ur.IdUser = u.Id AND ur.IsActive = 1
LEFT JOIN dbo.F03Roles AS r ON r.Id = ur.IdRole AND r.IsActive = 1
LEFT JOIN dbo.F03RoleFunctions AS rf ON rf.IdRole = r.Id AND rf.IsActive = 1
LEFT JOIN dbo.F03Functions AS f ON f.Id = rf.IdFunction AND (f.IsActive = 1 OR f.IsActive IS NULL)
    AND f.FunctionCode IN (2401,2402,2403,2404,2405,2406,2601,2602,2603,2604)
WHERE u.IsActive = 1
  AND u.PermissionCode IN (1, 2)
GROUP BY u.Id, u.EmployeeCode, u.FullName, u.PermissionCode, r.RoleCode
ORDER BY u.PermissionCode, u.EmployeeCode;
GO
