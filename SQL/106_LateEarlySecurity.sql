/*
  106 - LateEarly dedicated security capabilities
  Run after SQL/46_SecurityFunctionRegistry.sql.
  This registers function definitions only; role/user grants remain governed by Security Center.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 51601, N'F03Functions is required before registering LateEarly permissions.', 1;

IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NULL
    THROW 51602, N'F03Functions.FunctionKey is required. Run the current security-function registry migration first.', 1;

DECLARE @Definitions TABLE
(
    FunctionCode int NOT NULL PRIMARY KEY,
    FunctionKey nvarchar(150) NOT NULL,
    FunctionName nvarchar(150) NOT NULL,
    Detail nvarchar(500) NOT NULL,
    ModuleCode nvarchar(50) NOT NULL,
    ActionCode nvarchar(50) NOT NULL,
    ScopeCode nvarchar(30) NOT NULL,
    DisplayOrder int NOT NULL
);

INSERT @Definitions(FunctionCode, FunctionKey, FunctionName, Detail, ModuleCode, ActionCode, ScopeCode, DisplayOrder)
VALUES
(3401, N'LateEarly.View',    N'Xem đăng ký đi muộn/về sớm',       N'Xem đăng ký đi muộn/về sớm theo phạm vi được cấp.', N'LateEarly', N'View',    N'Own',        3401),
(3402, N'LateEarly.Create',  N'Tạo đăng ký đi muộn/về sớm',      N'Tạo đăng ký đi muộn/về sớm; đăng ký hộ phải qua chính sách registrar.', N'LateEarly', N'Create',  N'Own',        3402),
(3403, N'LateEarly.Edit',    N'Sửa đăng ký đi muộn/về sớm',      N'Sửa bản nháp hoặc yêu cầu cần chỉnh sửa.', N'LateEarly', N'Edit',    N'Own',        3403),
(3404, N'LateEarly.Cancel',  N'Hủy đăng ký đi muộn/về sớm',      N'Hủy đăng ký chưa hoàn tất.', N'LateEarly', N'Cancel',  N'Own',        3404),
(3405, N'LateEarly.Approve', N'Duyệt đăng ký đi muộn/về sớm',    N'Phê duyệt theo Approval Policy và phạm vi được cấp.', N'LateEarly', N'Approve', N'Department', 3405);

IF EXISTS
(
    SELECT 1
    FROM @Definitions d
    JOIN dbo.F03Functions f ON f.FunctionCode = d.FunctionCode
    WHERE ISNULL(f.FunctionKey, N'') <> d.FunctionKey
)
    THROW 51603, N'Mã FunctionCode 3401-3405 đã được dùng cho chức năng khác. Cần xử lý xung đột trước khi chạy migration.', 1;

IF EXISTS
(
    SELECT 1
    FROM @Definitions d
    JOIN dbo.F03Functions f ON f.FunctionKey = d.FunctionKey
    WHERE f.FunctionCode <> d.FunctionCode
)
    THROW 51604, N'FunctionKey LateEarly đã tồn tại với FunctionCode khác. Cần xử lý xung đột trước khi chạy migration.', 1;

UPDATE f
SET f.FunctionKey = d.FunctionKey,
    f.FunctionName = d.FunctionName,
    f.Detail = d.Detail,
    f.ModuleCode = d.ModuleCode,
    f.ActionCode = d.ActionCode,
    f.ScopeCode = d.ScopeCode,
    f.DisplayOrder = d.DisplayOrder,
    f.IsActive = 1,
    f.ModifiedAt = GETDATE(),
    f.LastModifiedSource = N'106_LATE_EARLY_SECURITY'
FROM dbo.F03Functions f
JOIN @Definitions d ON d.FunctionCode = f.FunctionCode;

INSERT dbo.F03Functions
(
    IsActive, CreatedBy, FunctionCode, FunctionKey, FunctionName,
    Detail, ModuleCode, ActionCode, ScopeCode, DisplayOrder
)
SELECT 1, 0, d.FunctionCode, d.FunctionKey, d.FunctionName,
       d.Detail, d.ModuleCode, d.ActionCode, d.ScopeCode, d.DisplayOrder
FROM @Definitions d
WHERE NOT EXISTS (SELECT 1 FROM dbo.F03Functions f WHERE f.FunctionCode = d.FunctionCode);

/* Seed role grants by cloning the closest existing request capability:
   - registrar creation follows OT.Create (registrars are still constrained by LATE_EARLY policy)
   - personal view/edit/cancel follows Trip equivalents
   - approver grant follows either Trip.Approve or OT.Approve.
   Administrators can refine these grants later in Security Center. */
DECLARE @GrantMap TABLE(SourceCode int NOT NULL, TargetCode int NOT NULL);
INSERT @GrantMap(SourceCode, TargetCode)
VALUES (2201,3401),(2102,3402),(2203,3403),(2204,3404),(2205,3405),(2105,3405);

INSERT dbo.F03RoleFunctions(IdRole, IdFunction, ScopeCode, AccessMode)
SELECT DISTINCT sourceGrant.IdRole, targetFunction.Id, sourceGrant.ScopeCode, sourceGrant.AccessMode
FROM @GrantMap mapping
JOIN dbo.F03Functions sourceFunction ON sourceFunction.FunctionCode = mapping.SourceCode AND sourceFunction.IsActive = 1
JOIN dbo.F03RoleFunctions sourceGrant ON sourceGrant.IdFunction = sourceFunction.Id
JOIN dbo.F03Functions targetFunction ON targetFunction.FunctionCode = mapping.TargetCode AND targetFunction.IsActive = 1
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.F03RoleFunctions existing
    WHERE existing.IdRole = sourceGrant.IdRole
      AND existing.IdFunction = targetFunction.Id
);

IF OBJECT_ID(N'dbo.F03SecurityFunctionRegistry', N'U') IS NOT NULL
BEGIN
    INSERT dbo.F03SecurityFunctionRegistry
    (
        FunctionKey, FunctionCode, DefinitionName, ModuleCode, ActionCode, ScopeCode,
        LifecycleStatus, SourceType, DefinitionHash, FirstDiscoveredAt, LastSeenAt, IsIgnored
    )
    SELECT f.FunctionKey, f.FunctionCode, f.FunctionName, f.ModuleCode, f.ActionCode, f.ScopeCode,
           N'Active', N'Code',
           CONVERT(varchar(128), HASHBYTES('SHA2_256', CONCAT(f.FunctionKey, N'|', f.FunctionCode, N'|', f.FunctionName)), 2),
           GETDATE(), GETDATE(), 0
    FROM dbo.F03Functions f
    JOIN @Definitions d ON d.FunctionCode = f.FunctionCode
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.F03SecurityFunctionRegistry r WHERE r.FunctionKey = f.FunctionKey
    );

    UPDATE r
    SET r.FunctionCode = f.FunctionCode,
        r.DefinitionName = f.FunctionName,
        r.ModuleCode = f.ModuleCode,
        r.ActionCode = f.ActionCode,
        r.ScopeCode = f.ScopeCode,
        r.LifecycleStatus = N'Active',
        r.LastSeenAt = GETDATE(),
        r.IsIgnored = 0
    FROM dbo.F03SecurityFunctionRegistry r
    JOIN dbo.F03Functions f ON f.FunctionKey = r.FunctionKey
    JOIN @Definitions d ON d.FunctionCode = f.FunctionCode;
END;

PRINT N'106_LateEarlySecurity: dedicated LateEarly function codes registered.';
