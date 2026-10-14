/*
  Diagnose "Chưa cấu hình người phê duyệt cho cấp N" for ONE requester (read-only, SQL Server 2012+).
  Fill in the two variables, run, and read the 5 result sets in order.
*/
DECLARE @EmployeeCode nvarchar(50) = N'<MÃ NHÂN VIÊN ĐANG ĐĂNG KÝ>';
DECLARE @RequestType  int          = 1;      -- 0 Leave (phép), 1 Overtime (OT), 2 Trip, 3 Equipment

DECLARE @TypeName nvarchar(20) =
    CASE @RequestType WHEN 0 THEN N'Leave' WHEN 1 THEN N'Overtime' WHEN 2 THEN N'Trip' WHEN 3 THEN N'Equipment' END;

-- 1) The requester as the route sees him (department + position come ONLY from F03Employees).
SELECT e.EmployeeCode, e.EmployeeName, e.DeptCode, d.DeptName, e.PositionCode, p.PositionName
FROM dbo.F03Employees e
LEFT JOIN dbo.F03Departments d ON d.DeptCode=e.DeptCode
LEFT JOIN dbo.F03Positions  p ON p.PositionCode=e.PositionCode
WHERE e.EmployeeCode=@EmployeeCode;

-- 2) Policies that apply to him. Expected after the seed (Overtime, requester = worker/staff):
--    L1 0002+0006, L2 0004+0010, L3 0011+0005+0009, L4 0001.
--    Only ONE row at a level (e.g. just 0005 at L3) = old policies, the seed skipped this dept.
SELECT ap.Level, ap.Sequence, ap.ApprovalPositionCode, ap.LevelName, ap.RoleName, ap.Required,
       RequesterPosition=ap.PositionCode, ap.LastModifiedSource, ap.Id
FROM dbo.F03ApprovalPolicies ap
JOIN dbo.F03Employees e ON e.EmployeeCode=@EmployeeCode
WHERE ap.IsActive=1 AND ap.RequestType=@RequestType AND ap.DeptCode=e.DeptCode
  AND (ap.PositionCode IS NULL OR ap.PositionCode=e.PositionCode)
ORDER BY ap.Level, ap.ApprovalPositionCode;

-- 3) For every policy row: the approvers the route would find. A row with ApproverCode = NULL
--    is a (Level, position) that has NO candidate -> that is the level the screen complains about.
SELECT ap.Level, ap.ApprovalPositionCode,
       a.ApproverCode, a.ApproverName, a.ApproveForDeptCode, a.ApproverDeptCode,
       ApproverRowSource=a.LastModifiedSource,
       EmployeeActive=emp.IsActive, EmployeePosition=emp.PositionCode
FROM dbo.F03ApprovalPolicies ap
JOIN dbo.F03Employees e ON e.EmployeeCode=@EmployeeCode
LEFT JOIN dbo.F03Approvers a
       ON a.IsActive=1
      AND a.RequestType=@TypeName
      AND a.Level=ap.Level
      AND a.PositionCode=ap.ApprovalPositionCode
      AND a.ApproverCode<>@EmployeeCode
      AND (a.ApproveForDeptCode=e.DeptCode OR a.ApproveForDeptCode=0)
LEFT JOIN dbo.F03Employees emp ON emp.EmployeeCode=a.ApproverCode AND emp.IsActive=1
WHERE ap.IsActive=1 AND ap.RequestType=@RequestType AND ap.DeptCode=e.DeptCode
  AND (ap.PositionCode IS NULL OR ap.PositionCode=e.PositionCode)
ORDER BY ap.Level, ap.ApprovalPositionCode, a.ApproverCode;

-- 4) Who holds the approval positions of this requester's policies, and in WHICH department.
--    HRM sync only creates an approver for the department the person belongs to. If the Manager of
--    the requester's department sits in another DeptCode (or nobody holds the position there),
--    level 3 can never be filled automatically -> add a Manual approver row (ApproveForDept = dept or ALL).
SELECT TOP (100)
       e.PositionCode, p.PositionName, e.DeptCode, d.DeptName,
       e.EmployeeCode, e.EmployeeName, e.IsActive, e.LevelApprove,
       SameDeptAsRequester=CASE WHEN e.DeptCode=r.DeptCode THEN 1 ELSE 0 END
FROM dbo.F03Employees r
JOIN dbo.F03ApprovalPolicies ap
  ON ap.IsActive=1 AND ap.RequestType=@RequestType AND ap.DeptCode=r.DeptCode
 AND (ap.PositionCode IS NULL OR ap.PositionCode=r.PositionCode)
JOIN dbo.F03Employees e ON e.IsActive=1 AND LTRIM(RTRIM(e.PositionCode))=ap.ApprovalPositionCode
LEFT JOIN dbo.F03Positions  p ON p.PositionCode=e.PositionCode
LEFT JOIN dbo.F03Departments d ON d.DeptCode=e.DeptCode
WHERE r.EmployeeCode=@EmployeeCode
GROUP BY e.PositionCode,p.PositionName,e.DeptCode,d.DeptName,e.EmployeeCode,e.EmployeeName,e.IsActive,e.LevelApprove,r.DeptCode
ORDER BY CASE WHEN e.DeptCode=r.DeptCode THEN 0 ELSE 1 END, e.PositionCode, e.EmployeeCode;

-- 5) Approver rows that exist for those levels/types but for another department (wrong ApproveForDeptCode).
SELECT TOP (100) a.Level, a.PositionCode, a.ApproverCode, a.ApproverName,
       a.ApproveForDeptCode, a.ApproverDeptCode, a.IsActive, a.LastModifiedSource
FROM dbo.F03Approvers a
WHERE a.RequestType=@TypeName
  AND a.PositionCode IN (SELECT ap.ApprovalPositionCode
                         FROM dbo.F03ApprovalPolicies ap
                         JOIN dbo.F03Employees r ON r.EmployeeCode=@EmployeeCode
                         WHERE ap.IsActive=1 AND ap.RequestType=@RequestType AND ap.DeptCode=r.DeptCode)
ORDER BY a.Level, a.PositionCode, a.ApproveForDeptCode, a.ApproverCode;
