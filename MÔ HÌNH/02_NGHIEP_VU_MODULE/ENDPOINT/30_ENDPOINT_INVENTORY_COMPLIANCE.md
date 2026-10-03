# FVN-REGISTER — Windows Endpoint Inventory & Compliance

> **Tài liệu tổng thể của tính năng Endpoint Inventory & Compliance.**
> Không tạo tài liệu kiến trúc riêng cho từng phần nhỏ. Các tài liệu `31–39` là tài liệu chuyên đề/triển khai bổ trợ và phải tuân theo tài liệu này.

## 1. Mục tiêu

Quản lý tập trung máy Windows, phần mềm và Windows Service thực tế trong mạng nội bộ; đối chiếu với danh sách phần mềm/service được IT phê duyệt; phát hiện sai lệch và tạo yêu cầu xử lý/ngoại lệ theo Approval Engine dùng chung của FVN-REGISTER.

Thiết kế **không phụ thuộc Active Directory** và không phụ thuộc quyền quản trị Microsoft Defender của tập đoàn.

## 2. Vị trí của Endpoint trong FVN-REGISTER

Endpoint là một module quản trị dữ liệu và compliance, không phải một hệ thống Approval riêng:

```text
Windows Endpoint Agent
        │
        │ HTTPS API
        ▼
FVN-REGISTER API
        │
        ├── Endpoint Inventory
        ├── Equipment / Device
        ├── Software & Service Catalog
        ├── Compliance
        ├── Alert / Exception
        │
        └── Approval Engine dùng chung
                    │
                    ├── Approval Policy
                    ├── Approval Route
                    ├── Snapshot / Evidence
                    ├── Notification
                    └── Audit
```

Leave / OT / Trip và các module nghiệp vụ hiện hữu **không thay đổi business flow**. Endpoint chỉ tích hợp vào cơ chế Approval/Security dùng chung.

## 3. Nguyên tắc kiến trúc

1. Windows Agent là nguồn inventory chính cho endpoint được quản lý.
2. FVN Server không dùng tài khoản Administrator để quét WMI/SMB toàn mạng.
3. Network Discovery chỉ hỗ trợ phát hiện thiết bị chưa quản lý; không phải nguồn chính của software inventory.
4. Agent không được nhận arbitrary PowerShell/CMD từ server.
5. Agent không truy cập database trực tiếp.
6. Inventory thực tế **không tự động trở thành Allowlist**.
7. Allowlist chỉ có hiệu lực sau Security Review/Approval theo policy.
8. Không tự động xóa/disable software/service khi phát hiện vi phạm nếu chưa có policy riêng được phê duyệt.
9. Exception phải có lý do, phạm vi, thời hạn và Approval.
10. Mọi thay đổi chính sách phải có version/snapshot và audit.
11. Quyền phải kiểm tra server-side bằng Security Center/capability hiện hữu.
12. Không tạo `EndpointApprovalEngine`, `EndpointNotificationEngine` hoặc hệ thống permission/audit song song nếu FVN-REGISTER đã có service dùng chung.

## 4. Luồng vận hành tổng thể

### 4.1. Inventory

```text
Windows Agent
 → Heartbeat
 → Device Inventory
 → Software Inventory
 → Windows Service Inventory
 → FVN API
 → Normalize/Reconcile
 → Inventory hiện tại + lịch sử
```

### 4.2. Allowlist

```text
Draft
 → IT Security Review
 → Submit
 → Approval Engine chung
 → Approved
 → Active Catalog/Policy
```

Software/service phát hiện từ Agent chỉ là **thực tế đang tồn tại**, không phải bằng chứng rằng nó được phép.

### 4.3. Yêu cầu cài phần mềm

Nếu software đã được phép:

```text
Employee
 → chọn từ Active Software Catalog
 → tạo Install Request
 → snapshot catalog version
 → Approval chung
 → Approved
 → Agent/IT thực hiện cài đặt
 → Agent inventory xác nhận
 → Compliance cập nhật
```

Nếu software chưa có trong danh sách:

```text
Employee
 → yêu cầu software mới
 → IT Security Review
 → đánh giá an toàn + phạm vi
 → thay đổi Software Catalog
 → Approval chung
 → Active Catalog
 → quay lại Install Request
```

Không cho người dùng bypass Security Review bằng cách nhập một tên phần mềm tự do vào Install Request.

