USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
  59_CalendarViewCapability.sql

  Canonical Calendar.View capability = FunctionCode 3043.
  This migration must repair an existing F03Functions row as well as create
  the capability on a new database. Older deployments may already contain
  3043 but with IsActive=0 or incomplete metadata; INSERT-only logic leaves
  those databases failing the final capability gate.

  This script is intentionally idempotent and does not change existing
  user/role scope policy beyond ensuring the canonical role assignments.
*/

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 51590, N'F03Functions is required before applying Calendar.View.', 1;

DECLARE @FunctionId int;

SELECT TOP (1) @FunctionId = Id
FROM dbo.F03Functions
WHERE FunctionCode = 3043
ORDER BY Id;

IF @FunctionId IS NULL
BEGIN
    IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
    BEGIN
        INSERT dbo.F03Functions
        (
            IsActive, CreatedBy, FunctionCode, FunctionKey, FunctionName,
            Detail, ModuleCode, ActionCode, ScopeCode, DisplayOrder
        )
        VALUES
        (
            1, 0, 3043, N'Calendar.View', N'Calendar.View',
            N'Xem lịch làm việc chung', N'Calendar', N'View', N'Own', 1040
        );
    END
    ELSE
    BEGIN
        INSERT dbo.F03Functions
        (
            IsActive, CreatedBy, FunctionCode, FunctionName,
            Detail, ModuleCode, ActionCode, ScopeCode, DisplayOrder
        )
        VALUES
        (
            1, 0, 3043, N'Calendar.View',
            N'Xem lịch làm việc chung', N'Calendar', N'View', N'Own', 1040
        );
    END;

    SET @FunctionId = CONVERT(int, SCOPE_IDENTITY());
END
ELSE
BEGIN
    /* Repair an existing 3043 instead of relying on INSERT-only behavior. */
    UPDATE dbo.F03Functions
    SET IsActive = 1,
        FunctionName = N'Calendar.View',
        Detail = N'Xem lịch làm việc chung',
        ModuleCode = N'Calendar',
        ActionCode = N'View',
        ScopeCode = N'Own',
        DisplayOrder = 1040,
        ModifiedAt = GETDATE(),
        LastModifiedSource = N'59_CALENDAR_VIEW_CAPABILITY'
    WHERE Id = @FunctionId;

    IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
    BEGIN
        UPDATE dbo.F03Functions
        SET FunctionKey = N'Calendar.View'
        WHERE Id = @FunctionId;
    END;
END;
GO

/* Same baseline as the canonical RBAC model: roles 1,2,3,5,7,8. */
INSERT dbo.F03RoleFunctions(IdRole, IdFunction)
SELECT r.Id, f.Id
FROM dbo.F03Roles r
CROSS JOIN dbo.F03Functions f
WHERE r.RoleCode IN (1,2,3,5,7,8)
  AND f.FunctionCode = 3043
  AND f.IsActive = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.F03RoleFunctions rf
      WHERE rf.IdRole = r.Id
        AND rf.IdFunction = f.Id
  );
GO

/* Reconcile the active Calendar.View function with the Function Registry. */
IF OBJECT_ID(N'dbo.F03SecurityFunctionRegistry', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
BEGIN
    EXEC (N'
        INSERT INTO dbo.F03SecurityFunctionRegistry
        (
            FunctionKey, FunctionCode, DefinitionName, ModuleCode, ActionCode, ScopeCode,
            LifecycleStatus, SourceType, DefinitionHash, FirstDiscoveredAt, LastSeenAt, IsIgnored
        )
        SELECT f.FunctionKey, f.FunctionCode, f.FunctionName, f.ModuleCode, f.ActionCode, f.ScopeCode,
               N''Active'', N''Code'',
               CONVERT(varchar(128), HASHBYTES(''SHA2_256'', CONCAT(f.FunctionKey, N''|'', f.FunctionCode, N''|'', f.FunctionName)), 2),
               GETDATE(), GETDATE(), 0
        FROM dbo.F03Functions f
        WHERE f.FunctionCode = 3043
          AND f.IsActive = 1
          AND f.FunctionKey IS NOT NULL
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.F03SecurityFunctionRegistry r
              WHERE r.FunctionKey = f.FunctionKey
          );

        UPDATE r
        SET r.FunctionCode = f.FunctionCode,
            r.DefinitionName = f.FunctionName,
            r.ModuleCode = f.ModuleCode,
            r.ActionCode = f.ActionCode,
            r.ScopeCode = f.ScopeCode,
            r.LifecycleStatus = N''Active'',
            r.LastSeenAt = GETDATE(),
            r.IsIgnored = 0
        FROM dbo.F03SecurityFunctionRegistry r
        JOIN dbo.F03Functions f ON f.FunctionKey = r.FunctionKey
        WHERE f.FunctionCode = 3043
          AND f.IsActive = 1;
    ');
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.F03Functions
    WHERE FunctionCode = 3043
      AND IsActive = 1
)
    THROW 51591, N'Calendar.View capability is missing.', 1;

PRINT N'59_CalendarViewCapability: Calendar.View (3043) verified and active.';
GO
