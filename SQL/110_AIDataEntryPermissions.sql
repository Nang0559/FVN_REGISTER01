/*
  110 - AI document data-entry capabilities.
  Idempotently registers dedicated capabilities and a least-privilege role matrix.
  Existing user-specific grants are not modified.
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 51901, N'dbo.F03Functions is required. Run SQL/14_SecurityAuthorization.sql first.', 1;
IF OBJECT_ID(N'dbo.F03Roles', N'U') IS NULL OR OBJECT_ID(N'dbo.F03RoleFunctions', N'U') IS NULL
    THROW 51902, N'RBAC role tables are required. Run SQL/14_SecurityAuthorization.sql first.', 1;
IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NULL
   OR COL_LENGTH(N'dbo.F03Functions', N'LifecycleStatus') IS NULL
   OR COL_LENGTH(N'dbo.F03Functions', N'SourceType') IS NULL
   OR COL_LENGTH(N'dbo.F03Functions', N'LastSeenAt') IS NULL
    THROW 51903, N'F03Functions security registry columns are missing. Run SQL/46_SecurityFunctionRegistry.sql first.', 1;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.F03Functions f
    JOIN (VALUES
        (3501,N'AIDocumentDataEntry.View'),
        (3502,N'AIDocumentDataEntry.TemplateManage'),
        (3503,N'AIDocumentDataEntry.RunCreate'),
        (3504,N'AIDocumentDataEntry.Confirm'),
        (3505,N'AIDocumentDataEntry.Export')
    ) v(FunctionCode,FunctionKey)
      ON f.FunctionCode = v.FunctionCode
    WHERE ISNULL(f.FunctionKey,N'') <> v.FunctionKey
)
    THROW 51904, N'One or more AI data-entry function codes are already assigned to another capability.', 1;

IF EXISTS
(
    SELECT 1
    FROM dbo.F03Functions f
    JOIN (VALUES
        (3501,N'AIDocumentDataEntry.View'),
        (3502,N'AIDocumentDataEntry.TemplateManage'),
        (3503,N'AIDocumentDataEntry.RunCreate'),
        (3504,N'AIDocumentDataEntry.Confirm'),
        (3505,N'AIDocumentDataEntry.Export')
    ) v(FunctionCode,FunctionKey)
      ON f.FunctionKey = v.FunctionKey
    WHERE f.FunctionCode <> v.FunctionCode
)
    THROW 51905, N'One or more AI data-entry function keys are already assigned to another code.', 1;
GO

MERGE dbo.F03Functions AS target
USING (VALUES
    (3501,N'AIDocumentDataEntry.View',N'Xem biểu mẫu và kết quả nhập liệu AI',N'Cho phép xem danh sách biểu mẫu và các lượt nhập liệu thuộc phạm vi tenant.',N'View',N'All',3501),
    (3502,N'AIDocumentDataEntry.TemplateManage',N'Quản lý biểu mẫu nhập liệu AI',N'Cho phép tạo và cập nhật cấu hình biểu mẫu Excel; chỉ dành cho quản trị viên.',N'TemplateManage',N'All',3502),
    (3503,N'AIDocumentDataEntry.RunCreate',N'Tạo lượt nhập liệu AI',N'Cho phép tải chứng từ và tạo lượt trích xuất dữ liệu; lượt chạy được ràng buộc với người tạo.',N'RunCreate',N'All',3503),
    (3504,N'AIDocumentDataEntry.Confirm',N'Xác nhận dữ liệu trích xuất AI',N'Cho phép người sở hữu lượt nhập liệu xác nhận dữ liệu đã kiểm tra.',N'Confirm',N'All',3504),
    (3505,N'AIDocumentDataEntry.Export',N'Xuất kết quả nhập liệu AI',N'Cho phép người sở hữu lượt nhập liệu xuất Excel sau khi xác nhận.',N'Export',N'All',3505)
) AS source(FunctionCode,FunctionKey,DisplayName,Detail,ActionCode,ScopeCode,DisplayOrder)
ON target.FunctionCode = source.FunctionCode
WHEN MATCHED THEN UPDATE SET
    target.IsActive = 1,
    target.FunctionName = source.FunctionKey,
    target.Detail = source.Detail,
    target.FunctionKey = source.FunctionKey,
    target.ModuleCode = N'AIDocumentDataEntry',
    target.ActionCode = source.ActionCode,
    target.ScopeCode = source.ScopeCode,
    target.LifecycleStatus = N'Active',
    target.SourceType = N'SecurityFunctionCodes',
    target.LastSeenAt = GETDATE(),
    target.DisplayOrder = source.DisplayOrder
WHEN NOT MATCHED BY TARGET THEN
    INSERT
    (
        IsActive,CreatedBy,LastModifiedSource,CreatedAt,
        FunctionCode,FunctionName,Detail,FunctionKey,ModuleCode,ActionCode,
        ScopeCode,LifecycleStatus,SourceType,LastSeenAt,DisplayOrder
    )
    VALUES
    (
        1,0,N'110_AI_DATA_ENTRY_PERMISSIONS',GETDATE(),
        source.FunctionCode,source.FunctionKey,source.Detail,source.FunctionKey,N'AIDocumentDataEntry',
        source.ActionCode,source.ScopeCode,N'Active',N'SecurityFunctionCodes',GETDATE(),source.DisplayOrder
    );
GO

/* Least privilege:
   - All authenticated business roles (1..5) can view and process their own runs.
   - Template design is restricted to SuperAdmin/Admin (1..2).
   - Guest role (6) receives no AI data-entry capability.
   - Run ownership is independently enforced by the AI data-entry service.
*/
INSERT dbo.F03RoleFunctions(IdRole,IdFunction)
SELECT r.Id,f.Id
FROM dbo.F03Roles r
JOIN dbo.F03Functions f
  ON f.FunctionCode IN (3501,3503,3504,3505)
WHERE r.RoleCode IN (1,2,3,4,5)
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.F03RoleFunctions rf
      WHERE rf.IdRole=r.Id AND rf.IdFunction=f.Id
  );

INSERT dbo.F03RoleFunctions(IdRole,IdFunction)
SELECT r.Id,f.Id
FROM dbo.F03Roles r
JOIN dbo.F03Functions f ON f.FunctionCode=3502
WHERE r.RoleCode IN (1,2)
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.F03RoleFunctions rf
      WHERE rf.IdRole=r.Id AND rf.IdFunction=f.Id
  );
GO

IF EXISTS
(
    SELECT required.FunctionCode
    FROM (VALUES (3501),(3502),(3503),(3504),(3505)) required(FunctionCode)
    LEFT JOIN dbo.F03Functions f ON f.FunctionCode=required.FunctionCode AND f.IsActive=1
    WHERE f.Id IS NULL
)
    THROW 51906, N'AI data-entry capability seed verification failed.', 1;

PRINT N'110_AIDataEntryPermissions completed: capabilities 3501-3505 seeded and least-privilege role grants applied.';
GO