### 4.4. Service

Windows Service sử dụng cùng mô hình Software:

```text
Service Request/Policy Change
 → Security Review
 → Approval chung
 → Active Service Catalog
 → Agent Inventory
 → Compliance
```

### 4.5. Compliance

```text
Inventory thực tế
       ↓
Normalize
       ↓
Active Policy + Exception
       ↓
Compliance Engine
       ↓
Compliant / Unknown / NonCompliant
       ↓
Alert / Task / Exception Request
```

Các trường hợp tối thiểu:

- software ngoài Allowlist;
- service ngoài Allowlist;
- version không phù hợp policy;
- computer name/device identity thay đổi;
- endpoint mất heartbeat;
- endpoint chưa được quản lý;
- exception hết hạn.

## 5. Device identity

Thiết bị phải có identity ổn định dựa trên thông tin phần cứng/agent phù hợp. `ComputerName` chỉ là một thuộc tính có thể thay đổi.

Khi ComputerName thay đổi:

1. giữ lịch sử tên cũ;
2. ghi nhận identity change;
3. chạy reconciliation;
4. tạo cảnh báo/review theo policy;
5. không tự động tạo thiết bị mới chỉ vì hostname thay đổi nếu identity phần cứng/agent vẫn xác định được cùng endpoint.

## 6. Đối tượng dữ liệu

### Endpoint

- DeviceKey
- ComputerName
- SerialNumber
- OSName
- OSVersion
- User/EmployeeCode
- LastSeenUtc
- AgentVersion
- Status

### Software Inventory

- DeviceKey
- NormalizedName
- Publisher
- Version
- Architecture
- InstallDate
- InstallLocation
- DetectedAtUtc
- Source

### Service Inventory

- DeviceKey
- ServiceName
- DisplayName
- State
- StartMode
- BinaryPathHash
- DetectedAtUtc
- Source

Không lưu secret trong inventory.

### Policy / Catalog

- Policy/Catalog item
- ScopeType / ScopeKey
- Allow/Deny hoặc Allowed Version Rule
- EffectiveFrom / EffectiveTo
- Version
- ApprovalCaseId
- Status

### Exception

- PolicyId
- DeviceKey hoặc phạm vi
- Reason
- EffectiveFrom / EffectiveTo
- ApprovalCaseId
- Status
- Evidence

## 7. Approval dùng chung

Endpoint phải sử dụng:

`Application/Interfaces/Orchestrators/IApprovalWorkflowOrchestrator<TSubject>`

và các Approval Policy/Route/Snapshot/Evidence/Notification/Audit hiện hữu.

Không tạo workflow engine riêng.

Approver cần thấy được trong một approval case:

- trạng thái tổng thể hiện tại;
- Before/After;
- danh sách software/service thay đổi;
- catalog/policy version;
- endpoint/scope bị ảnh hưởng;
- requester;
- kết quả IT Security Review;
- evidence;
- lịch sử thay đổi liên quan.

Request đã tạo phải snapshot dữ liệu cần phê duyệt; catalog thay đổi sau đó không được âm thầm thay đổi nội dung request cũ.

## 8. Security Center

Capability Endpoint phải được đăng ký vào Security Center hiện hữu. Các API phải kiểm tra quyền server-side.

Nhóm capability tối thiểu:

- Endpoint Inventory View;
- Endpoint Compliance View;
- Endpoint Alert View/Resolve;
- Credential Provision/Revoke;
- Software Catalog View/Manage;
- Software Security Review;
- Software Policy Submit/Approve;
- Service Catalog View/Manage;
- Service Security Review;
- Service Policy Submit/Approve;
- Install Request Create/View/Approve;
- Exception Create/View/Approve.

Tên function/action và seed SQL phải được rà khớp với `SecurityCenter` thực tế trước khi migration.

## 9. Credential

Credential provisioning/revoke dùng `IEndpointCredentialService` hiện hữu trong Application. Secret không được lưu trong inventory hoặc log.

## 10. Network Discovery

V1 chỉ discovery tối thiểu:

- IP/hostname/device fingerprint;
- thiết bị không có FVN Agent;
- thiết bị đã biết nhưng chưa khai báo Equipment.

Discovery phải có:

- scope mạng;
- exclusion list;
- lịch chạy;
- audit;
- capability riêng;
- rate limit.

