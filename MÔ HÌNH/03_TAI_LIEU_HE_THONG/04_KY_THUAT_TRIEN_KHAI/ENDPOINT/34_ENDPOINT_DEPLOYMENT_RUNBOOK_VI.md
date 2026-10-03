# FVN-REGISTER — Hướng dẫn triển khai Windows Endpoint Inventory & Compliance

## 1. Phạm vi

Tính năng endpoint Windows không phụ thuộc Active Directory, Domain Controller, LDAP hay tài khoản domain.

```text
FVN Employee → Equipment Asset → Endpoint Device → Windows Agent
→ HTTPS + credential riêng → FVN API → Inventory → Policy → Compliance → Alert/Exception → Approval Engine hiện tại
```

Microsoft Defender/Intune nếu được tập đoàn cấp quyền chỉ là nguồn dữ liệu bổ sung, không phải dependency bắt buộc.

## 2. Định danh máy không dùng AD

Không dùng AD Object ID, Domain SID hoặc Computer Account làm khóa chính. Agent cung cấp AgentInstallationId, SerialNumber, HardwareUuid, ComputerName và OS metadata. FVN tạo DeviceKey nội bộ.

Credential xác định endpoint; DeviceKey lấy server-side; HardwareIdentity dùng để kiểm tra nhất quán; ComputerName chỉ là thuộc tính. Hardware identity thay đổi phải chuyển sang cần xác minh, không tự merge.

## 3. Chuẩn bị server

Chạy SQL theo thứ tự `54_Endpoint_Inventory_Compliance.sql`, `55_Endpoint_Credentials.sql`, `56_Endpoint_Governance.sql`.

Production yêu cầu HTTPS, rate limiting, audit provision/revoke/rotate, credential hash và inventory authentication bằng credential máy.

## 4. Provision

1. Tạo Equipment Asset nếu cần.
2. Provision endpoint bằng API quản trị.
3. FVN tạo DeviceKey và credential riêng, secret chỉ trả một lần.
4. Không ghi secret vào log/database.
5. Bảo vệ secret trên Windows bằng DPAPI.
6. Cài Agent và gửi inventory.
7. Server lấy DeviceKey từ credential, không tin scope trong payload.

## 5. Windows Agent

Agent chạy Windows Service và đọc installed software từ uninstall registry, Windows Services và metadata phần cứng/OS cơ bản.

Không yêu cầu Domain Account/AD/remote WMI/SMB credential. Không chạy arbitrary PowerShell/CMD nhận từ server.

```json
{
  "FVNEndpointAgent": {
    "ApiBaseUrl": "https://<fvn-api>",
    "DeviceKey": "<provisioned-device-key>",
    "ApiKeyProtected": "<DPAPI-protected-secret>",
    "IntervalMinutes": 30
  }
}
```

Không commit secret thật vào appsettings. Script cài đặt mẫu nằm tại `FVN_REGISTER.EndpointAgent/install-agent.ps1`.

## 6. Inventory

Mỗi complete snapshot gồm device metadata, software, Windows services và collection timestamp. Server upsert theo DeviceKey và cập nhật current snapshot transaction-safe; lịch sử compliance được giữ riêng.

## 7. Policy

```text
Draft → Snapshot → Approval Engine → Approved → Active
```

Software/service policy phải có mã, allow/deny, scope, hiệu lực và lịch sử thay đổi.

## 8. Compliance

`Compliant`, `NonCompliant`, `Unknown`, `AgentOffline`, `UnmanagedDevice`. Không chuyển Unknown thành NonCompliant chỉ vì thiếu dữ liệu.

Exception được xét trước violation.

## 9. Alert

Alert phải idempotent. Vi phạm lặp lại cập nhật LastDetectedAt thay vì tạo hàng loạt alert. Chỉ đóng khi inventory chứng minh hết vi phạm hoặc workflow xử lý cho phép đóng.

## 10. Exception và Approval

Endpoint dùng Approval Engine hiện tại và immutable snapshot gồm thiết bị, software/service, policy, lý do, thời hạn, requester và evidence. Approved exception mới được Compliance Engine coi là active.

## 11. Network Discovery

V1 không credentialed WMI/SMB scan. Discovery chỉ phát hiện unmanaged device/offline endpoint/chưa gắn Equipment Asset trong scope mạng đã cấu hình; phải có exclusion, rate limit, schedule và audit.

## 12. Defender/Intune

