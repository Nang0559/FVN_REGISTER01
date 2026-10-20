/*
  108 - Independent Feature Operator for LateEarly.
  - LateEarly.RegistrarManage (3406) is independent from OT.RegistrarManage (3095).
  - Reuses the existing department / block / company-wide assignment model.
  - Enables LATE_EARLY in the shared registration-rule table.
  - Adds a database guard for on-behalf LateEarly requests.
*/
USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.F03OTRegistrationRules', N'U') IS NULL
    THROW 51801, N'F03OTRegistrationRules is required. Run SQL/93_OTTripRegistrationPolicy.sql first.', 1;
IF OBJECT_ID(N'dbo.F03FeatureOperatorAssignments', N'U') IS NULL
    THROW 51802, N'F03FeatureOperatorAssignments is required. Run SQL/14C_SecurityFeatureOperatorAssignments.sql first.', 1;
IF OBJECT_ID(N'dbo.F03LateEarlyRequests', N'U') IS NULL
    THROW 51803, N'F03LateEarlyRequests is required. Run the LateEarly schema scripts first.', 1;
IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NULL
    THROW 51806, N'F03Functions.FunctionKey is required. Run SQL/46_SecurityFunctionRegistry.sql first.', 1;
IF COL_LENGTH(N'dbo.F03Functions', N'DisplayOrder') IS NULL
    THROW 51807, N'F03Functions.DisplayOrder is required by the current security catalog schema.', 1;
IF COL_LENGTH(N'dbo.F03Functions', N'ModuleCode') IS NULL
   OR COL_LENGTH(N'dbo.F03Functions', N'ActionCode') IS NULL
   OR COL_LENGTH(N'dbo.F03Functions', N'ScopeCode') IS NULL
   OR COL_LENGTH(N'dbo.F03Functions', N'LifecycleStatus') IS NULL
   OR COL_LENGTH(N'dbo.F03Functions', N'SourceType') IS NULL
   OR COL_LENGTH(N'dbo.F03Functions', N'LastSeenAt') IS NULL
    THROW 51808, N'F03Functions security registry columns are missing. Run SQL/46_SecurityFunctionRegistry.sql first.', 1;

/* Permit the third registration type in the existing shared rule store. */
IF EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.F03OTRegistrationRules')
      AND name = N'CK_F03OTRegistrationRules_RegisterType'
)
    ALTER TABLE dbo.F03OTRegistrationRules DROP CONSTRAINT CK_F03OTRegistrationRules_RegisterType;
GO
IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.F03OTRegistrationRules')
      AND name = N'CK_F03OTRegistrationRules_RegisterType'
)
    ALTER TABLE dbo.F03OTRegistrationRules
        ADD CONSTRAINT CK_F03OTRegistrationRules_RegisterType
        CHECK (RegisterType IN (N'OT', N'TRIP', N'LATE_EARLY'));
GO

/* Default fallback rule: no position is automatically granted; explicit Feature Operators remain usable. */
IF NOT EXISTS
(
    SELECT 1 FROM dbo.F03OTRegistrationRules
    WHERE DepartmentCode = 0 AND RegisterType = N'LATE_EARLY'
)
BEGIN
    INSERT dbo.F03OTRegistrationRules
        (DepartmentCode, RegisterType, Mode, IsActive, CreatedBy)
    VALUES
        (0, N'LATE_EARLY', 3, 1, 0);
END;
GO

/* Register the Function so it is selectable in /admin/security.
   This does not grant the function to any role or user. */
IF EXISTS
(
    SELECT 1 FROM dbo.F03Functions
    WHERE FunctionCode = 3406 AND ISNULL(FunctionKey, N'') <> N'LateEarly.RegistrarManage'
)
    THROW 51804, N'FunctionCode 3406 is already used by another security function.', 1;
IF EXISTS
(
    SELECT 1 FROM dbo.F03Functions
    WHERE FunctionKey = N'LateEarly.RegistrarManage' AND FunctionCode <> 3406
)
    THROW 51805, N'LateEarly.RegistrarManage already exists with a different FunctionCode.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode = 3406 OR FunctionKey = N'LateEarly.RegistrarManage')
BEGIN
    INSERT dbo.F03Functions
    (
        IsActive, CreatedBy, LastModifiedSource, CreatedAt,
        FunctionCode, FunctionName, Detail, FunctionKey, ModuleCode, ActionCode,
        ScopeCode, LifecycleStatus, SourceType, LastSeenAt, DisplayOrder
    )
    VALUES
    (
        1, 0, N'108_LATE_EARLY_INDEPENDENT_REGISTRAR', GETDATE(),
        3406, N'Chỉ định người đăng ký Đi muộn/Về sớm',
        N'Quản lý danh sách người được chỉ định đăng ký Đi muộn/Về sớm theo phòng ban, khối hoặc toàn công ty.',
        N'LateEarly.RegistrarManage', N'LateEarly', N'RegistrarManage',
        N'All', N'Active', N'SecurityFunctionCodes', GETDATE(), 3406
    );
