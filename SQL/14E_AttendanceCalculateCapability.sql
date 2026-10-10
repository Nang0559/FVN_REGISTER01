USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
  Canonical attendance calculation capability.
  This is deliberately isolated from the legacy authorization seed so that
  existing databases can be upgraded idempotently without replaying the full
  RBAC catalog.
*/

IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 53070, N'F03Functions is required before Attendance.Calculate can be seeded.', 1;

/* FunctionKey is created later by 46_SecurityFunctionRegistry.sql (which also backfills
   'Attendance.Calculate' for code 2911). It must NOT be referenced statically here:
   on a fresh database the column does not exist yet (Msg 207 at batch compile). */
IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'IF NOT EXISTS
(
    SELECT 1
    FROM dbo.F03Functions
    WHERE FunctionCode = 2911
       OR FunctionKey = N''Attendance.Calculate''
)
BEGIN
    INSERT dbo.F03Functions
    (
        FunctionKey,
        FunctionCode,
        FunctionName,
        Detail,
        ModuleCode,
        ActionCode,
        ScopeCode,
        IsActive,
        CreatedBy,
        DisplayOrder
    )
    VALUES
    (
        N''Attendance.Calculate'',
        2911,
        N''Attendance.Calculate'',
        N''Tính và đồng bộ dữ liệu chấm công HRM'',
        N''Attendance'',
        N''Calculate'',
        N''All'',
        1,
        0,
        290
    );
END
ELSE
BEGIN
    /* Repair an older/incomplete row instead of creating a duplicate. */
    UPDATE f
       SET FunctionKey = N''Attendance.Calculate'',
           FunctionName = N''Attendance.Calculate'',
           Detail = N''Tính và đồng bộ dữ liệu chấm công HRM'',
           ModuleCode = N''Attendance'',
           ActionCode = N''Calculate'',
           ScopeCode = N''All'',
           IsActive = 1
    FROM dbo.F03Functions AS f
    WHERE f.FunctionCode = 2911
       OR f.FunctionKey = N''Attendance.Calculate'';
END;';
END
ELSE
BEGIN
IF NOT EXISTS
(
    SELECT 1
    FROM dbo.F03Functions
    WHERE FunctionCode = 2911
)
BEGIN
    INSERT dbo.F03Functions
    (
        FunctionCode,
        FunctionName,
        Detail,
        ModuleCode,
        ActionCode,
        ScopeCode,
        IsActive,
        CreatedBy,
        DisplayOrder
    )
    VALUES
    (
        2911,
        N'Attendance.Calculate',
        N'Tính và đồng bộ dữ liệu chấm công HRM',
        N'Attendance',
        N'Calculate',
        N'All',
        1,
        0,
        290
    );
END
ELSE
BEGIN
    /* Repair an older/incomplete row instead of creating a duplicate. */
    UPDATE f
       SET FunctionName = N'Attendance.Calculate',
           Detail = N'Tính và đồng bộ dữ liệu chấm công HRM',
           ModuleCode = N'Attendance',
           ActionCode = N'Calculate',
           ScopeCode = N'All',
           IsActive = 1
    FROM dbo.F03Functions AS f
    WHERE f.FunctionCode = 2911;
END;
END;

/* SuperAdmin and Admin receive the server capability. */
IF OBJECT_ID(N'dbo.F03Roles', N'U') IS NULL
    THROW 53071, N'F03Roles is required before Attendance.Calculate role grants can be seeded.', 1;
IF OBJECT_ID(N'dbo.F03RoleFunctions', N'U') IS NULL
    THROW 53072, N'F03RoleFunctions is required before Attendance.Calculate role grants can be seeded.', 1;

DECLARE @FunctionId int;
SELECT TOP (1) @FunctionId = Id
FROM dbo.F03Functions
WHERE FunctionCode = 2911
  AND IsActive = 1
ORDER BY Id;

IF @FunctionId IS NULL
    THROW 53073, N'Attendance.Calculate capability 2911 could not be resolved after seeding.', 1;

INSERT dbo.F03RoleFunctions
(
    IdRole,
    IdFunction,
    IsActive,
    CreatedBy
)
SELECT r.Id,
       @FunctionId,
       1,
       0
FROM dbo.F03Roles AS r
WHERE r.RoleCode IN (1, 2)
  AND ISNULL(r.IsActive, 1) = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.F03RoleFunctions AS rf
      WHERE rf.IdRole = r.Id
        AND rf.IdFunction = @FunctionId
  );

/* Normal users must never receive this calculation capability. */
DELETE rf
FROM dbo.F03RoleFunctions AS rf
JOIN dbo.F03Roles AS r ON r.Id = rf.IdRole
JOIN dbo.F03Functions AS f ON f.Id = rf.IdFunction
WHERE r.RoleCode = 5
  AND f.FunctionCode = 2911;

IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'IF NOT EXISTS
(
    SELECT 1
    FROM dbo.F03Functions
    WHERE FunctionCode = 2911
      AND FunctionKey = N''Attendance.Calculate''
      AND FunctionName = N''Attendance.Calculate''
      AND ModuleCode = N''Attendance''
      AND ActionCode = N''Calculate''
      AND ScopeCode = N''All''
      AND ISNULL(IsActive, 1) = 1
)
    THROW 53074, N''Attendance.Calculate capability 2911 is not canonical after deployment.'', 1;';
END
ELSE
IF NOT EXISTS
(
    SELECT 1
    FROM dbo.F03Functions
    WHERE FunctionCode = 2911
      AND FunctionName = N'Attendance.Calculate'
      AND ModuleCode = N'Attendance'
      AND ActionCode = N'Calculate'
      AND ScopeCode = N'All'
      AND ISNULL(IsActive, 1) = 1
)
    THROW 53074, N'Attendance.Calculate capability 2911 is not canonical after deployment.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.F03RoleFunctions rf
    JOIN dbo.F03Roles r ON r.Id = rf.IdRole
    JOIN dbo.F03Functions f ON f.Id = rf.IdFunction
    WHERE r.RoleCode IN (1,2)
      AND f.FunctionCode = 2911
      AND ISNULL(rf.IsActive,1) = 1
)
    THROW 53075, N'Attendance.Calculate must be granted to SuperAdmin/Admin.', 1;

PRINT N'Attendance.Calculate capability 2911 seeded and verified.';
GO
