# FVN-REGISTER — Endpoint Requirement Traceability Matrix

> Ma trận đối chiếu requirement → code hiện tại. Đây là tài liệu traceability, không phải architecture mới.
>
> **Nguyên tắc:** ưu tiên thành phần dùng chung hiện có; chỉ đánh `MISSING` khi repository thực sự chưa có implementation/contract tương ứng. Equipment giữ nguyên domain hiện tại; Endpoint không tạo Approval Engine riêng.

## 1. Source of Truth đã rà

### Endpoint canonical

- `MÔ HÌNH/02_NGHIEP_VU_MODULE/ENDPOINT/30_ENDPOINT_INVENTORY_COMPLIANCE.md`
- `MÔ HÌNH/03_TAI_LIEU_HE_THONG/04_KY_THUAT_TRIEN_KHAI/ENDPOINT/31_ENDPOINT_INVENTORY_API_CONTRACT.md`
- `32_ENDPOINT_IMPLEMENTATION_PLAN.md`
- `33_ENDPOINT_CREDENTIAL_SECURITY.md`
- `34_ENDPOINT_DEPLOYMENT_RUNBOOK_VI.md`
- `35_ENDPOINT_DATA_MODEL_VI.md`
- `36_ENDPOINT_GOVERNANCE_STANDARD_VI.md`
- `37_ENDPOINT_ROLLOUT_CHECKLIST_VI.md`
- `38_ENDPOINT_SOFTWARE_SERVICE_APPROVAL_STANDARD_VI.md`
- `39_ENDPOINT_GOVERNANCE_IMPLEMENTATION_CHECKLIST_VI.md`

### Equipment / common architecture

- `MÔ HÌNH/02_NGHIEP_VU_MODULE/EQUIPMENT/10_EQUIPMENT_REGISTER.md`
- `MÔ HÌNH/02_NGHIEP_VU_MODULE/EQUIPMENT/11_EQUIPMENT_COMPLETE.md`
- `MÔ HÌNH/00_DOCUMENT_MAP.md`
- Common Approval interfaces dưới `Application/Interfaces/Approvals/`
- Security capability registry hiện tại (`SecurityFunctionCodes`, Security Center)

## 2. Trạng thái

- **EXISTS** — đã có code/contract chạy được cho requirement.
- **PARTIAL** — có nền nhưng thiếu một hoặc nhiều mắt xích của requirement.
- **MISSING** — tài liệu yêu cầu nhưng chưa tìm thấy implementation/contract/entity tương ứng trong branch.
- **RISK** — đã có implementation nhưng đang nối sai boundary hoặc dùng capability/domain không đúng trách nhiệm.
- **DUPLICATE RISK** — không tạo mới; đang có thành phần khác cần tái sử dụng.

## 3. Ma trận Requirement → Entity → DTO → Service → API → UI → Approval → Agent → Compliance

