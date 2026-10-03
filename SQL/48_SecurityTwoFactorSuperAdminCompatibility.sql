USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
  Security compatibility fix:
  E0001 is a legacy direct-grant SuperAdmin when that account already exists.

  This script is intentionally seed-safe. HRM provisioning is the owner of
  employee/user creation, so a fresh database must not fail merely because
  E0001 has not been provisioned yet. When E0001 exists, grant Function 2407
  (UserManagement.ManageTwoFactor) directly with the SuperAdmin permission.
*/

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 51480, N'F03Functions is required before applying 48_SecurityTwoFactorSuperAdminCompatibility.', 1;

IF OBJECT_ID(N'dbo.F03UserFunctions', N'U') IS NULL
    THROW 51481, N'F03UserFunctions is required before applying 48_SecurityTwoFactorSuperAdminCompatibility.', 1;

DECLARE @UserId int;
DECLARE @FunctionId int;
DECLARE @PermissionId int;

SELECT TOP (1) @UserId = u.Id
FROM dbo.F03Users u
WHERE u.EmployeeCode = N'E0001'
  AND ISNULL(u.IsActive, 1) = 1
ORDER BY u.Id;

SELECT TOP (1) @FunctionId = f.Id
FROM dbo.F03Functions f
WHERE f.FunctionCode = 2407
  AND ISNULL(f.IsActive, 1) = 1
ORDER BY f.Id;

SELECT TOP (1) @PermissionId = p.Id
FROM dbo.F03Permissions p
WHERE p.PermissionCode = 1
  AND ISNULL(p.IsActive, 1) = 1
ORDER BY p.Id;

/*
  Do not manufacture a user here. F03Users is provisioned from HRM and this
  compatibility patch must not create an account with an unknown credential.
*/
IF @UserId IS NULL
BEGIN
    PRINT N'48_SecurityTwoFactorSuperAdminCompatibility: E0001 is not provisioned yet; direct 2FA grant deferred to HRM user provisioning.';
    RETURN;
END;

IF @FunctionId IS NULL
    THROW 51483, N'Function 2407 UserManagement.ManageTwoFactor was not found. Apply 41_SecurityFunctionCleanup before this migration.', 1;

IF @PermissionId IS NULL
    THROW 51484, N'PermissionCode=1 (SuperAdmin) was not found.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.F03UserFunctions uf
    WHERE uf.IdUser = @UserId
      AND uf.IdFunction = @FunctionId
      AND uf.IdPermission = @PermissionId
)
BEGIN
    INSERT dbo.F03UserFunctions
    (
        IsActive,
        CreatedBy,
        LastModifiedSource,
        CreatedAt,
        IdUser,
        IdPermission,
        IdFunction
    )
    VALUES
    (
        1,
        0,
        N'SecurityFunctionRegistry.2FACompatibility',
        GETDATE(),
        @UserId,
        @PermissionId,
        @FunctionId
    );
END
ELSE
BEGIN
    UPDATE dbo.F03UserFunctions
    SET IsActive = 1,
        LastModifiedSource = N'SecurityFunctionRegistry.2FACompatibility',
        ModifiedAt = GETDATE()
    WHERE IdUser = @UserId
      AND IdFunction = @FunctionId
      AND IdPermission = @PermissionId;
END;

PRINT N'48_SecurityTwoFactorSuperAdminCompatibility: direct SuperAdmin 2FA grant verified for E0001.';
GO
