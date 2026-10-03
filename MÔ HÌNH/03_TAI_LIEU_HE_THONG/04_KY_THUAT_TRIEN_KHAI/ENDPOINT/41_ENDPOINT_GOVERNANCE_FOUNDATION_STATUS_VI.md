# Endpoint Governance — Foundation Status

## Boundary đã chốt

### Equipment

Giữ nguyên domain Equipment hiện có:

- `F03EquipmentAsset`
- Equipment Schema / Import / Forms / Inspection
- `EquipmentSchemaSummaryDto`
- `EquipmentImport/EquipmentSchemaDtos`
- Equipment-specific services và UI

Endpoint chỉ tham chiếu Equipment qua `F03EndpointDevice.EquipmentAssetId`. Đây là liên kết asset/reference, không tạo domain Equipment thứ hai và không đưa software/service allowlist vào Equipment.

Không tạo `EquipmentSoftwareAllowlist`, `EquipmentApprovalEngine` hoặc DTO Equipment mới chỉ để phục vụ Endpoint.

### Approval

Endpoint governance dùng `RequestModule.Endpoint` và các abstraction hiện có dưới `Application/Interfaces/Approvals`.

Không tạo:

- `EndpointApprovalEngine`
- `SoftwareApprovalEngine`
- `ServiceApprovalEngine`
- `EquipmentSoftwareApproval`

Approval snapshot/version vẫn thuộc Common Approval.

## Đã triển khai trên branch

| Layer | Thành phần | Trạng thái |
|---|---|---|
| Core | Endpoint governance enums | **DONE** |
| Core | `RequestModule.Endpoint` | **DONE** |
| Core Entity | `F03EndpointGovernancePolicy` | **DONE** |
| Core Entity | `F03EndpointGovernancePolicyItem` | **DONE** |
| Core Entity | `F03EndpointGovernanceRequest` | **DONE** |
| Core Entity | `F03EndpointComplianceFinding` | **DONE / persistence only** |
| Contract | Endpoint Governance policy/request/compliance DTOs | **DONE** |
| Application | `IEndpointGovernanceService` | **DONE** |
| Application | `EndpointGovernanceRequestSubject` | **DONE** |
| Approval | `EndpointGovernanceApprovalProvider` | **DONE** |
| Approval | `ApprovalBuildContext.ForEndpoint(...)` | **DONE** |
| Approval | `ApprovalEngineResolver` Endpoint route | **DONE** |
| Infrastructure | `EndpointGovernanceService` | **DONE** |
| Infrastructure | Excel adapter using existing ClosedXML dependency | **DONE** |
| API | `/api/security/endpoint-governance/*` | **DONE** |
| Security Center | Existing Endpoint capability family 3101–3124 reused | **DONE** |
| SQL | `54_EndpointGovernanceFoundation.sql` | **DONE** |
| SQL migration | `55_EndpointGovernanceCatalogApproval.sql` | **DONE** |
| Deployment | `SQL/00_Deploy_All.sql` calls 54 then 55 | **DONE** |
| Equipment link | `F03EndpointDevices.EquipmentAssetId -> F03EquipmentAssets.Id` | **DONE / SQL FK + index** |

## Catalog lifecycle

Software và Windows Service dùng chung một policy model:

```text
EndpointGovernancePolicy
    PolicyCode + Version
    ItemType = Software / WindowsService
    TargetType = Workstation / Server / Both
          |
          +-- EndpointGovernancePolicyItem
```

Danh sách có thể được tạo:

```text
Manual
   or
Excel Preview -> Validate -> Canonical Upsert
```

Cả hai đường đều đi vào cùng `EndpointGovernanceService.CreateVersionAsync()`.
Không có persistence path riêng cho Excel.

### Catalog approval

```text
Draft Version
      |
      | Submit permission:
      | 3111 Software / 3116 Service
      v
F03EndpointGovernanceRequest
      |
      | RequestModule.Endpoint
      v
Common Approval
      |
      +---- Rejected
      |
      +---- Approved
                |
                v
             Publish
```

`Publish` chỉ được phép khi request liên kết với version đã có `RequestStatus = Approved` và người thao tác có capability approve tương ứng (`3112` Software / `3117` Service).

Vì vậy không còn đường `Create/Import -> Publish` trực tiếp.

## Security Center

Không tạo permission system mới.

Các capability Endpoint đã có trong `SecurityFunctionCodes` được dùng trực tiếp:

- Software: View / Manage / Submit / Approve / Security Review
- Service: View / Manage / Submit / Approve / Security Review
- Install request
- Exception
- Compliance

Super Admin cấu hình user/role được cấp các capability này bằng Security Center hiện có.

Người approve của catalog không được hard-code trong Endpoint. Route approver tiếp tục lấy từ Common Approval Policy/Route hiện có với `RequestModule.Endpoint`, PositionCode, Approval Group và Approval Level.

## Equipment integration rule

```text
F03EquipmentAsset
       ^
       |
EquipmentAssetId
       |
F03EndpointDevice
       |
       +-- Software Inventory
       +-- Service Inventory
```

Equipment là asset/master-data. Endpoint Device là technical identity/inventory. Governance catalog là policy. Ba boundary này không được nhập thành một entity.

## Versioning rule

Policy đã publish không được sửa nội dung in-place.

```text
Version N = Published / immutable
       |
       | change
       v
Version N+1 = Draft
       |
       v
Submit -> Common Approval -> Approved -> Publish
```

Kết quả compliance phải luôn giữ `PolicyId + PolicyVersion + PolicyItemId` để chứng minh chính xác policy đã được dùng khi đánh giá.

## Phần còn lại để đạt Governance E2E

1. **Compliance evaluator**: lấy inventory Software/Windows Service hiện tại, chọn policy `IsPublished` mới nhất đúng scope và ghi finding theo đúng policy version.
2. **EndpointType đáng tin cậy**: Workstation/Server phải là thuộc tính của cùng `F03EndpointDevice`, không tạo entity riêng; Agent không được tự ghi đè phân loại đã được quản trị.
3. **Compliance history/reconciliation**: idempotent evaluation, resolve finding khi inventory trở lại compliant.
4. **Endpoint operator UI**: Catalog/version, import, submit/approval state, requests, findings.
5. **Agent integration**: Agent chỉ collect inventory; server-side evaluator mới quyết định compliant/non-compliant.
6. **E2E/build verification**: compile solution, API tests, approval tests và inventory→compliance tests.

## Quy tắc chống trùng

Nếu capability đã tồn tại ở Common Approval, Security Center, Equipment hoặc Endpoint Inventory thì implementation phải gọi lại capability đó thay vì tạo subsystem tương đương.

`F03EndpointAlerts` không thay thế `F03EndpointComplianceFinding`: alert là sự kiện/cảnh báo; compliance finding là kết quả đánh giá có policy version làm bằng chứng.