| # | Requirement | Entity / persistence | DTO / Contract | Service | API | UI | Approval | Agent | Compliance | Status |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Endpoint/device identity | `F03EndpointDevice` | `EndpointInventoryRequestDto`, `EndpointInventorySummaryDto` | `EndpointInventoryService` | `GET /api/security/endpoints`, `GET /{deviceKey}`, ingest | Chưa tìm thấy Endpoint UI canonical | Không áp dụng trực tiếp | `EndpointWorker` gửi identity | Identity state | **EXISTS** |
| 2 | Heartbeat / last seen | `F03EndpointDevice.LastSeenUtc`, `Status` | Summary DTO | `EndpointInventoryService.UpsertInventoryAsync` | ingest | Chưa có UI riêng xác nhận | Không | `EndpointWorker` định kỳ | Online/offline policy chưa có engine | **PARTIAL** |
| 3 | Software inventory | `F03EndpointSoftwareInventory` | `EndpointSoftwareInventoryDto` | `EndpointInventoryService` | ingest + summary | Chưa tìm thấy inventory detail UI | Không | `EndpointCollector.CollectSoftware()` | Chưa có policy evaluation | **EXISTS / GAP** |
| 4 | Windows Service inventory | `F03EndpointServiceInventory` | `EndpointServiceInventoryDto` | `EndpointInventoryService` | ingest + summary | Chưa tìm thấy inventory detail UI | Không | `EndpointCollector.CollectServices()` | Chưa có policy evaluation | **EXISTS / GAP** |
| 5 | Per-device credential | `F03EndpointCredentials` | credential contract đã được tách khỏi duplicate project | `IEndpointCredentialService` hiện hữu theo architecture | Credential API tồn tại/được tài liệu hóa; cần trace tiếp toàn bộ action | UI chưa xác nhận | Không | `WindowsSecretStore`, `ApiKeyProtected` | Credential status chưa nối compliance | **PARTIAL** |
| 6 | Secure inventory ingestion | Device credential + endpoint tables | `EndpointInventoryRequestDto` | `EndpointInventoryService` | `POST /api/security/endpoints/inventory` | Không | Không | Header `X-FVN-Device-Api-Key` | Không | **EXISTS** |
| 7 | Server-side DeviceKey resolution | `F03EndpointCredentials` → `F03EndpointDevices` | Request identity bị overwrite server-side | `EndpointInventoryController.ResolveDeviceKeyAsync` | ingest | Không | Không | Không tin DeviceKey business ownership từ client | Không | **EXISTS** |
| 8 | Equipment binding | `F03EndpointDevice.EquipmentAssetId` | Request có `EquipmentAssetId` nhưng controller loại bỏ giá trị client | `EndpointInventoryService` chỉ bind khi tạo mới và asset tồn tại | ingest | Equipment UI có domain riêng | Không tạo Endpoint-Equipment approval riêng | Agent không được tự bind | Chưa có reconciliation | **PARTIAL** |
| 9 | Equipment master | `F03EquipmentAsset` và các entity Equipment hiện hữu | Equipment DTO grouping hiện hữu | `IEquipmentService`, import/form/inspection services | Equipment API hiện hữu | Equipment UI hiện hữu | Common Approval | Không phụ thuộc Agent | Không | **EXISTS** |
| 10 | Equipment Schema canonical summary | `F03EquipmentFieldDefinition` / schema entities | `EquipmentSchemaSummaryDto` trong `Dtos/Equipment` | Equipment import/schema services | Schema API hiện hữu | Equipment Schema UI | Common Approval khi nghiệp vụ yêu cầu | Không | Không | **EXISTS** |
| 11 | Equipment Import | `F03EquipmentImportBatch`, `F03EquipmentImportRow` | `Dtos/EquipmentImport/EquipmentSchemaDtos.cs` và import DTOs | `IEquipmentImportService` | Equipment import API | Import UI | Theo Equipment workflow nếu cần | Không | Không | **EXISTS** |
| 12 | Common Approval engine | `F03Approval*` hiện hữu | Approval contracts hiện hữu | `IApprovalEngine`, `IApprovalProvider`, route/policy/selection/inbox services | Approval controllers hiện hữu | Approval UI dùng chung | **Common Approval** | Không | Audit/history | **EXISTS** |
| 13 | Equipment Approval provider/subject | Equipment request + approval snapshot | Equipment approval DTOs/subject | Equipment-specific provider/service dùng common engine | Equipment submit/approval APIs | Equipment request UI | Common engine | Không | Audit | **EXISTS / VERIFY IMPLEMENTATION** |
| 14 | Software Allowlist/Catalog | Chưa tìm thấy entity catalog/allowlist | Chưa có canonical catalog DTO | Chưa có catalog service | Chưa có catalog API | Chưa có catalog UI | Requirement gọi common approval | Agent chỉ inventory | Chưa có evaluation | **MISSING** |
| 15 | Windows Service Allowlist/Catalog | Chưa tìm thấy entity service catalog | Chưa có canonical service policy DTO | Chưa có service catalog service | Chưa có catalog API | Chưa có catalog UI | Common approval theo tài liệu | Agent chỉ inventory | Chưa có evaluation | **MISSING** |
| 16 | Allowlist version | Chưa tìm thấy versioned policy entity | Chưa có contract | Chưa có service | Chưa có API | Chưa có UI | Requirement yêu cầu snapshot/version | Agent không tự thay policy | Chưa có policy version evaluation | **MISSING** |
| 17 | Software Security Review | Chưa tìm thấy security-review entity/workflow riêng cho Endpoint | Chưa có contract | Chưa có Endpoint governance service | Chưa có API | Chưa có UI | Phải dùng common approval sau review | Agent không review | PendingReview chưa được nối catalog | **MISSING** |
| 18 | Service Security Review | Chưa tìm thấy | Chưa có | Chưa có | Chưa có | Chưa có | Common approval | Agent không review | Chưa có | **MISSING** |
| 19 | Install Request | Chưa tìm thấy Endpoint install-request entity | Chưa có DTO | Chưa có service | Chưa có API | Chưa có UI | Requirement yêu cầu common Approval + catalog snapshot | Agent mới xác nhận inventory | Chưa có Installed → Compliance link | **MISSING** |
| 20 | New Software Request | Chưa tìm thấy | Chưa có | Chưa có | Chưa có | Chưa có | IT Security Review → common Approval theo tài liệu | Agent không tự promote | Chưa có | **MISSING** |
| 21 | Service Change Request | Chưa tìm thấy | Chưa có | Chưa có | Chưa có | Chưa có | Common Approval | Agent không mutate catalog | Chưa có | **MISSING** |
| 22 | Exception Request | `F03EndpointAlerts` có `RelatedPolicyId`, nhưng chưa có exception aggregate đầy đủ | Chưa có exception DTO | Chưa có exception service | Chưa có exception API | Chưa có UI | Common Approval yêu cầu theo tài liệu | Không | Chưa có exception-aware evaluation | **PARTIAL** |
| 23 | Immutable Approval Snapshot | `F03ApprovalSnapshot` dùng chung | Approval snapshot contracts dùng chung | Common Approval | Approval APIs | Approval UI | **Common Approval** | Không | Audit | **EXISTS / REUSE** |
| 24 | Before/After catalog snapshot | Chưa có Endpoint catalog entity để snapshot | Chưa có Endpoint catalog snapshot DTO | Chưa có Endpoint governance service | Chưa có | Chưa có | Có nền snapshot chung nhưng chưa có Endpoint subject/payload | Không | Không | **PARTIAL** |
| 25 | Approval route from policy | `F03ApprovalPolicies` hiện hữu | Approval route contracts hiện hữu | `IApprovalRouteService`, `IApprovalPolicyService`, selection services | Approval route/policy APIs | Approval administration UI | Common route | Không | Audit | **EXISTS / REUSE** |
| 26 | Compliance engine | Chưa tìm thấy compliance result/finding aggregate | Chưa có compliance DTO | Chưa có Endpoint compliance service | Chưa có compliance API | Chưa có compliance UI | Exception/approval sẽ dùng common approval | Agent là input | **Chưa có policy evaluation** | **MISSING** |
| 27 | Compliance finding/history | Chưa tìm thấy finding/history entity; `F03EndpointAlerts` chỉ là alert | Chưa có | Chưa có | Chưa có | Chưa có | Exception approval chưa nối | Agent input | Chưa có immutable evaluation history | **MISSING** |
| 28 | Endpoint alert | `F03EndpointAlerts` hiện hữu; `UpsertAlertAsync` đã idempotent theo alert type/open state | Chưa có dedicated alert DTO được xác nhận | `EndpointInventoryService.UpsertAlertAsync` | Alert API chưa xác nhận đầy đủ | UI chưa xác nhận | Resolve có thể dùng common approval nếu policy yêu cầu | Agent là trigger | Alert không thay thế compliance finding | **PARTIAL / EXISTS** |
| 29 | Identity history | `F03EndpointIdentityHistory` được ghi từ inventory service | Chưa có dedicated DTO | `AddIdentityHistoryAsync` | Chưa thấy history API | Chưa thấy UI | Review cần common workflow nếu policy yêu cầu | Agent cung cấp identity | Identity alert có nền | **EXISTS / GAP UI** |
| 30 | Security Center Endpoint capabilities | `F03Function`, `F03RoleFunction`, registry entities hiện hữu | Security DTOs/manifest hiện hữu | `IAuthorizationService` hiện hữu | Endpoint controller có server-side check nhưng đang dùng `EquipmentView` | Security manifest hiện hữu | Không | Không | Không | **RISK** |
| 31 | Endpoint-specific capability registry | Chưa có FunctionCode riêng trong `SecurityFunctionCodes`; hiện chỉ có Equipment/Security/... | Chưa có Endpoint capability contract | Chưa có registration/seed riêng | Endpoint API chưa dùng capability Endpoint | Chưa có UI permission mapping | Không | Không | Không | **MISSING** |
| 32 | Data scope / employee ownership | `F03EndpointDevice.EmployeeCode` | Request employee code bị bỏ qua khi ingest | Service chưa có endpoint scope query | GET endpoint hiện chỉ check capability, chưa thấy scope filter | Chưa có UI | Không | Agent không được tự assign owner | Compliance scope chưa có | **PARTIAL / RISK** |
| 33 | Agent safety: no arbitrary command | Agent chỉ collect + POST inventory | Inventory contract only | Worker chỉ sync | Ingest endpoint only | Không | Không | **EXISTS** | Không | **EXISTS** |
| 34 | Agent idempotent inventory | Inventory hash tồn tại; service hiện xóa/reinsert child inventory theo mỗi payload | DTO có full snapshot | `EndpointInventoryService.UpsertInventoryAsync` | ingest | Không | Không | Worker định kỳ | Không | **PARTIAL** |
| 35 | Agent offline/reconnect | `LastSeenUtc` + Status nền | Summary có last seen/status | Chưa có offline evaluator | Chưa có dedicated status API | Chưa có UI | Không | Worker retry theo vòng lặp | Chưa có policy evaluation | **PARTIAL** |
| 36 | Network discovery | Chưa tìm thấy discovery entity/service/API | Chưa có | Chưa có | Chưa có | Chưa có | Không | Không | Unmanaged device chưa được tạo từ discovery | **MISSING** |
| 37 | Audit lifecycle | Security audit/common audit có nền | `SecurityAuditEntryDto` hiện hữu | Common audit services | Security audit API | Security audit UI | Approval history dùng chung | Agent logs local | Compliance audit chưa có | **PARTIAL** |
| 38 | Endpoint UI / operator workspace | Không áp dụng | Không áp dụng | Không có Endpoint UI service được xác nhận | API tồn tại cho inventory | **Chưa tìm thấy trang Endpoint Inventory/Compliance canonical trong branch** | Approval UI dùng chung | Agent background | Compliance UI chưa có | **MISSING** |
| 39 | Deployment / Agent installation | Agent project + `install-agent.ps1` + options/appsettings example | Endpoint options/config | Worker/collector | API endpoint | Deployment/runbook docs | Không | **EXISTS** | Không | **EXISTS / VERIFY E2E** |
| 40 | End-to-end tests / production gate | Test artifacts chưa được xác nhận cho governance | Chưa có full governance test contract | Inventory tests cần bổ sung governance tests | API ingest có thể test | UI tests chưa có | Approval E2E chưa có Endpoint subject | Agent pilot chưa có evidence | Compliance tests chưa có | **MISSING** |

