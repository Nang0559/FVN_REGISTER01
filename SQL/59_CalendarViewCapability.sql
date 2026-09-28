USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
  59_CalendarViewCapability.sql
  Calendar.View (3043) was only seeded by 38_RBAC_HR_IT_MANAGED_SCOPE.sql, which is not part of
  00_Deploy_All.sql / 00_Deploy_Run.sql. On a database deployed with the master scripts the
  capability did not exist, so cross-employee calendar access (Calendar.View + ManagedScope)
  could never be granted. This script is idempotent and only ADDS the capability; it does not
  change any existing role/user assignment.
  Own-calendar access does not depend on this capability (see WorkCalendarController).
*/

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 51590, N'F03Functions is required before applying Calendar.View.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode = 3043)
BEGIN
    IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
        EXEC (N'INSERT dbo.F03Functions(IsActive,CreatedBy,FunctionCode,FunctionKey,FunctionName,Detail,ModuleCode,ActionCode,ScopeCode,DisplayOrder)
                VALUES(1,0,3043,N''Calendar.View'',N''Calendar.View'',N''Xem lịch làm việc chung'',N''Calendar'',N''View'',N''Own'',1040);');
    ELSE
        EXEC (N'INSERT dbo.F03Functions(IsActive,CreatedBy,FunctionCode,FunctionName,Detail,ModuleCode,ActionCode,ScopeCode,DisplayOrder)
                VALUES(1,0,3043,N''Calendar.View'',N''Xem lịch làm việc chung'',N''Calendar'',N''View'',N''Own'',1040);');
END
GO

/* Same baseline as 38_RBAC_HR_IT_MANAGED_SCOPE.sql: roles 1,2,3,5,7,8. */
INSERT dbo.F03RoleFunctions(IdRole, IdFunction)
SELECT r.Id, f.Id
FROM dbo.F03Roles r
CROSS JOIN dbo.F03Functions f
WHERE r.RoleCode IN (1,2,3,5,7,8)
  AND f.FunctionCode = 3043
  AND NOT EXISTS (SELECT 1 FROM dbo.F03RoleFunctions rf WHERE rf.IdRole = r.Id AND rf.IdFunction = f.Id);
GO

IF OBJECT_ID(N'dbo.F03SecurityFunctionRegistry', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
BEGIN
    EXEC (N'INSERT INTO dbo.F03SecurityFunctionRegistry
            (FunctionKey, FunctionCode, DefinitionName, ModuleCode, ActionCode, ScopeCode,
             LifecycleStatus, SourceType, DefinitionHash, FirstDiscoveredAt, LastSeenAt, IsIgnored)
            SELECT f.FunctionKey, f.FunctionCode, f.FunctionName, f.ModuleCode, f.ActionCode, f.ScopeCode,
                   N''Active'', N''Code'',
                   CONVERT(varchar(128), HASHBYTES(''SHA2_256'', CONCAT(f.FunctionKey, N''|'', f.FunctionCode, N''|'', f.FunctionName)), 2),
                   GETDATE(), GETDATE(), 0
            FROM dbo.F03Functions f
            WHERE f.FunctionCode = 3043
              AND f.FunctionKey IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM dbo.F03SecurityFunctionRegistry r WHERE r.FunctionKey = f.FunctionKey);');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode = 3043 AND IsActive = 1)
    THROW 51591, N'Calendar.View capability is missing.', 1;
GO
