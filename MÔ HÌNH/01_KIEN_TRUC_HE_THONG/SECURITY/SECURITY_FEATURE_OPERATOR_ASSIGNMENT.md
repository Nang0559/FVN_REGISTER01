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
| 3044 WorkCalendar.SymbolRuleManage | ATTENDANCE_SYMBOL_RULE | NULL (module-level operator) |

## Authorization order

1. The user must have the function capability through the canonical IAuthorizationService.HasAsync RBAC path. Feature Operator Assignment never grants RBAC.
2. When an operator assignment is configured, the user's HRM EmployeeCode must be assigned to that resource/module.
3. Otherwise, a global assignment (ResourceId IS NULL) is checked where the resource type supports module-wide operators.
4. If no assignment exists at either level, the feature keeps its existing RBAC + data-scope behavior for backward compatibility.
5. Runtime service methods must repeat the same authorization rule; controller checks are not considered sufficient.
6. Assignment creation is rejected when the target employee has no active user account or does not currently have the effective RBAC capability. The error is: "Nhân viên {EmployeeCode} chưa có RBAC cho {FunctionKey}. Hãy cấp capability trước khi chỉ định operator."

Employee identity, department and position are resolved from HRM; the assignment table stores only EmployeeCode. RBAC is resolved centrally so active-role/lifecycle rules cannot diverge between Security Center and runtime authorization.

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

Các policy/rule cấu hình cấp module và feature quản trị có phân công operator hiện dùng chung catalog:

| Function | ResourceType | Phạm vi / mục đích |
|---|---|---|
| 3073 Execution.PolicyManage | EXECUTION_POLICY | Toàn module — chính sách đối soát công |
| 3071 ApprovalPolicy.Manage | APPROVAL_POLICY | Toàn module — chính sách phê duyệt |
| 3061 OTLimit.Manage | OT_LIMIT_RULE | Toàn module — quy tắc giới hạn OT |
| 3042 WorkCalendar.Manage | WORK_CALENDAR | Toàn module — cấu hình lịch làm việc |
| 3044 WorkCalendar.SymbolRuleManage | ATTENDANCE_SYMBOL_RULE | Toàn module — rule ký hiệu chấm công |
| 3072 HrmUserRoleRule.Manage | HRM_USER_ROLE_RULE | Toàn module — quy tắc vai trò người dùng HRM |
| 2802 Execution.Review | EXECUTION_REVIEW | Toàn module — tiếp nhận/xử lý phản hồi đối soát của nhân viên |

`Execution.Review` là **feature xử lý phản hồi**, không phải policy cấu hình; nó được đưa vào cùng bảng để thể hiện đầy đủ các feature module-wide đang dùng chung cơ chế Feature Operator Assignment. Người được chỉ định vẫn phải có capability `Execution.Review` qua RBAC.

Các feature đã dùng operator assignment trước đó tiếp tục giữ nguyên cơ chế resource-specific/global.

Nguyên tắc mở rộng: khi thêm một policy/rule quản trị mới, không tạo cơ chế phân quyền riêng. Function mới phải được khai báo trong FeatureOperatorCatalog, chọn ResourceType, xác định ResourceScoped, sau đó endpoint quản trị phải kiểm tra đồng thời capability RBAC và CanOperateAsync. UI Security Center lấy cùng catalog để người quản trị chỉ định nhân viên.

Đối với policy cấp module, ResourceId luôn để trống. Khi chưa có assignment cho feature, hệ thống giữ hành vi tương thích hiện tại (RBAC + scope); khi đã có assignment, chỉ nhân viên được chỉ định mới được thao tác. Điều này cho phép triển khai dần mà không khóa các hệ thống đang chạy.

## Calendar Symbol Rule governance

`ATTENDANCE_SYMBOL_RULE` là configuration resource cấp module. Rule thay đổi cách Calendar diễn giải kết quả attendance thành symbol, nhưng không thay đổi HRM attendance calculation.

Current implementation note: the Symbol Rule endpoints do **not** perform a separate `F03ManagedScopes` check. Therefore this document does not claim ManagedScope enforcement for Symbol Rule. Capability authorization is centralized in the authorization layer and Feature Operator Assignment is enforced by the operator-aware service/controller path. If Symbol Rule is later made department-scoped, that change must add an explicit `CanAccessAsync`/ManagedScope check and update this section in the same change.

Current governance:
1. Có `WorkCalendar.SymbolRuleManage` qua RBAC.
2. Khi operator assignment được cấu hình, người được chỉ định cũng phải có capability RBAC tương ứng.
3. Thay đổi Rule qua UI; không sửa trực tiếp symbol trong Calendar source code.
4. Dùng Test Rule trước khi Active.
5. Audit actor, thời điểm, rule và trạng thái trước/sau.

Security Center nên cảnh báo assignment mà nhân viên đã inactive hoặc capability RBAC hiện tại đã bị thu hồi. Assignment cũ không tự động bị xóa; phải được remediation.

Operator Assignment không thay thế RBAC và không cấp quyền Approve/HR Resolution.