## 4. Kết luận ranh giới trách nhiệm

### Giữ nguyên — không tạo mới

1. **Equipment domain**: giữ `F03EquipmentAsset`, Equipment services, Equipment DTO grouping, `EquipmentSchemaSummaryDto`, Equipment Import DTOs và Equipment UI hiện có.
2. **Approval**: dùng `Application/Interfaces/Approvals/*`, Approval Engine/Provider/Subject/Route/Policy/Snapshot/Inbox/Notification hiện hữu. Không tạo `EndpointApprovalEngine`, `SoftwareApprovalEngine`, `EquipmentSoftwareApproval`.
3. **Endpoint inventory**: giữ `F03EndpointDevice`, `F03EndpointSoftwareInventory`, `F03EndpointServiceInventory`, `EndpointInventoryDtos`, `IEndpointInventoryService`, `EndpointInventoryService`, `EndpointInventoryController`, `EndpointCollector`, `EndpointWorker`.
4. **Help**: dùng `FeatureHelp`; không tạo EndpointHelp/EquipmentHelp riêng.

### Cần bổ sung vì thực sự chưa có

1. Endpoint Software Catalog/Allowlist aggregate + version.
2. Endpoint Service Catalog/Allowlist aggregate + version.
3. Security Review state/record cho catalog/request.
4. Install Request + New Software Request + Service Change Request + Exception Request.
5. Compliance evaluation/result/history.
6. Endpoint-specific Security Center capability codes/registry/seed và mapping server-side.
7. Endpoint operator UI/Compliance UI nếu chưa tồn tại ở nơi khác.
8. Tests cho approval/catalog/compliance/identity/agent E2E.

