/*
  Pre-pilot audit for Feature Operator assignments.
  This script is intentionally non-destructive and is NOT part of deployment.
  It reports active assignments whose employee/user/function state no longer satisfies
  the canonical operator rule.

  Canonical rule:
    - normal assignment functions: RBAC + assignment are both required;
    - capability-granting functions (currently Execution.Review): assignment grants
      the function capability, but the assignment scope must still be valid;
    - direct F03UserFunction rows are intentionally ignored because AuthorizationService
      does not treat them as effective RBAC grants;
    - do not delete assignments automatically.

  Remediation:
    - employee missing/inactive -> correct HRM/user mapping;
    - function missing/inactive -> correct function catalog;
    - RBAC missing -> grant the Function through the canonical active Role -> RoleFunction path;
    - invalid capability scope -> set All / Department / Own (or NULL for function default).
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
        a.EmployeeCode,
        a.ScopeCode
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
    x.ScopeCode,
    CASE
        WHEN x.EmployeeId IS NULL THEN N'EMPLOYEE_NOT_FOUND'
        WHEN ISNULL(x.EmployeeIsActive, 0) <> 1 THEN N'EMPLOYEE_INACTIVE'
        WHEN x.UserId IS NULL THEN N'USER_NOT_FOUND'
        WHEN ISNULL(x.UserIsActive, 0) <> 1 THEN N'USER_INACTIVE'
        WHEN f.Id IS NULL THEN N'FUNCTION_NOT_FOUND_OR_INACTIVE'
        WHEN x.FunctionKey = N'Execution.Review'
             AND x.ScopeCode IS NOT NULL
             AND x.ScopeCode NOT IN (N'All', N'Department', N'Own') THEN N'INVALID_SCOPE'
        WHEN x.FunctionKey = N'Execution.Review' THEN N'OK_ASSIGNMENT_GRANTS_CAPABILITY'
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
    OR (x.FunctionKey = N'Execution.Review'
        AND x.ScopeCode IS NOT NULL
        AND x.ScopeCode NOT IN (N'All', N'Department', N'Own'))
    OR (x.FunctionKey <> N'Execution.Review'
        AND NOT EXISTS
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
        ))
ORDER BY x.FunctionCode, x.ResourceType, x.ResourceId, x.EmployeeCode;
GO
