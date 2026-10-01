SET NOCOUNT ON;
GO

/*
  FVN_REGISTER - System Critical Security Functions
  -------------------------------------------------
  System-critical functions are part of the security infrastructure itself.
  Their function definition must always exist and remain active.

  Current critical set:
    2601 Security.View
    2602 Security.ManageRoles
    2603 Security.ManageFunctions
    2604 Security.Audit

  Normal business functions continue to use IsActive normally.
*/

IF COL_LENGTH(N'dbo.F03Functions', N'IsSystemCritical') IS NULL
BEGIN
    ALTER TABLE dbo.F03Functions
        ADD IsSystemCritical bit NOT NULL
            CONSTRAINT DF_F03Functions_IsSystemCritical DEFAULT (0);
END;
GO

/* Ensure every critical definition exists even on an older database. */
DECLARE @Critical TABLE
(
    FunctionCode int NOT NULL,
    FunctionKey nvarchar(150) NOT NULL,
    FunctionName nvarchar(100) NOT NULL,
    Detail nvarchar(500) NOT NULL,
    ActionCode nvarchar(50) NOT NULL
);

INSERT INTO @Critical VALUES
(2601, N'Security.View', N'Security.View', N'Xem Security Center', N'View'),
(2602, N'Security.ManageRoles', N'Security.ManageRoles', N'Quản lý role', N'ManageRoles'),
(2603, N'Security.ManageFunctions', N'Security.ManageFunctions', N'Quản lý function/action', N'ManageFunctions'),
(2604, N'Security.Audit', N'Security.Audit', N'Xem audit security', N'Audit');

INSERT INTO dbo.F03Functions
(
    IsActive, CreatedBy, FunctionCode, FunctionKey, FunctionName, Detail,
    ModuleCode, ActionCode, ScopeCode, LifecycleStatus, SourceType,
    DisplayOrder, IsSystemCritical
)
SELECT
    1, 0, c.FunctionCode, c.FunctionKey, c.FunctionName, c.Detail,
    N'Security', c.ActionCode, N'All', N'Active', N'System',
    c.FunctionCode, 1
FROM @Critical AS c
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.F03Functions AS f
    WHERE f.FunctionCode = c.FunctionCode
);

UPDATE f
SET
    f.FunctionKey = c.FunctionKey,
    f.FunctionName = c.FunctionName,
    f.Detail = c.Detail,
    f.ModuleCode = N'Security',
    f.ActionCode = c.ActionCode,
    f.ScopeCode = N'All'
FROM dbo.F03Functions AS f
INNER JOIN @Critical AS c ON c.FunctionCode = f.FunctionCode;

/* Normalize the current critical set. */
UPDATE f
SET
    f.IsSystemCritical = 1,
    f.IsActive = 1,
    f.LifecycleStatus = N'Active'
FROM dbo.F03Functions AS f
WHERE f.FunctionCode IN (2601, 2602, 2603, 2604);
GO

/* Existing SuperAdmin must retain every critical Security capability. */
INSERT INTO dbo.F03RoleFunctions
(
    IdRole, IdFunction, IsActive, CreatedBy, CreatedAt
)
SELECT r.Id, f.Id, 1, 0, GETDATE()
FROM dbo.F03Roles AS r
CROSS JOIN dbo.F03Functions AS f
WHERE r.RoleCode = 1
  AND r.IsActive = 1
  AND f.FunctionCode IN (2601, 2602, 2603, 2604)
  AND f.IsSystemCritical = 1
  AND f.IsActive = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.F03RoleFunctions AS rf
      WHERE rf.IdRole = r.Id
        AND rf.IdFunction = f.Id
  );
GO

UPDATE rf
SET rf.IsActive = 1
FROM dbo.F03RoleFunctions AS rf
INNER JOIN dbo.F03Roles AS r ON r.Id = rf.IdRole
INNER JOIN dbo.F03Functions AS f ON f.Id = rf.IdFunction
WHERE r.RoleCode = 1
  AND r.IsActive = 1
  AND f.IsSystemCritical = 1;
GO

/*
  Database guard #1:
  a critical function can never be inserted/updated inactive, marked
  non-critical, or deleted.
*/
CREATE OR ALTER TRIGGER dbo.TR_F03Functions_SystemCriticalGuard
ON dbo.F03Functions
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS i
        WHERE i.FunctionCode IN (2601, 2602, 2603, 2604)
          AND (ISNULL(i.IsSystemCritical, 0) <> 1 OR ISNULL(i.IsActive, 0) <> 1)
    )
    BEGIN
        THROW 51001, N'System Critical Security Function không được vô hiệu hóa hoặc hạ cấp.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM deleted AS d
        LEFT JOIN inserted AS i ON i.Id = d.Id
        WHERE d.FunctionCode IN (2601, 2602, 2603, 2604)
          AND
          (
              i.Id IS NULL
              OR i.FunctionCode NOT IN (2601, 2602, 2603, 2604)
              OR ISNULL(i.IsSystemCritical, 0) <> 1
              OR ISNULL(i.IsActive, 0) <> 1
          )
    )
    BEGIN
        THROW 51002, N'Không được xóa, đổi mã hoặc hạ cấp System Critical Security Function.', 1;
    END;
END;
GO

/*
  Database guard #2:
  SuperAdmin cannot lose a System Critical Security capability.
  Normal roles remain fully manageable by the existing RBAC matrix.
*/
CREATE OR ALTER TRIGGER dbo.TR_F03RoleFunctions_SuperAdminCriticalGuard
ON dbo.F03RoleFunctions
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM deleted AS d
        INNER JOIN dbo.F03Roles AS r ON r.Id = d.IdRole
        INNER JOIN dbo.F03Functions AS f ON f.Id = d.IdFunction
        WHERE r.RoleCode = 1
          AND f.IsSystemCritical = 1
          AND NOT EXISTS
          (
              SELECT 1
              FROM inserted AS i
              WHERE i.Id = d.Id
          )
    )
    BEGIN
        THROW 51003, N'SuperAdmin bắt buộc phải giữ System Critical Security Functions.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS i
        INNER JOIN dbo.F03Roles AS r ON r.Id = i.IdRole
        INNER JOIN dbo.F03Functions AS f ON f.Id = i.IdFunction
        WHERE r.RoleCode = 1
          AND f.IsSystemCritical = 1
          AND ISNULL(i.IsActive, 0) <> 1
    )
    BEGIN
        THROW 51004, N'System Critical Security Function của SuperAdmin không được vô hiệu hóa.', 1;
    END;
END;
GO

/* Final invariant report. */
SELECT
    f.FunctionCode,
    f.FunctionKey,
    f.FunctionName,
    f.IsSystemCritical,
    f.IsActive,
    CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.F03RoleFunctions rf
        INNER JOIN dbo.F03Roles r ON r.Id = rf.IdRole
        WHERE r.RoleCode = 1
          AND rf.IdFunction = f.Id
          AND rf.IsActive = 1
    ) THEN 1 ELSE 0 END AS SuperAdminGranted
FROM dbo.F03Functions AS f
WHERE f.FunctionCode IN (2601, 2602, 2603, 2604)
ORDER BY f.FunctionCode;
GO