## 5. Các điểm code lệch cần xử lý trước khi mở rộng

### 5.1 Endpoint controller đang dùng Equipment capability

`EndpointInventoryController` hiện kiểm tra `SecurityFunctionCodes.EquipmentView` cho GET endpoint. Đây là **sai boundary**: Endpoint không nên mượn capability Equipment để đại diện quyền Endpoint.

Không thay bằng một engine mới. Cần bổ sung Endpoint capability vào Security Center hiện tại rồi thay controller sang capability đó.

### 5.2 Agent không gửi ownership là đúng

`EndpointWorker` gửi `EmployeeCode = null` và `EquipmentAssetId = null`; server resolve credential → DeviceKey. Đây là đúng hướng bảo mật. Không sửa Agent để cho phép client tự bind ownership.

### 5.3 Inventory hiện chưa phải Compliance

`EndpointInventoryService` đã normalize/dedupe payload và có identity/alert handling, nhưng chưa có Active Policy + Exception → Evaluation → Finding. Vì vậy không được coi inventory hiện tại là Compliance hoàn chỉnh.

### 5.4 Allowlist không được gắn vào Equipment

Nếu bổ sung catalog, đặt dưới Endpoint/Security governance. `EquipmentAssetId` chỉ là liên kết tới asset khi nghiệp vụ cần; không tạo `EquipmentSoftwareAllowlist`.

