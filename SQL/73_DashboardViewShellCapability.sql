USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

/*
  FVN_REGISTER - Dashboard.View (2701) is a "shell" capability, not a personal one.

  Problem:
    2701 was seeded with ScopeCode = 'Own'. F03RoleFunctions rows inherit that scope, so the
    effective AccessMode becomes 'Personal'. AuthorizationService.GetSnapshotAsync drops every
    Personal grant when the account has no ACTIVE F03Employees row (break-glass / manually created
    SuperAdmin, or EmployeeCode not present in F03Employees). Result: DashboardController returns
    403 even though the account is linked to role SuperAdmin.

  Fix:
    1. 2701 becomes a Management/All capability for every role (module widgets are still gated
       by each provider's own capability: Leave/OT/Trip/Equipment View).
    2. Every active user with PermissionCode = 1 is (re)linked to RoleCode = 1, so accounts created
       after 62_SuperAdminFullAccess.sql ran are not left without a role.
  Idempotent.
*/

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 51730, N'F03Functions chưa tồn tại. Chạy migration Security/RBAC trước.', 1;
IF OBJECT_ID(N'dbo.F03RoleFunctions', N'U') IS NULL
    THROW 51731, N'F03RoleFunctions chưa tồn tại. Chạy migration Security/RBAC trước.', 1;
GO

UPDATE dbo.F03Functions
SET ScopeCode = N'All'
WHERE FunctionCode = 2701
  AND ISNULL(ScopeCode, N'') <> N'All';
GO

/* Every active role gets the Dashboard shell. */
INSERT dbo.F03RoleFunctions (IsActive, CreatedBy, LastModifiedSource, CreatedAt, IdRole, IdFunction)
SELECT 1, 0, N'DASHBOARD_SHELL', GETDATE(), r.Id, f.Id
FROM dbo.F03Roles AS r
JOIN dbo.F03Functions AS f ON f.FunctionCode = 2701
WHERE r.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.F03RoleFunctions rf WHERE rf.IdRole = r.Id AND rf.IdFunction = f.Id);
GO

UPDATE rf
SET rf.IsActive = 1,
    rf.ScopeCode = N'All',
    rf.AccessMode = N'Management',
    rf.LastModifiedSource = N'DASHBOARD_SHELL',
    rf.ModifiedAt = GETDATE()
FROM dbo.F03RoleFunctions AS rf
JOIN dbo.F03Roles AS r ON r.Id = rf.IdRole
JOIN dbo.F03Functions AS f ON f.Id = rf.IdFunction
WHERE f.FunctionCode = 2701
  AND r.IsActive = 1
  AND (rf.IsActive IS NULL OR rf.IsActive = 0 OR ISNULL(rf.ScopeCode, N'') <> N'All' OR ISNULL(rf.AccessMode, N'') <> N'Management');
GO

/* Link every active PermissionCode = 1 account to the canonical SuperAdmin role. */
IF OBJECT_ID(N'dbo.F03UserRoles', N'U') IS NOT NULL
BEGIN
    INSERT dbo.F03UserRoles (IsActive, CreatedBy, LastModifiedSource, CreatedAt, IdUser, IdRole, IsPrimary)
    SELECT 1, 0, N'DASHBOARD_SHELL', GETDATE(), u.Id, r.Id, 1
    FROM dbo.F03Users AS u
    JOIN dbo.F03Roles AS r ON r.RoleCode = 1 AND r.IsActive = 1
    WHERE u.IsActive = 1
      AND u.PermissionCode = 1
      AND NOT EXISTS (SELECT 1 FROM dbo.F03UserRoles ur WHERE ur.IdUser = u.Id AND ur.IdRole = r.Id);

    UPDATE ur
    SET ur.IsActive = 1, ur.LastModifiedSource = N'DASHBOARD_SHELL', ur.ModifiedAt = GETDATE()
    FROM dbo.F03UserRoles AS ur
    JOIN dbo.F03Users AS u ON u.Id = ur.IdUser
    JOIN dbo.F03Roles AS r ON r.Id = ur.IdRole
    WHERE u.IsActive = 1 AND u.PermissionCode = 1 AND r.RoleCode = 1
      AND (ur.IsActive IS NULL OR ur.IsActive = 0);
END;
GO

IF EXISTS (
    SELECT 1
    FROM dbo.F03Roles r
    JOIN dbo.F03Functions f ON f.FunctionCode = 2701
    WHERE r.RoleCode = 1 AND r.IsActive = 1
      AND NOT EXISTS (
          SELECT 1 FROM dbo.F03RoleFunctions rf
          WHERE rf.IdRole = r.Id AND rf.IdFunction = f.Id
            AND rf.IsActive = 1 AND rf.AccessMode = N'Management'))
    THROW 51732, N'SuperAdmin chưa có Dashboard.View (2701) dạng Management.', 1;
PRINT N'73_DashboardViewShellCapability: OK';
GO