Không cần quyền quản trị Defender của tập đoàn để vận hành FVN Agent. Nếu có quyền read-only, Defender/Intune là nguồn bổ sung để reconcile, không thay DeviceKey.

## 13. Kiểm thử bắt buộc

- Hai máy không dùng chung credential.
- Revoke/expire credential chặn inventory.
- Credential máy A không thể gửi inventory cho B.
- Đổi ComputerName không tạo endpoint mới nếu identity còn hợp lệ.
- Snapshot gửi hai lần không duplicate.
- Software/service thay đổi được cập nhật.
- Inventory lỗi giữa chừng không xóa snapshot hợp lệ.
- Allow/Deny/Unknown/Offline/Exception cho kết quả compliance đúng.
- HTTPS, rate limit, audit và không remote command execution.

## 14. Rollout

Pilot 5–10 máy IT → một phòng ban → toàn bộ laptop/desktop → server/máy đặc thù → network discovery → Defender/Intune read-only nếu được cấp quyền.

Không bật Deny diện rộng ngay; trước tiên Monitor để phát hiện false positive.

## 15. Production gate

Agent identity ổn định; credential lifecycle; inventory idempotent; compliance tests; Approval/exception dùng engine hiện tại; audit; không phụ thuộc AD; không remote command; rollback/revoke; dashboard unmanaged/offline/non-compliant.


## 4. Equipment-first installation (current)

### Bước 1 — Equipment
Tạo/đăng ký Equipment Asset và hoàn tất approval nếu nghiệp vụ yêu cầu.

### Bước 2 — Endpoint Agent
Trong Sổ thiết bị → Endpoint Agent, kiểm tra OS family và EndpointAgentEligible. Chỉ người có capability phù hợp mới được bật eligibility.

### Bước 3 — Provision credential
Chọn Cấp credential. Server tạo/duy trì DeviceKey và credential riêng. Secret plaintext chỉ trả một lần.

### Bước 4 — Cài Agent
Dùng DeviceKey được trả về cho Equipment và nhập secret qua cơ chế bảo mật của installer; không đưa secret plaintext vào command-line/log. Installer lưu secret bảo vệ cục bộ bằng DPAPI.

### Bước 5 — Xác nhận
Quay lại Equipment → Endpoint Agent để kiểm tra AgentVersion, LastSeen, AgentInstallationId và credential status.

### Lifecycle sau triển khai
- Rotate credential khi cần thay secret.
- Revoke khi endpoint không còn được phép gửi inventory.
- Bàn giao Equipment không tạo DeviceKey mới.
- Reinstall có thể đổi AgentInstallationId; hardware identity được dùng để xác minh.

### Không dùng flow legacy
Không provision bằng một màn hình độc lập yêu cầu người vận hành tự nhập DeviceKey như business flow. Nếu cần technical endpoint API theo DeviceKey, đó là operation dành cho security/technical compatibility.

## 16. LANSCOPE Cat — bulk deployment

Topology: PC → HTTPS → LANSCOPE Client → Internet/LAN → IIS → FVN_REGISTER.API → SQL Server.

LANSCOPE is the distribution/execution engine; FVN REGISTER is the security/control plane.

The Golden Package must not contain a DeviceKey, API credential or reusable enrollment secret. Each LANSCOPE target receives one short-lived bootstrap token. FVN stores only its SHA-256 hash; the token expires after 20 minutes and is atomically consumed once.

Supported LANSCOPE fields: ClientId, ComputerName, IP, MAC, SerialNumber, WindowsUser, Domain, OU, Group, OS, Manufacturer, Model.

Configure LANSCOPE to copy the Golden Package plus the target bootstrap JSON, execute `install-agent.ps1` with HTTPS API URL and bootstrap path, and run it as **LocalSystem**. LANSCOPE controls rollout scheduling/retry/return codes. The installer creates/updates `FVNRegisterEndpointAgent` as LocalSystem.

After enrollment the Agent receives a DeviceKey and one-year credential, protects the credential with DPAPI LocalMachine and deletes the bootstrap file. Equipment remains the business identity; DeviceKey is the technical identity. FVN does not infer Department from LANSCOPE Group/OU.

Before mass rollout, pilot 2–3 PCs and verify token replay/expiry, distinct DeviceKey/credential, serial mismatch → `PendingReview`, credential-only inventory authentication, revoke blocking inventory, reinstall/handover behavior, and HTTPS forwarded-proto configuration.