## 6. Kế hoạch code theo thứ tự, không phá business flow

1. Bổ sung Endpoint capability vào **Security Center hiện tại** và seed SQL.
2. Hoàn thiện scope check cho Endpoint GET/alert/compliance theo operator assignment hiện tại.
3. Tạo catalog/policy/version ở Endpoint governance layer; không gom vào Equipment.
4. Tạo DTO theo nhóm trách nhiệm: `EndpointCatalog`, `EndpointRequests`, `EndpointCompliance`; không tạo một DTO khổng lồ.
5. Dùng common Approval Subject/Provider/Engine cho Catalog Change, Install Request và Exception.
6. Bổ sung Compliance evaluator dùng inventory hiện tại làm input và lưu `PolicyVersion`/`PolicyItemId`/evaluation time.
7. Bổ sung UI qua cấu trúc Web hiện tại và `FeatureHelp`.
8. Bổ sung E2E tests và chỉ sau đó cập nhật rollout gate.

## 7. Quy tắc chống trùng

- Không tạo DTO summary thứ hai cho Equipment Schema.
- Không gom Equipment DTO vào một file khổng lồ.
- Không tạo Endpoint Approval Engine.
- Không tạo Equipment Software Allowlist.
- Không để Agent mutate catalog.
- Không coi Agent Installed là Allowed.
- Không dùng Equipment capability để che lấp Endpoint capability.
- Không tạo architecture document thứ hai; tài liệu này chỉ là traceability matrix.
