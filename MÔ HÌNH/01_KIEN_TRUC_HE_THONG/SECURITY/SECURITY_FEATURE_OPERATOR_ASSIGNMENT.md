# Feature Operator Assignment Security

## Purpose

F03FeatureOperatorAssignments defines who is allowed to operate a concrete application resource after RBAC has granted the capability.

It is intentionally separate from:
- F03Functions / F03RoleFunctions: capability (what can be done).
- F03ManagedScopes: organizational data scope.
- F03ApprovalPolicies / F03Approvers: approval workflow.
- F03PublicFormAudiences: who may submit a public form.

## Supported resources

| Function | ResourceType | ResourceId |
|---|---|---|
| 2801 PublicInformation.Manage | PUBLIC_INFORMATION | F03PublicInformation.Id |
| 2802 Execution.Review | EXECUTION_REVIEW | NULL (module-level operator) |
| 2807 PublicForm.Manage | PUBLIC_FORM | F03PublicForms.Id |
| 2808 PublicForm.SubmissionView | PUBLIC_FORM | F03PublicForms.Id |
| 2809 PublicForm.Export | PUBLIC_FORM | F03PublicForms.Id |
| 3110 Endpoint.SoftwareCatalogManage | ENDPOINT_SOFTWARE_CATALOG | NULL (global operator) |

## Authorization order

1. The user must have the function capability through RBAC.
2. If resource-specific operator assignments exist, the user's HRM EmployeeCode must be assigned to that resource.
3. Otherwise, a global assignment (ResourceId IS NULL) is checked.
4. If no assignment exists at either level, existing RBAC + scope behavior remains effective for backward compatibility.

Employee identity, department and position are resolved from HRM; the assignment table stores only EmployeeCode.

## Examples

### HR feedback
Assign E0005 and E0012 to 2802 / EXECUTION_REVIEW / NULL. Both employees can process HR feedback only if they also have Execution.Review.

### Public Information
Assign E0005 to 2801 / PUBLIC_INFORMATION / 15. Only E0005 can edit/publish/archive information record 15 when resource assignment is configured.

### Public Form
The audience answers who can register. Operator assignment answers who can manage the form/submissions.

Example: Audience = AllCompany; 2807 E0005 manages form 12; 2808 E0005 views submissions for form 12; 2809 E0012 exports submissions for form 12.

### Endpoint Software Catalog
Assign E0015 to 3110 / ENDPOINT_SOFTWARE_CATALOG / NULL. E0015 must still have the 3110 RBAC capability. When at least one global operator is configured, only assigned employees may create/import Software Catalog drafts.

## Security Center
The Security Center exposes operator assignment with HRM employee lookup showing EmployeeCode — EmployeeName — DeptCode — PositionCode.
No employee name, department name, position name or email supplied by the client is trusted.

## Chuẩn chung cho Policy / Rule cấu hình

Feature Operator Assignment là lớp phân công người thao tác cho các cấu hình quản trị có tác động nghiệp vụ. Mô hình thống nhất:

**RBAC capability + ManagedScope (nếu có) + Feature Operator Assignment (nếu feature đã được cấu hình operator).**

Các policy/rule cấu hình cấp module hiện dùng chung catalog:

| Function | ResourceType | Phạm vi |
|---|---|---|
| 3073 Execution.PolicyManage | EXECUTION_POLICY | Toàn module |
| 3071 ApprovalPolicy.Manage | APPROVAL_POLICY | Toàn module |
| 3061 OTLimit.Manage | OT_LIMIT_RULE | Toàn module |
| 3042 WorkCalendar.Manage | WORK_CALENDAR | Toàn module |
| 3072 HrmUserRoleRule.Manage | HRM_USER_ROLE_RULE | Toàn module |

Các feature đã dùng operator assignment trước đó tiếp tục giữ nguyên cơ chế resource-specific/global.

Nguyên tắc mở rộng: khi thêm một policy/rule quản trị mới, không tạo cơ chế phân quyền riêng. Function mới phải được khai báo trong FeatureOperatorCatalog, chọn ResourceType, xác định ResourceScoped, sau đó endpoint quản trị phải kiểm tra đồng thời capability RBAC và CanOperateAsync. UI Security Center lấy cùng catalog để người quản trị chỉ định nhân viên.

Đối với policy cấp module, ResourceId luôn để trống. Khi chưa có assignment cho feature, hệ thống giữ hành vi tương thích hiện tại (RBAC + scope); khi đã có assignment, chỉ nhân viên được chỉ định mới được thao tác. Điều này cho phép triển khai dần mà không khóa các hệ thống đang chạy.
