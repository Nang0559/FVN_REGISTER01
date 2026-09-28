# Checklist triển khai Endpoint Governance

Tài liệu này là gate trước khi coi tính năng hoàn chỉnh. Mục nào chưa đạt không được bật policy deny diện rộng.

## A. Approval Engine

- [ ] Endpoint dùng provider/module của Approval Engine hiện tại.
- [ ] Software Catalog Change, Service Catalog Change, Install Request và Exception tạo đúng ApprovalCase.
- [ ] Route lấy từ F03ApprovalPolicies, không hard-code cấp duyệt.
- [ ] Approver xem immutable snapshot.
- [ ] Approve/reject callback transaction-safe.
- [ ] Không tạo bảng/engine approval thứ hai.

## B. Software Catalog

- [ ] Draft không dùng cho Compliance.
- [ ] Chỉ Active version mới được đối chiếu.
- [ ] Mỗi thay đổi tạo version.
- [ ] Snapshot before/after.
- [ ] Publisher/version rule/scope đầy đủ.

## C. Service Catalog

- [ ] ServiceName là định danh kỹ thuật.
- [ ] AllowedState/AllowedStartMode/Scope/Version.
- [ ] Approval snapshot.

## D. Install Request

- [ ] UI chỉ load Active Allowlist.
- [ ] Request lưu CatalogVersion.
- [ ] Submit tạo ApprovalCase.
- [ ] Employee không sửa catalog.
- [ ] Approved không coi là Installed.
- [ ] Agent inventory mới xác nhận Installed.

## E. New Software

- [ ] Employee tạo New Software Request.
- [ ] IT Security Review.
- [ ] Reject giữ nguyên catalog.
- [ ] Accept tạo Catalog Change Request.
- [ ] Catalog Change phải Approval.
- [ ] Sau Active mới cho Install Request tiếp tục.

## F. Agent

- [ ] Credential riêng từng máy.
- [ ] Server resolve DeviceKey từ credential.
- [ ] DPAPI bảo vệ secret.
- [ ] HTTPS.
- [ ] Không remote command execution.
- [ ] Software/service inventory idempotent.

## G. Compliance

- [ ] Allowed software → Compliant.
- [ ] Unknown software → NonCompliant + Alert theo policy.
- [ ] Active exception → không tạo violation tương ứng.
- [ ] Agent offline → AgentOffline/Unknown theo policy.
- [ ] Chưa có inventory → không tự suy diễn NonCompliant.

## H. Identity

- [ ] ComputerName change có history/alert.
- [ ] Hardware identity change → PendingReview.
- [ ] Không tự merge máy khi hardware identity thay đổi.
- [ ] Chuyển người sử dụng Equipment không đổi DeviceKey.

## I. Security Center

- [ ] Endpoint capabilities được registry.
- [ ] Không trùng FunctionCode.
- [ ] Server-side capability check.
- [ ] Employee/IT/Approver scope đúng assignment/policy.
- [ ] Superadmin capability riêng.

## J. Production gate

- [ ] Unit tests.
- [ ] Integration tests.
- [ ] Approval end-to-end test.
- [ ] Agent pilot 5–10 máy.
- [ ] Offline/reconnect test.
- [ ] Credential revoke test.
- [ ] Duplicate inventory test.
- [ ] Unauthorized software test.
- [ ] Device rename test.
- [ ] Hardware replacement test.
- [ ] Rollback catalog version test.
- [ ] Audit verification.
- [ ] Chỉ sau khi tất cả đạt mới bật enforcement diện rộng.
