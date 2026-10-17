/* Hotfix: trigger không chặn UPDATE hệ thống của tính công HRM (lỗi 51531). Idempotent - chạy trên DB FVN_REGISTER. */
GO
CREATE OR ALTER TRIGGER dbo.trg_F03OTEmployees_RegistrarPolicy
ON dbo.F03OTEmployees
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    /* Chỉ kiểm tra khi THÊM người vào đơn hoặc ĐỔI người/đơn. Các UPDATE hệ thống (usp_CalculateHrmAttendance cập nhật
       giờ thực tế, ValidationStatus, ModifiedBy...) không đổi EmployeeCode/OTRequestId nên không được kiểm tra lại policy,
       nếu không đơn OT cũ của người không còn là registrar sẽ làm hỏng cả lần tính công (lỗi 51531). */
    IF NOT (UPDATE(EmployeeCode) OR UPDATE(OTRequestId)) RETURN;

    IF NOT EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.F03OTRequests q ON q.Id = i.OTRequestId
        CROSS APPLY
        (
            SELECT TOP (1) rr.*
            FROM dbo.F03OTRegistrationRules rr
            WHERE rr.IsActive = 1
              AND rr.RegisterType = N'OT'
              AND rr.DepartmentCode IN (ISNULL(q.DeptCode, 0), 0)
            ORDER BY CASE WHEN rr.DepartmentCode = ISNULL(q.DeptCode, 0) THEN 0 ELSE 1 END
        ) policy_rule
        OUTER APPLY
        (
            /* F03OTRequests.EmployeeCode is the canonical requester identity.
               CreatedBy is an audit UserId and must not be used to infer the employee
               when user IDs and employee records are not guaranteed to share the same mapping. */
            SELECT TOP (1) e.EmployeeCode, e.PositionCode
            FROM dbo.F03Employees e
            WHERE UPPER(LTRIM(RTRIM(e.EmployeeCode))) =
                  UPPER(LTRIM(RTRIM(ISNULL(q.EmployeeCode, N''))))
        ) creator_info
        WHERE UPPER(LTRIM(RTRIM(i.EmployeeCode))) <> UPPER(LTRIM(RTRIM(ISNULL(q.EmployeeCode, N''))))
          AND NOT
          (
              policy_rule.Mode = 1
              OR (policy_rule.Mode = 2 AND EXISTS (SELECT 1 FROM dbo.F03OTRegistrars x WHERE x.RegistrationRuleId = policy_rule.Id AND x.UserId = q.CreatedBy AND x.RegisterType = N'OT'))
              OR (policy_rule.Mode = 3 AND EXISTS (SELECT 1 FROM dbo.F03OTRegistrarPositions x WHERE x.RegistrationRuleId = policy_rule.Id AND x.PositionCode = creator_info.PositionCode AND x.RegisterType = N'OT'))
              /* Feature Operator OT.RegistrarManage (3095): chỉ định theo phòng ban / khối (Block:{code}) / toàn công ty */
              OR EXISTS
              (
                  SELECT 1
                  FROM dbo.F03FeatureOperatorAssignments fa
                  WHERE fa.IsActive = 1
                    AND fa.FunctionCode = 3095
                    AND fa.ResourceType = N'OT_REGISTRAR'
                    AND UPPER(LTRIM(RTRIM(fa.EmployeeCode))) = UPPER(LTRIM(RTRIM(ISNULL(q.EmployeeCode, N''))))
                    AND (
                          fa.ResourceId = ISNULL(q.DeptCode, 0)
                          OR (fa.ResourceId IS NULL AND
                              (   ISNULL(fa.ScopeCode, N'') IN (N'', N'All')
                               OR fa.ScopeCode = N'Block:' + ISNULL((SELECT TOP (1) d.BlockCode FROM dbo.F03Departments d WHERE d.DeptCode = ISNULL(q.DeptCode, 0)), N'#')))
                        )
              )
          )
    )
    BEGIN
        THROW 51531, N'Người đăng ký không có quyền đăng ký OT cho nhân viên khác theo policy của phòng ban.', 1;
    END;
END;
GO