Không triển khai credentialed WMI/SMB scanning trong V1.

## 11. Microsoft Defender / 365

Nếu tập đoàn cấp quyền read-only sau này, Defender/Intune có thể trở thành **nguồn bổ sung để reconciliation**.

FVN-REGISTER không phụ thuộc Defender để vận hành Agent/Inventory/Compliance.

## 12. Audit và lịch sử

Phải phân biệt:

- inventory event;
- identity change;
- security review;
- catalog/policy change;
- approval action;
- install request;
- installation result;
- compliance result;
- alert/resolve;
- exception.

## 13. Tài liệu liên quan

- `31_ENDPOINT_INVENTORY_API_CONTRACT.md` — API/contract.
- `32_ENDPOINT_IMPLEMENTATION_PLAN.md` — kế hoạch implementation.
- `33_ENDPOINT_CREDENTIAL_SECURITY.md` — credential/security.
- `34_ENDPOINT_DEPLOYMENT_RUNBOOK_VI.md` — triển khai Agent/API.
- `35_ENDPOINT_DATA_MODEL_VI.md` — data model.
- `36_ENDPOINT_GOVERNANCE_STANDARD_VI.md` — governance.
- `37_ENDPOINT_ROLLOUT_CHECKLIST_VI.md` — rollout.
- `38_ENDPOINT_SOFTWARE_SERVICE_APPROVAL_STANDARD_VI.md` — Software/Service approval.
- `39_ENDPOINT_GOVERNANCE_IMPLEMENTATION_CHECKLIST_VI.md` — checklist implementation.

Các tài liệu trên **không được định nghĩa kiến trúc khác**; nếu có khác biệt, tài liệu `30_ENDPOINT_INVENTORY_COMPLIANCE.md` là tài liệu tổng thể cần được cập nhật cùng code.

## 14. Điều kiện hoàn thành

Chỉ coi tính năng hoàn thành khi:

- Agent heartbeat hoạt động;
- inventory software/service idempotent;
- device identity reconciliation hoạt động;
- catalog/policy có version;
- Security Review hoạt động;
- Approval dùng orchestrator chung;
- snapshot Before/After hoạt động;
- Install Request dùng snapshot catalog;
- software mới không bypass review;
- compliance cảnh báo sai lệch;
- exception có thời hạn;
- audit đầy đủ;
- Security Center kiểm soát server-side;
- không phụ thuộc AD;
- deployment/runbook đã cập nhật;
- test end-to-end đạt yêu cầu.


## 15. Equipment-first lifecycle (current)

Từ branch hiện tại, Equipment là business entry point của Endpoint Agent:

Equipment Asset → Endpoint Agent eligibility → Endpoint Device + DeviceKey → Credential (one-time secret) → Agent installation → Heartbeat + Inventory → Compliance + Alert.

### Eligibility
Không phải mọi Equipment đều là Endpoint. Chỉ tài sản có OS/Agent capability mới được bật EndpointAgentEligible. UI phải ẩn credential controls khi Equipment không eligible.

### Identity invariant
Employee identity ≠ Asset identity ≠ Endpoint identity.
- Employee: HRM/EmployeeCode.
- Asset: Equipment Asset/AssetCode/Id.
- Endpoint: DeviceKey + hardware/agent identity.

Đổi người sử dụng không tạo DeviceKey mới. Reinstall có thể đổi AgentInstallationId. Đổi ComputerName không tự tạo Endpoint mới. Thay đổi hardware identity cần IT review.

### Credential invariant
Credential chỉ xác thực machine; không cấp quyền cho user. Server hash secret, chỉ trả plaintext khi provision/rotate, audit provision/rotate/revoke và không ghi secret vào log/inventory.

### Software/service compliance
Inventory thực tế không đồng nghĩa Allowlist. Policy/Catalog/Exception vẫn đi qua Security Review + Approval Engine dùng chung.

## 16. Current implementation boundary
- Equipment UI: entry point và eligibility.
- Endpoint Credential service/controller: credential lifecycle.
- Endpoint Inventory service: heartbeat/inventory/device identity.
- Compliance service: policy result/alerts.
- Security Center: capability + scope.
- SQL 68_EndpointEquipmentAgentIntegration.sql: tích hợp Equipment ↔ Endpoint schema.

Không tạo EndpointApprovalEngine, EndpointNotificationEngine hoặc permission system riêng.