END;

UPDATE dbo.F03Functions
SET IsActive = 1,
    FunctionName = N'Chỉ định người đăng ký Đi muộn/Về sớm',
    Detail = N'Quản lý danh sách người được chỉ định đăng ký Đi muộn/Về sớm theo phòng ban, khối hoặc toàn công ty.',
    ModuleCode = N'LateEarly',
    ActionCode = N'RegistrarManage',
    ScopeCode = N'All',
    LifecycleStatus = N'Active',
    SourceType = N'SecurityFunctionCodes',
    LastSeenAt = GETDATE()
WHERE FunctionCode = 3406 AND FunctionKey = N'LateEarly.RegistrarManage';
GO

/* DB-level protection mirrors the application policy for on-behalf requests.
   Self-registration is unaffected. OT registrar assignments never satisfy this check. */
CREATE OR ALTER TRIGGER dbo.trg_F03LateEarlyRequests_RegistrarPolicy
ON dbo.F03LateEarlyRequests
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT (UPDATE(EmployeeCode) OR UPDATE(RegisteredByEmployeeCode)
            OR UPDATE(IsOnBehalf) OR UPDATE(CreatedBy) OR UPDATE(DeptCode))
        RETURN;

    IF EXISTS
    (
        SELECT 1
        FROM inserted q
        OUTER APPLY
        (
            SELECT TOP (1) rr.*
            FROM dbo.F03OTRegistrationRules rr
            WHERE rr.IsActive = 1
              AND rr.RegisterType = N'LATE_EARLY'
              AND rr.DepartmentCode IN (ISNULL(q.DeptCode, 0), 0)
            ORDER BY CASE WHEN rr.DepartmentCode = ISNULL(q.DeptCode, 0) THEN 0 ELSE 1 END
        ) policy_rule
        OUTER APPLY
        (
            SELECT TOP (1) u.EmployeeCode, e.PositionCode
            FROM dbo.F03Users u
            LEFT JOIN dbo.F03Employees e ON e.EmployeeCode = u.EmployeeCode
            WHERE u.Id = q.CreatedBy
        ) creator_info
        WHERE UPPER(LTRIM(RTRIM(q.EmployeeCode))) <> UPPER(LTRIM(RTRIM(ISNULL(creator_info.EmployeeCode, N''))))
          AND
          (
              policy_rule.Id IS NULL
              OR NOT
              (
              policy_rule.Mode = 1
              OR (policy_rule.Mode = 2 AND EXISTS
                  (SELECT 1 FROM dbo.F03OTRegistrars x
                   WHERE x.RegistrationRuleId = policy_rule.Id
                     AND x.UserId = q.CreatedBy
                     AND x.RegisterType = N'LATE_EARLY'))
              OR (policy_rule.Mode = 3 AND EXISTS
                  (SELECT 1 FROM dbo.F03OTRegistrarPositions x
                   WHERE x.RegistrationRuleId = policy_rule.Id
                     AND x.PositionCode = creator_info.PositionCode
                     AND x.RegisterType = N'LATE_EARLY'))
              OR EXISTS
              (
                  SELECT 1
                  FROM dbo.F03FeatureOperatorAssignments fa
                  WHERE fa.IsActive = 1
                    AND fa.FunctionCode = 3406
                    AND fa.ResourceType = N'LATE_EARLY_REGISTRAR'
                    AND fa.EmployeeCode = creator_info.EmployeeCode
                    AND
                    (
                        fa.ResourceId = ISNULL(q.DeptCode, 0)
                        OR
                        (
                            fa.ResourceId IS NULL
                            AND
                            (
                                ISNULL(fa.ScopeCode, N'') IN (N'', N'All')
                                OR fa.ScopeCode = N'Block:' + ISNULL
                                (
                                    (SELECT TOP (1) d.BlockCode
                                     FROM dbo.F03Departments d
                                     WHERE d.DeptCode = ISNULL(q.DeptCode, 0)),
                                    N'#'
                                )
                            )
                        )
                    )
              )
              )
          )
    )
    BEGIN
        THROW 51533, N'Người đăng ký không có quyền đăng ký Đi muộn/Về sớm cho nhân viên khác theo policy của phòng ban.', 1;
    END;
END;
GO

PRINT N'108_LateEarlyIndependentRegistrar completed. LateEarly registrar assignments are now independent from OT.';

SELECT FunctionCode, FunctionKey, FunctionName, IsActive
FROM dbo.F03Functions
WHERE FunctionCode = 3406 OR FunctionKey = N'LateEarly.RegistrarManage';

SELECT DepartmentCode, RegisterType, Mode, IsActive
FROM dbo.F03OTRegistrationRules
WHERE RegisterType = N'LATE_EARLY'
ORDER BY DepartmentCode;
GO
