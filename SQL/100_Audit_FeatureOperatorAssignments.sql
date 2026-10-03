/*
  Pre-pilot audit for Feature Operator assignments.
  This script is intentionally non-destructive and is NOT part of deployment.
  It reports active assignments whose employee/user/RBAC state no longer satisfies
  the canonical operator rule.

  Remediation:
    - employee missing/inactive -> correct HRM/user mapping;
    - RBAC missing -> grant the Function through an active Role -> RoleFunction;
    - do not delete assignments automatically.
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;

;WITH ActiveAssignments AS
(
    SELECT
        a.Id AS AssignmentId,
        a.FunctionCode,
        f.FunctionKey,
        a.ResourceType,
        a.ResourceId,
        a.EmployeeCode
    FROM dbo.F03FeatureOperatorAssignments a
    LEFT JOIN dbo.F03Functions f
        ON f.FunctionCode = a.FunctionCode
    WHERE a.IsActive = 1
),
EmployeeState AS
(
    SELECT
        a.*,
        e.Id AS EmployeeId,
        e.IsActive AS EmployeeIsActive,
        u.Id AS UserId,
        u.IsActive AS UserIsActive
    FROM ActiveAssignments a
    LEFT JOIN dbo.F03Employees e
        ON e.EmployeeCode = a.EmployeeCode
    LEFT JOIN dbo.F03Users u
        ON u.EmployeeCode = a.EmployeeCode
)
SELECT
    x.AssignmentId,
    x.FunctionCode,
    x.FunctionKey,
    x.ResourceType,
    x.ResourceId,
    x.EmployeeCode,
    CASE
        WHEN x.EmployeeId IS NULL THEN N'EMPLOYEE_NOT_FOUND'
        WHEN ISNULL(x.EmployeeIsActive, 0) <> 1 THEN N'EMPLOYEE_INACTIVE'
        WHEN x.UserId IS NULL THEN N'USER_NOT_FOUND'
        WHEN ISNULL(x.UserIsActive, 0) <> 1 THEN N'USER_INACTIVE'
        WHEN f.Id IS NULL THEN N'FUNCTION_NOT_FOUND_OR_INACTIVE'
        WHEN EXISTS
        (
            SELECT 1
            FROM dbo.F03UserRoles ur
            INNER JOIN dbo.F03Roles r
                ON r.Id = ur.IdRole
            INNER JOIN dbo.F03RoleFunctions rf
                ON rf.IdRole = ur.IdRole
            INNER JOIN dbo.F03Functions fr
                ON fr.Id = rf.IdFunction
            WHERE ur.IdUser = x.UserId
              AND ur.IsActive = 1
              AND r.IsActive = 1
              AND rf.IsActive = 1
              AND ISNULL(fr.IsActive, 1) = 1
              AND fr.FunctionCode = x.FunctionCode
        ) THEN N'OK'
        ELSE N'RBAC_MISSING'
    END AS AuditStatus
FROM EmployeeState x
LEFT JOIN dbo.F03Functions f
    ON f.FunctionCode = x.FunctionCode
   AND ISNULL(f.IsActive, 1) = 1
WHERE
    x.EmployeeId IS NULL
    OR ISNULL(x.EmployeeIsActive, 0) <> 1
    OR x.UserId IS NULL
    OR ISNULL(x.UserIsActive, 0) <> 1
    OR f.Id IS NULL
    OR NOT EXISTS
    (
        SELECT 1
        FROM dbo.F03UserRoles ur
        INNER JOIN dbo.F03Roles r
            ON r.Id = ur.IdRole
        INNER JOIN dbo.F03RoleFunctions rf
            ON rf.IdRole = ur.IdRole
        INNER JOIN dbo.F03Functions fr
            ON fr.Id = rf.IdFunction
        WHERE ur.IdUser = x.UserId
          AND ur.IsActive = 1
          AND r.IsActive = 1
          AND rf.IsActive = 1
          AND ISNULL(fr.IsActive, 1) = 1
          AND fr.FunctionCode = x.FunctionCode
    )
ORDER BY x.FunctionCode, x.ResourceType, x.ResourceId, x.EmployeeCode;
GO
