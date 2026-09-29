USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

/*
  FVN_REGISTER - SuperAdmin full access invariant
  ------------------------------------------------
  RoleCode = 1 is the canonical SuperAdmin role.

  SuperAdmin means:
    1. Every active F03Function is assigned to RoleCode 1.
    2. Every active user whose legacy PermissionCode = 1 is linked to RoleCode 1.
    3. SuperAdmin users receive Company managed scope so function scopes such as
       Own/Employee/Department do not hide data from the security administrator.
    4. Newly-created F03Functions are automatically assigned to RoleCode 1 by
       the trigger below, so discovery of a new UI/API capability does not leave
       SuperAdmin without that capability.

  This does NOT disable business-state validation. Workflow/state-machine rules
  remain server-side and continue to apply to all users.
*/

IF OBJECT_ID(N'dbo.F03Roles', N'U') IS NULL
    THROW 51620, N'F03Roles chưa tồn tại. Chạy migration Security/RBAC trước.', 1;

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 51621, N'F03Functions chưa tồn tại. Chạy migration Security/RBAC trước.', 1;

IF OBJECT_ID(N'dbo.F03UserRoles', N'U') IS NULL
    THROW 51622, N'F03UserRoles chưa tồn tại. Chạy migration Security/RBAC trước.', 1;

IF OBJECT_ID(N'dbo.F03RoleFunctions', N'U') IS NULL
    THROW 51623, N'F03RoleFunctions chưa tồn tại. Chạy migration Security/RBAC trước.', 1;
GO

/* Canonical SuperAdmin role. */
IF NOT EXISTS (SELECT 1 FROM dbo.F03Roles WHERE RoleCode = 1)
BEGIN
    INSERT dbo.F03Roles
    (
        IsActive, CreatedBy, LastModifiedSource, CreatedAt,
        RoleCode, RoleName, Detail, IsSystem
    )
    VALUES
    (
        1, 0, N'SUPERADMIN_FULL_ACCESS', GETDATE(),
        1, N'SuperAdmin', N'Full system administration access.', 1
    );
END;
ELSE
BEGIN
    UPDATE dbo.F03Roles
    SET IsActive = 1,
        RoleName = N'SuperAdmin',
        IsSystem = 1,
        LastModifiedSource = N'SUPERADMIN_FULL_ACCESS',
        ModifiedAt = GETDATE()
    WHERE RoleCode = 1;
END;
GO

/* Link every legacy PermissionCode=1 SuperAdmin account to canonical RoleCode=1. */
INSERT dbo.F03UserRoles
(
    IsActive, CreatedBy, LastModifiedSource, CreatedAt,
    IdUser, IdRole, IsPrimary
)
SELECT
    1,
    0,
    N'SUPERADMIN_FULL_ACCESS',
    GETDATE(),
    u.Id,
    r.Id,
    1
FROM dbo.F03Users AS u
CROSS JOIN dbo.F03Roles AS r
WHERE u.IsActive = 1
  AND u.PermissionCode = 1
  AND r.RoleCode = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.F03UserRoles AS ur
      WHERE ur.IdUser = u.Id
        AND ur.IdRole = r.Id
  );
GO

UPDATE ur
SET ur.IsActive = 1,
    ur.IsPrimary = 1,
    ur.LastModifiedSource = N'SUPERADMIN_FULL_ACCESS',
    ur.ModifiedAt = GETDATE()
FROM dbo.F03UserRoles AS ur
INNER JOIN dbo.F03Users AS u ON u.Id = ur.IdUser
INNER JOIN dbo.F03Roles AS r ON r.Id = ur.IdRole
WHERE u.IsActive = 1
  AND u.PermissionCode = 1
  AND r.RoleCode = 1;
GO

/* Grant every currently active capability to SuperAdmin. */
INSERT dbo.F03RoleFunctions
(
    IsActive, CreatedBy, LastModifiedSource, CreatedAt,
    IdRole, IdFunction
)
SELECT
    1,
    0,
    N'SUPERADMIN_FULL_ACCESS',
    GETDATE(),
    r.Id,
    f.Id
FROM dbo.F03Roles AS r
CROSS JOIN dbo.F03Functions AS f
WHERE r.RoleCode = 1
  AND r.IsActive = 1
  AND (f.IsActive = 1 OR f.IsActive IS NULL)
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.F03RoleFunctions AS rf
      WHERE rf.IdRole = r.Id
        AND rf.IdFunction = f.Id
  );
GO

/* Re-enable previously existing but accidentally disabled SuperAdmin grants. */
UPDATE rf
SET rf.IsActive = 1,
    rf.LastModifiedSource = N'SUPERADMIN_FULL_ACCESS',
    rf.ModifiedAt = GETDATE()
FROM dbo.F03RoleFunctions AS rf
INNER JOIN dbo.F03Roles AS r ON r.Id = rf.IdRole
INNER JOIN dbo.F03Functions AS f ON f.Id = rf.IdFunction
WHERE r.RoleCode = 1
  AND r.IsActive = 1
  AND (f.IsActive = 1 OR f.IsActive IS NULL);
GO

