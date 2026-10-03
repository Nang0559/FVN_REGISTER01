# FVN-REGISTER — Chuẩn hóa Governance Software / Windows Service / Endpoint

## 1. Nguyên tắc

Endpoint Agent chỉ cung cấp dữ liệu thực tế. Agent không tự thêm software/service vào allowlist, không tự đổi Equipment owner/DeviceKey, không tự phê duyệt policy và không nhận arbitrary command từ server.

Approval dùng Approval Engine hiện tại của FVN-REGISTER.

## 2. Ba lớp dữ liệu

- **Inventory thực tế:** Windows Agent.
- **Catalog/Allowlist hiệu lực:** IT Security quản lý và chỉ active sau Approval.
- **Request nghiệp vụ:** Install Request, Catalog Change Request, Service Change Request, Exception Request.

Ba lớp không ghi đè lẫn nhau.

## 3. Software đã Allowlist

`Employee → Install Request → Active Software Catalog → AllowlistVersion + rule snapshot → Approval → Approved → cài đặt → Agent → Compliance`.

Approved không đồng nghĩa Installed.

## 4. Software chưa Allowlist

`New Software Request → IT Security Review → Catalog Change Request → Snapshot before/after → Approval Engine → Approved → Active Catalog → Install Request`.

IT Security Review không bypass Approval.

## 5. Software phát hiện từ Agent

`Agent Inventory → Compliance → Alert`.

Nếu muốn cho phép: `IT → Catalog Change → Approval → Active`. Không auto-promote inventory thành allowed software.

## 6. Service Governance

Catalog riêng, với ServiceName, DisplayName, Publisher nếu có, AllowedStartMode, AllowedState, Scope, EffectiveFrom/To, Status, ApprovalCaseId và Version.

## 7. Snapshot

Catalog change snapshot gồm RequestId, RequestType, CatalogVersionBefore/After, Scope, ChangedItems, OldValue, NewValue, Reason, SecurityReview, Requester, Department, CreatedAtUtc và evidence.

Install Request snapshot gồm RequestId, Endpoint/Equipment, Employee, SoftwareCatalogId, SoftwareVersionRule, CatalogVersion, Purpose, ITReviewResult, Evidence và CreatedAtUtc.

Sau submit, thay đổi catalog không làm thay đổi snapshot.

## 8. State machine

Catalog Change: `Draft → SecurityReview → PendingApproval → Approved → Active`.

Install Request: `Draft → PendingApproval → Approved → PendingInstallation → Installed → ComplianceVerified`.

Kết thúc lỗi: `Rejected`, `Cancelled`, `InstallationFailed`, `Expired`.

## 9. Compliance

`Compliant`, `NonCompliant`, `Unknown`, `AgentOffline`, `UnmanagedDevice`, `PendingReview`.

Unknown không tự chuyển NonCompliant chỉ vì Agent chưa gửi dữ liệu.

## 10. Identity change

ComputerName chỉ là thuộc tính. Đổi tên tạo history/alert. Hardware identity bất thường chuyển PendingReview; không tự merge.

## 11. Phân quyền

**Employee:** xem catalog Active, tạo request, không sửa catalog/resolve security alert.

**IT Operator:** inventory, alert, provision/rotate/revoke theo assignment.

**IT Security:** catalog draft, security review, catalog change, exception theo quyền.

**Approver:** approve/reject theo Approval Policy.

**Superadmin:** capability/role/function và credential governance; không thay business approval.

Capability phải đăng ký trong Security Center hiện tại; không hard-code role name.

## 12. Audit

Audit provision/rotate/revoke, endpoint bind/unbind, catalog create/edit/submit/approve/reject/activate, security review, install request lifecycle, compliance changes, alert và exception lifecycle.

## 13. Versioning

Catalog active có version tăng tuần tự. Install Request tham chiếu version đã dùng; Approval snapshot giữ nguyên version đó.

## 14. Acceptance criteria

Employee chỉ thấy software/service Active; software mới qua IT Security Review; catalog change qua Approval Engine; snapshot immutable; approver thấy before/after và evidence; Agent không mutate catalog; software lạ sinh Compliance/Alert; Installed chỉ được xác nhận bởi Agent; đổi tên máy tạo identity history; credential xác định máy; không phụ thuộc AD.
