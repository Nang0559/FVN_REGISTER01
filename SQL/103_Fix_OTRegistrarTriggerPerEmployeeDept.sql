

/*
    Hotfix lỗi 51531 khi đăng ký OT ("Người đăng ký không có quyền đăng ký OT cho nhân viên khác theo policy của phòng ban").

    Nguyên nhân: trigger dbo.trg_F03OTEmployees_RegistrarPolicy xét quyền theo q.DeptCode (phòng ban của ĐƠN = phòng ban
    của người tạo, do OTCreate.razor luôn gán DeptCode = phòng của user đăng nhập), trong khi API (OtRegistrationPolicy.
    CanRegisterForEmployeesAsync) xét theo phòng ban CỦA TỪNG NHÂN VIÊN. Người được chỉ định (Feature Operator
    OT.RegistrarManage / rule phòng ban khác) đăng ký cho nhân viên phòng khác thì API cho qua nhưng trigger chặn -> 500.

    Cách sửa: trigger xét theo phòng ban của từng nhân viên trong đơn (F03Employees.DeptCode -> i.DeptCode -> q.DeptCode),
    cùng logic với OtRegistrationPolicy. Vẫn chỉ kiểm tra khi INSERT hoặc đổi EmployeeCode/OTRequestId. Idempotent.
*/
SET NOCOUNT ON;
GO
CREATE OR ALTER TRIGGER dbo.trg_F03OTEmployees_RegistrarPolicy
ON dbo.F03OTEmployees
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT (UPDATE(EmployeeCode) OR UPDATE(OTRequestId)) RETURN;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.F03OTRequests q ON q.Id = i.OTRequestId
        OUTER APPLY
        (
            SELECT TOP (1) u.EmployeeCode, e.PositionCode
            FROM dbo.F03Users u
            LEFT JOIN dbo.F03Employees e ON e.EmployeeCode = u.EmployeeCode
            WHERE u.Id = q.CreatedBy
        ) creator_info
        /* Phòng ban dùng để xét policy = phòng ban của chính nhân viên được đăng ký (khớp OtRegistrationPolicy). */
        OUTER APPLY
        (
            SELECT COALESCE((SELECT TOP (1) em.DeptCode FROM dbo.F03Employees em WHERE em.EmployeeCode = i.EmployeeCode),
                            i.DeptCode, q.DeptCode, 0) AS DeptCode
        ) emp_dept
        OUTER APPLY
        (
            SELECT TOP (1) rr.*
            FROM dbo.F03OTRegistrationRules rr
            WHERE rr.IsActive = 1
              AND rr.RegisterType = N'OT'
              AND rr.DepartmentCode IN (emp_dept.DeptCode, 0)
            ORDER BY CASE WHEN rr.DepartmentCode = emp_dept.DeptCode THEN 0 ELSE 1 END
        ) policy_rule
        WHERE UPPER(LTRIM(RTRIM(i.EmployeeCode))) <> UPPER(LTRIM(RTRIM(ISNULL(creator_info.EmployeeCode, N''))))
          AND NOT
          (
              policy_rule.Mode = 1
              OR (policy_rule.Mode = 2 AND EXISTS (SELECT 1 FROM dbo.F03OTRegistrars x WHERE x.RegistrationRuleId = policy_rule.Id AND x.UserId = q.CreatedBy AND x.RegisterType = N'OT'))
              OR (policy_rule.Mode = 3 AND EXISTS (SELECT 1 FROM dbo.F03OTRegistrarPositions x WHERE x.RegistrationRuleId = policy_rule.Id AND x.PositionCode = creator_info.PositionCode AND x.RegisterType = N'OT'))
              /* Feature Operator OT.RegistrarManage (3095): theo phòng ban / khối (Block:{code}) / toàn công ty */
              OR EXISTS
              (
                  SELECT 1
                  FROM dbo.F03FeatureOperatorAssignments fa
                  WHERE fa.IsActive = 1
                    AND fa.FunctionCode = 3095
                    AND fa.ResourceType = N'OT_REGISTRAR'
                    AND fa.EmployeeCode = creator_info.EmployeeCode
                    AND (
                          fa.ResourceId = emp_dept.DeptCode
                          OR (fa.ResourceId IS NULL AND
                              (   ISNULL(fa.ScopeCode, N'') IN (N'', N'All')
                               OR fa.ScopeCode = N'Block:' + ISNULL((SELECT TOP (1) d.BlockCode FROM dbo.F03Departments d WHERE d.DeptCode = emp_dept.DeptCode), N'#')))
                        )
              )
          )
    )
    BEGIN
        THROW 51531, N'Người đăng ký không có quyền đăng ký OT cho nhân viên khác theo policy của phòng ban.', 1;
    END;
END;
GO
