# Checklist nghiệm thu Endpoint Inventory & Compliance

## A. Database

- [ ] `54_Endpoint_Inventory_Compliance.sql` đã chạy.
- [ ] `55_Endpoint_Credentials.sql` đã chạy.
- [ ] `56_Endpoint_Governance.sql` đã chạy sau 54/55.
- [ ] Không duplicate DeviceKey.
- [ ] Credential chỉ lưu hash.
- [ ] Approval snapshot JSON hợp lệ.

## B. Machine identity

- [ ] Mỗi máy có DeviceKey và credential riêng.
- [ ] Credential cũ bị revoke khi rotate/provision lại.
- [ ] API lấy DeviceKey từ credential.
- [ ] Đổi ComputerName tạo Identity History + Alert.
- [ ] Hardware identity change tạo Identity Review.
- [ ] Không tự merge hardware mới.

## C. Inventory

- [ ] Software inventory từ Agent.
- [ ] Windows Service inventory từ Agent.
- [ ] Current snapshot transaction-safe.
- [ ] Inventory hash/fingerprint nhận biết payload không đổi.
- [ ] Agent không tự gán EmployeeCode/EquipmentAssetId.

## D. Software governance

- [ ] Catalog chỉ Active sau Security Review + Approval.
- [ ] Allowlist có version logic.
- [ ] Install Request tự load catalog Approved.
- [ ] Install Request lưu AllowlistVersion.
- [ ] Software mới chuyển Security Review.
- [ ] Agent phát hiện software lạ không tự thêm vào Allowlist.

## E. Service governance

- [ ] Service Catalog độc lập với Software Catalog.
- [ ] Có RequiredState/RequiredStartMode.
- [ ] Service policy có Security Review + Approval.

## F. Approval

- [ ] Dùng Approval Engine hiện tại.
- [ ] Không có Approval Engine thứ hai.
- [ ] Snapshot bất biến.
- [ ] Approver xem Before/After hoặc Proposed Version đầy đủ.
- [ ] ApprovalCaseId liên kết với request.
- [ ] Reject không làm Active policy thay đổi.
- [ ] Chỉ Approved mới được Active.

## G. Security Center

- [ ] Endpoint capabilities đã registry.
- [ ] Capability server-side và theo scope.
- [ ] Employee scope chỉ endpoint/equipment được phép.
- [ ] IT scope theo assignment.
- [ ] Approver scope theo ApprovalPolicy.
- [ ] Superadmin capability riêng.

## H. Pilot

- [ ] 5–10 máy IT.
- [ ] Đổi tên thử nghiệm tạo alert.
- [ ] Revoke credential.
- [ ] Credential PC-A không gửi inventory cho PC-B.
- [ ] Allowlist → Compliant sau inventory.
- [ ] Software ngoài allowlist → NonCompliant/Alert.
- [ ] Software mới → Security Review → Approval → Active.
- [ ] Install Request dùng catalog mới.
- [ ] Sau cài đặt Agent xác nhận Installed.

## I. Enforcement

Giai đoạn đầu chạy `Monitor` để phát hiện false positive. Chỉ sau khi catalog/policy ổn định mới chuyển từng scope sang enforcement.
