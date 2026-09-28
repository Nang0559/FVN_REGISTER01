# Chuẩn hóa Endpoint Governance — Software / Windows Service

## 1. Mục tiêu

FVN-REGISTER quản lý hai lớp dữ liệu độc lập nhưng liên kết:

1. **Inventory thực tế** từ Windows Agent.
2. **Danh mục được phép** đã qua IT Security Review và Approval.

Inventory không được tự động làm thay đổi Allowlist.

## 2. Phần mềm đã Approved

```text
Người dùng → Install Request → load Active Software Catalog
→ chọn phần mềm → snapshot Allowlist Version
→ Approval cấp nghiệp vụ → Approved
→ Agent xác nhận Installed → Compliance
```

`Approved` của Install Request không đồng nghĩa `Installed`.

## 3. Phần mềm chưa có trong danh mục

```text
New Software Request → IT Security Review
→ Catalog Change Request → Snapshot → Approval Engine hiện tại
→ Approved → Active Allowlist → Install Request
```

IT Security Review không bypass Business Approval.

## 4. Windows Service

Service dùng governance tương tự nhưng catalog riêng: Service Catalog → Security Review → Approval → Active Service Allowlist → Agent Inventory → Compliance.

## 5. Approval Snapshot

Tối thiểu gồm request type/id, thiết bị/asset/employee, catalog id/version, tên software/service, publisher, version rule, scope, action, lý do, IT Security Review, evidence, requester và thời điểm tạo. Approver xem snapshot tại thời điểm yêu cầu.

## 6. Versioning

Allowlist có version logic. Install Request lưu AllowlistVersion đã sử dụng.

## 7. Inventory và cảnh báo

```text
Approved Allowlist + Actual Agent Inventory → Compliance
```

Kết quả gồm Compliant, NonCompliant, Unknown, AgentOffline. ComputerName thay đổi tạo Identity History/Review; không đổi DeviceKey tự động.

## 8. Capability

Các capability chuẩn:

```text
Endpoint.View
Endpoint.Inventory.View
Endpoint.Compliance.View
Endpoint.Alert.View
Endpoint.Alert.Resolve
Endpoint.Provision
Endpoint.Credential.Rotate
Endpoint.Credential.Revoke
Endpoint.SoftwareCatalog.View
Endpoint.SoftwareCatalog.Manage
Endpoint.SoftwarePolicy.Submit
Endpoint.SoftwarePolicy.Approve
Endpoint.SoftwareSecurityReview
Endpoint.ServiceCatalog.View
Endpoint.ServiceCatalog.Manage
Endpoint.ServicePolicy.Submit
Endpoint.ServicePolicy.Approve
Endpoint.ServiceSecurityReview
Endpoint.InstallRequest.Create
Endpoint.InstallRequest.View
Endpoint.InstallRequest.Approve
Endpoint.Exception.Create
Endpoint.Exception.View
Endpoint.Exception.Approve
```

Capability phải được đăng ký trong Security Center hiện tại và kiểm tra server-side theo scope; không hard-code theo role.

## 9. Phân tách trách nhiệm

**Employee:** xem catalog được phép, tạo Install Request, cung cấp mục đích/evidence.

**IT Operator:** inventory, alert, provision/rotate/revoke theo assignment.

**IT Security:** security review, catalog change, exception theo quyền.

**Approver:** approval theo ApprovalPolicy hiện tại.

**Superadmin:** capability/operator/credential governance; không thay business approval.

## 10. Không tạo Approval Engine thứ hai

Endpoint chỉ lưu ApprovalCaseId và snapshot. Route, level, approver, approve/reject dùng Approval Engine hiện tại.

## 11. Không tự động tin Agent

`Agent says Installed` ≠ `System says Allowed`. Compliance Engine quyết định dựa trên policy hiệu lực.

## 12. Không auto-add software

Software lạ: `Inventory → NonCompliant/Review → IT Review → Software Change Request → Approval → Allowlist mới`.

## 13. Trạng thái chính

Catalog: `Draft → SecurityReview → PendingApproval → Approved → Retired`.

Install Request: `Draft → SecurityReview? → PendingApproval → Approved → PendingInstallation → Installed`.

Identity Review: `Pending → Confirmed | Rejected | Merged`.

## 14. Nguyên tắc dữ liệu

Current inventory có thể cập nhật; Approval Snapshot không sửa; Audit history không overwrite; Allowlist Active chỉ đổi qua workflow approved; Install Request giữ AllowlistVersion; Alert idempotent.
