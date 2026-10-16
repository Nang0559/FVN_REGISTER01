/*
  Execution.Review is a capability-granting operator function (effective capability =
  role grant OR active assignment). This script is read-only: it shows which roles still carry
  the grant (they make the assignment unnecessary for everyone in that role) and the active
  assignments. Review the output, then un-tick the function from shared roles in Security Center.
*/
USE [FVN_REGISTER];
GO

-- 1. Roles that currently carry Execution.Review (should normally NOT include the shared 'User' role)
SELECT r.RoleCode, r.RoleName, COUNT(DISTINCT ur.IdUser) AS UsersInRole
FROM dbo.F03RoleFunctions rf
INNER JOIN dbo.F03Roles r     ON r.Id = rf.IdRole AND r.IsActive = 1
INNER JOIN dbo.F03Functions f ON f.Id = rf.IdFunction AND f.FunctionCode = 2802
LEFT  JOIN dbo.F03UserRoles ur ON ur.IdRole = r.Id AND ur.IsActive = 1
WHERE rf.IsActive = 1
GROUP BY r.RoleCode, r.RoleName
ORDER BY UsersInRole DESC;
GO

-- 2. Active module-wide assignments (who can review through the assignment itself)
SELECT a.Id, a.EmployeeCode, e.EmployeeName, e.IsActive AS EmployeeActive, a.ScopeCode,
       a.CreatedBy, a.CreatedAt, a.LastModifiedSource
FROM dbo.F03FeatureOperatorAssignments a
LEFT JOIN dbo.F03Employees e ON e.EmployeeCode = a.EmployeeCode
WHERE a.IsActive = 1 AND a.FunctionCode = 2802 AND a.ResourceId IS NULL
ORDER BY a.EmployeeCode;
GO

-- 3. Assignments that will not grant anything (inactive employee/user): should be empty after the next HRM sync
SELECT a.Id, a.EmployeeCode, e.IsActive AS EmployeeActive, u.IsActive AS UserActive
FROM dbo.F03FeatureOperatorAssignments a
LEFT JOIN dbo.F03Employees e ON e.EmployeeCode = a.EmployeeCode
LEFT JOIN dbo.F03Users u     ON u.EmployeeCode = a.EmployeeCode
WHERE a.IsActive = 1 AND a.FunctionCode = 2802
  AND (ISNULL(e.IsActive, 0) = 0 OR ISNULL(u.IsActive, 0) = 0);
GO
