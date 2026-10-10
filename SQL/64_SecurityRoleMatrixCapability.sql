SET NOCOUNT ON;
GO

/*
  FVN_REGISTER - Security role/function matrix capability
  --------------------------------------------------------
  2602 Security.ManageRoles is used by SecurityCenter to expose the
  role/function permission matrix. It must remain an active capability.
  The actual write endpoint is additionally protected by 2603
  Security.ManageFunctions.
*/

/* Canonicalize the retired legacy alias Global -> All before the role matrix is loaded. */
UPDATE dbo.F03Functions
SET ScopeCode = N'All'
WHERE ScopeCode = N'Global';

UPDATE dbo.F03RoleFunctions
SET ScopeCode = N'All'
WHERE ScopeCode = N'Global';
GO

IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode = 2602)
BEGIN
    INSERT INTO dbo.F03Functions
    (
        FunctionCode, FunctionName, Detail, ModuleCode, ActionCode,
        ScopeCode, DisplayOrder, IsActive, CreatedBy, CreatedAt
    )
    VALUES
    (
        2602,
        N'Security.ManageRoles',
        N'Quản lý ma trận phân quyền chức năng theo vai trò',
        N'Security',
        N'ManageRoles',
        N'All',
        2602,
        1,
        0,
        GETDATE()
    );
END
ELSE
BEGIN
    UPDATE dbo.F03Functions
    SET IsActive = 1,
        FunctionName = N'Security.ManageRoles',
        Detail = N'Quản lý ma trận phân quyền chức năng theo vai trò',
        ModuleCode = N'Security',
        ActionCode = N'ManageRoles',
        ScopeCode = N'All',
        DisplayOrder = 2602
    WHERE FunctionCode = 2602;
END;
GO

/* SuperAdmin must always have this capability. */
INSERT INTO dbo.F03RoleFunctions
(
    IdRole, IdFunction, IsActive, CreatedBy, CreatedAt
)
SELECT r.Id, f.Id, 1, 0, GETDATE()
FROM dbo.F03Roles AS r
CROSS JOIN dbo.F03Functions AS f
WHERE r.RoleCode = 1
  AND r.IsActive = 1
  AND f.FunctionCode = 2602
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
  AND f.FunctionCode = 2602
  AND f.IsActive = 1;
GO

SELECT
    f.FunctionCode,
    f.FunctionName,
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
FROM dbo.F03Functions f
WHERE f.FunctionCode = 2602;
GO
