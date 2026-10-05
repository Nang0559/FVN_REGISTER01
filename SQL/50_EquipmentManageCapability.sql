USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
  Equipment security gate.
  Equipment.Manage is intentionally a separate capability from the
  contextual actions (handover/transfer/repair/return). It is granted only
  to the SuperAdmin role here. Existing business capabilities are not
  re-enabled or otherwise changed by this migration.
*/

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 51500, N'F03Functions is required before applying 50_EquipmentManageCapability.', 1;

/*
  Compatibility guard:
  50 may also be deployed independently by the targeted deployment script.
  Do not assume 46_SecurityFunctionRegistry has already run. SQL Server compiles
  a batch before executing ALTER TABLE, so the schema change is separated with
  GO before any statement that references FunctionKey.
*/
IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NULL
    ALTER TABLE dbo.F03Functions ADD FunctionKey nvarchar(150) NULL;
GO


/*
  46_SecurityFunctionRegistry makes FunctionKey mandatory.
  Therefore every new function must be inserted with its stable key.
*/
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode = 2319)
BEGIN
    INSERT INTO dbo.F03Functions
    (
        FunctionKey, FunctionCode, FunctionName, Detail, ModuleCode, ActionCode,
        ScopeCode, DisplayOrder, IsActive, CreatedBy, CreatedAt
    )
    VALUES
    (
        N'Equipment.Manage',
        2319,
        N'Equipment.Manage',
        N'Quản lý thiết bị: danh sách, nghiệp vụ quản trị và các thao tác được cấp riêng',
        N'Equipment',
        N'Manage',
        N'Global',
        2319,
        1,
        0,
        GETDATE()
    );
END
ELSE
BEGIN
    UPDATE dbo.F03Functions
    SET FunctionKey = N'Equipment.Manage',
        IsActive = 1,
        FunctionName = N'Equipment.Manage',
        Detail = N'Quản lý thiết bị: danh sách, nghiệp vụ quản trị và các thao tác được cấp riêng',
        ModuleCode = N'Equipment',
        ActionCode = N'Manage',
        ScopeCode = N'Global'
    WHERE FunctionCode = 2319;
END;
GO

/* Grant Equipment.Manage only to the active SuperAdmin role. */
INSERT INTO dbo.F03RoleFunctions (IdRole, IdFunction, IsActive, CreatedBy, CreatedAt)
SELECT r.Id, f.Id, 1, 0, GETDATE()
FROM dbo.F03Roles AS r
CROSS JOIN dbo.F03Functions AS f
WHERE r.IsActive = 1
  AND r.RoleCode = 1
  AND f.FunctionCode = 2319
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.F03RoleFunctions AS rf
      WHERE rf.IdRole = r.Id
        AND rf.IdFunction = f.Id
  );
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.F03Functions
    WHERE FunctionCode = 2319
      AND FunctionKey = N'Equipment.Manage'
      AND IsActive = 1
)
    THROW 51501, N'Equipment.Manage was not registered as active.', 1;
GO
