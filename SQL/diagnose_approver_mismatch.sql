/* Why does HRM sync report approvers as "missing"? Run on FVN_REGISTER (read-only). */

-- 1) Pairs (employee x active policy) that the CHECK counts, with what actually exists in F03Approvers.
SELECT  e.EmployeeCode, e.PositionCode, e.DeptCode AS EmployeeDept,
        ap.RequestType, ap.Level, ap.DeptCode AS PolicyDept,
        existing_same_dept  = (SELECT COUNT(*) FROM dbo.F03Approvers a
                               WHERE a.IsActive=1 AND a.ApproverCode=e.EmployeeCode AND a.Level=ap.Level
                                 AND a.RequestType=CASE ap.RequestType WHEN 0 THEN N'Leave' WHEN 1 THEN N'Overtime' WHEN 2 THEN N'Trip' WHEN 3 THEN N'Equipment' END
                                 AND a.ApproveForDeptCode=e.DeptCode),     -- what the proc creates
        existing_policy_dept= (SELECT COUNT(*) FROM dbo.F03Approvers a
                               WHERE a.IsActive=1 AND a.ApproverCode=e.EmployeeCode AND a.Level=ap.Level
                                 AND a.RequestType=CASE ap.RequestType WHEN 0 THEN N'Leave' WHEN 1 THEN N'Overtime' WHEN 2 THEN N'Trip' WHEN 3 THEN N'Equipment' END
                                 AND a.ApproveForDeptCode=ap.DeptCode)     -- what the old check required
FROM dbo.F03Employees e
JOIN dbo.F03ApprovalPolicies ap ON ap.ApprovalPositionCode=LTRIM(RTRIM(e.PositionCode)) AND ap.IsActive=1
WHERE e.IsActive=1 AND ISNULL(e.LevelApprove,0)>0
ORDER BY e.EmployeeCode, ap.RequestType, ap.Level;
-- If existing_same_dept > 0 and existing_policy_dept = 0 on (almost) every row, the check and the proc disagree.

-- 2) How many pairs have policy dept <> employee dept (these can never satisfy the old check).
SELECT SUM(CASE WHEN ap.DeptCode=e.DeptCode THEN 1 ELSE 0 END) AS same_dept,
       SUM(CASE WHEN ap.DeptCode<>e.DeptCode THEN 1 ELSE 0 END) AS different_dept
FROM dbo.F03Employees e
JOIN dbo.F03ApprovalPolicies ap ON ap.ApprovalPositionCode=LTRIM(RTRIM(e.PositionCode)) AND ap.IsActive=1
WHERE e.IsActive=1 AND ISNULL(e.LevelApprove,0)>0;
