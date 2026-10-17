/*
  CHẨN ĐOÁN: vì sao "mọi user vẫn đăng ký được OT".  CHỈ ĐỌC, không sửa dữ liệu.
  Chạy trên DB FVNWEBAPP và gửi lại kết quả 4 phần.
*/
SET NOCOUNT ON;

PRINT N'--- 1) Rule đang hiệu lực (Mode: 1=Everyone, 2=AssignedUsers, 3=AssignedPositions) ---';
SELECT Id, DepartmentCode, RegisterType, Mode, IsActive FROM dbo.F03OTRegistrationRules ORDER BY RegisterType, DepartmentCode;

PRINT N'--- 2) Chức vụ được phép đăng ký theo rule (staff/worker KHÔNG được có mặt ở đây) ---';
SELECT rp.RegistrationRuleId, r.DepartmentCode, rp.RegisterType, rp.PositionCode, rp.IsActive
FROM dbo.F03OTRegistrarPositions rp JOIN dbo.F03OTRegistrationRules r ON r.Id = rp.RegistrationRuleId
ORDER BY rp.RegisterType, r.DepartmentCode, rp.PositionCode;

PRINT N'--- 3) User được chỉ định qua Feature Operator OT.RegistrarManage (3095) ---';
SELECT fa.Id, fa.EmployeeCode, fa.ResourceId AS DeptCode, fa.ScopeCode, fa.IsActive
FROM dbo.F03FeatureOperatorAssignments fa
WHERE fa.FunctionCode = 3095 AND fa.ResourceType = N'OT_REGISTRAR' ORDER BY fa.IsActive DESC, fa.EmployeeCode;

PRINT N'--- 4) Nhân viên đang là registrar nhờ CHỨC VỤ (rule fallback DepartmentCode=0, Mode=3) — đếm theo chức vụ ---';
SELECT e.PositionCode, COUNT(*) AS SoNguoi
FROM dbo.F03Employees e
WHERE e.IsActive = 1
  AND EXISTS (SELECT 1 FROM dbo.F03OTRegistrarPositions rp
              JOIN dbo.F03OTRegistrationRules r ON r.Id = rp.RegistrationRuleId
              WHERE r.RegisterType = N'OT' AND r.IsActive = 1 AND r.DepartmentCode = 0 AND r.Mode = 3
                AND rp.PositionCode = e.PositionCode)
GROUP BY e.PositionCode ORDER BY SoNguoi DESC;