/*
  Company scope for every active SuperAdmin.
  Managed scope is a data-scope supplement; capability still comes from
  F03RoleFunctions above.
*/
IF OBJECT_ID(N'dbo.F03ManagedScopes', N'U') IS NOT NULL
BEGIN
    INSERT dbo.F03ManagedScopes
    (
        IsActive, CreatedBy, LastModifiedSource, CreatedAt,
        EmployeeCode, NodeType, NodeCode, FactoryCode,
        DeptCode, SubDepartmentCode, IncludeChildren, Remark
    )
    SELECT
        1,
        0,
        N'SUPERADMIN_FULL_ACCESS',
        GETDATE(),
        u.EmployeeCode,
        N'Company',
        NULL,
        NULL,
        NULL,
        NULL,
        1,
        N'Automatic full-company scope for SuperAdmin.'
    FROM dbo.F03Users AS u
    WHERE u.IsActive = 1
      AND u.PermissionCode = 1
      AND NULLIF(LTRIM(RTRIM(u.EmployeeCode)), N'') IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.F03ManagedScopes AS ms
          WHERE ms.IsActive = 1
            AND ms.EmployeeCode = u.EmployeeCode
            AND ms.NodeType = N'Company'
            AND ISNULL(ms.NodeCode,N'') = N''
            AND ISNULL(ms.FactoryCode,N'') = N''
            AND ISNULL(ms.DeptCode,N'') = N''
            AND ISNULL(ms.SubDepartmentCode,N'') = N''
      );
END;
GO

/*
  Future-proof the invariant: SecurityFunctionDiscovery may create new
  F03Functions after this deployment. Automatically grant each new active
  function to SuperAdmin.
*/
IF OBJECT_ID(N'dbo.TR_F03Functions_SuperAdminFullAccess', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_F03Functions_SuperAdminFullAccess;
GO

CREATE TRIGGER dbo.TR_F03Functions_SuperAdminFullAccess
ON dbo.F03Functions
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS
    (
        SELECT 1
        FROM inserted
        WHERE IsActive = 1 OR IsActive IS NULL
    )
        RETURN;

    DECLARE @RoleId int;
    SELECT @RoleId = Id
    FROM dbo.F03Roles
    WHERE RoleCode = 1
      AND IsActive = 1;

    IF @RoleId IS NULL
        RETURN;

    INSERT dbo.F03RoleFunctions
    (
        IsActive, CreatedBy, LastModifiedSource, CreatedAt,
        IdRole, IdFunction
    )
    SELECT
        1,
        0,
        N'SUPERADMIN_FUNCTION_TRIGGER',
        GETDATE(),
        @RoleId,
        i.Id
    FROM inserted AS i
    WHERE (i.IsActive = 1 OR i.IsActive IS NULL)
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.F03RoleFunctions AS rf
          WHERE rf.IdRole = @RoleId
            AND rf.IdFunction = i.Id
      );

    UPDATE rf
    SET rf.IsActive = 1,
        rf.LastModifiedSource = N'SUPERADMIN_FUNCTION_TRIGGER',
        rf.ModifiedAt = GETDATE()
    FROM dbo.F03RoleFunctions AS rf
    INNER JOIN inserted AS i ON i.Id = rf.IdFunction
    WHERE rf.IdRole = @RoleId
      AND (i.IsActive = 1 OR i.IsActive IS NULL);
END;
GO

/* Verification: every active capability must be present for SuperAdmin. */
SELECT
    r.RoleCode,
    r.RoleName,
    COUNT(DISTINCT CASE WHEN (f.IsActive = 1 OR f.IsActive IS NULL) THEN f.Id END) AS ActiveFunctionCount,
    COUNT(DISTINCT CASE WHEN (f.IsActive = 1 OR f.IsActive IS NULL) AND rf.IsActive = 1 THEN f.Id END) AS GrantedFunctionCount,
    COUNT(DISTINCT CASE WHEN (f.IsActive = 1 OR f.IsActive IS NULL) AND rf.Id IS NULL THEN f.Id END) AS MissingFunctionCount
FROM dbo.F03Roles AS r
LEFT JOIN dbo.F03RoleFunctions AS rf ON rf.IdRole = r.Id
LEFT JOIN dbo.F03Functions AS f ON f.Id = rf.IdFunction
WHERE r.RoleCode = 1
GROUP BY r.RoleCode, r.RoleName;

SELECT
    u.Id AS UserId,
    u.EmployeeCode,
    u.FullName,
    u.PermissionCode,
    r.RoleCode,
    CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.F03UserRoles AS ur
        WHERE ur.IdUser = u.Id
          AND ur.IdRole = r.Id
          AND ur.IsActive = 1
    ) THEN 1 ELSE 0 END AS HasSuperAdminRole,
    CASE WHEN OBJECT_ID(N'dbo.F03ManagedScopes', N'U') IS NOT NULL
         AND EXISTS
         (
             SELECT 1
             FROM dbo.F03ManagedScopes AS ms
             WHERE ms.EmployeeCode = u.EmployeeCode
               AND ms.IsActive = 1
               AND ms.NodeType = N'Company'
         )
         THEN 1 ELSE 0 END AS HasCompanyScope
FROM dbo.F03Users AS u
CROSS JOIN dbo.F03Roles AS r
WHERE u.IsActive = 1
  AND u.PermissionCode = 1
  AND r.RoleCode = 1
ORDER BY u.EmployeeCode;
GO

PRINT N'SuperAdmin full UI/capability access invariant is ready.';
GO
