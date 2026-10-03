# BẢN ĐỒ TÀI LIỆU FVN_REGISTER — BẢN CHUẨN

> Đây là bản đồ chính thức sau khi làm sạch và hợp nhất bộ tài liệu. `MÔ HÌNH` là nơi quản lý tài liệu chuẩn của hệ thống. Không duy trì thêm `docs/` hoặc `MÔ HÌNH/DOCUMENTATION/` như một kho tài liệu song song.

## 1. Cấu trúc chính thức

```text
MÔ HÌNH/
├── 01_KIEN_TRUC_HE_THONG/
├── 02_NGHIEP_VU_MODULE/
├── 03_TAI_LIEU_HE_THONG/
└── 23_PRODUCT_READINESS_PLAN.md
```

### 01_KIEN_TRUC_HE_THONG

Chứa kiến trúc và cơ chế dùng chung cho nhiều module:

- Security / RBAC / MFA / Function Registry / Operator Assignment
- Dashboard capability
- Schema / Import dùng chung
- Execution Reconciliation
- Reporting / Data Scope
- Approval Route / Selection
- Work Calendar / Action

Nguyên tắc: module chỉ mô tả cách sử dụng các cơ chế này; không định nghĩa lại engine dùng chung.

### 02_NGHIEP_VU_MODULE

Chứa mô hình nghiệp vụ canonical của từng module, gồm OT, Trip, Equipment, Attendance/HRM và Endpoint Inventory & Compliance.

`30_ENDPOINT_INVENTORY_COMPLIANCE.md` thuộc nhóm này vì đây là tài liệu kiến trúc/nghiệp vụ của module Endpoint.

### 03_TAI_LIEU_HE_THONG

Đây là kho thống nhất cho tài liệu không phải source code:

```text
03_TAI_LIEU_HE_THONG/
├── 00_DOCUMENTATION_INDEX.md
├── 01_GIOI_THIEU/
├── 02_HUONG_DAN_SU_DUNG/
├── 04_KY_THUAT_TRIEN_KHAI/
└── 05_TIEU_CHUAN_TAI_LIEU/
```

- `01_GIOI_THIEU`: giới thiệu, tính năng, presentation, business case, product/launch.
- `02_HUONG_DAN_SU_DUNG`: user guide, quick guide, FAQ và hướng dẫn theo module.
- `04_KY_THUAT_TRIEN_KHAI`: API contract, implementation, security, deployment, data model, governance và rollout; Endpoint 31–39 được quản lý tại đây và tham chiếu về tài liệu 30.
- `05_TIEU_CHUAN_TAI_LIEU`: chuẩn viết và tổ chức tài liệu.

## 2. Help trong ứng dụng

Hệ thống chỉ có **một cơ chế Help UI dùng chung: `FeatureHelp`**.

```text
UI
 ↓
FeatureHelp
 ↓
HelpCatalog / HelpKey
 ↓
route mapping cụ thể → nội dung trợ giúp
 ↓
Mở tài liệu chi tiết tại /huong-dan?feature=...
```

`FeatureHelp` nằm trong `FVN_REGISTER/FVN_REGISTER.Shared/Components/FeatureHelp.razor`.

### Quy tắc Help

1. Không tạo Help component riêng cho từng module nếu nội dung có thể dùng `FeatureHelp`.
2. Không dùng `route.Contains(...)` để nhận diện module; mapping phải theo segment/prefix rõ ràng và ưu tiên route cụ thể trước route tổng quát.
3. Equipment dùng các HelpKey chuyên biệt:
   - `EQUIPMENT_WORKSPACE`
   - `EQUIPMENT_SCHEMA`
   - `EQUIPMENT_IMPORT`
   - `EQUIPMENT_CHECKLIST`
   - `EQUIPMENT_REPAIR`
   - `EQUIPMENT_HANDOVER`
4. Help chỉ hướng dẫn thao tác; Help không cấp quyền và không thay thế server-side capability/data-scope checks.
5. `/huong-dan` là điểm đọc tài liệu chi tiết; không tạo một hệ Help độc lập cho Equipment.
6. Nội dung nghiệp vụ chi tiết phải được đối chiếu với tài liệu canonical trong `MÔ HÌNH`; không để Help trở thành một business-rule source riêng.

## 3. Source of Truth

- Kiến trúc dùng chung → `01_KIEN_TRUC_HE_THONG`.
- Nghiệp vụ module → `02_NGHIEP_VU_MODULE`.
- Hướng dẫn người dùng / triển khai / vận hành → `03_TAI_LIEU_HE_THONG`.
- UI Help → `FeatureHelp`; nội dung chi tiết phải liên kết/đối chiếu với tài liệu canonical.
- Approval vẫn dùng Approval Engine dùng chung; Equipment, OT, Leave, Trip và Endpoint không tạo workflow engine riêng.

## 4. Quy tắc chống trùng lặp

1. Một chủ đề chỉ có một Source of Truth.
2. Không giữ bản sao tài liệu chỉ vì khác tên hoặc khác thư mục.
3. Tài liệu hướng dẫn không định nghĩa kiến trúc.
4. Runbook/deployment không định nghĩa nghiệp vụ.
5. Tài liệu module phải tham chiếu cơ chế dùng chung thay vì sao chép lại.
6. Khi đổi vị trí tài liệu phải cập nhật link nội bộ và Help/documentation mapping.
7. Không tạo `docs/` hoặc `MÔ HÌNH/DOCUMENTATION/` mới để chứa bản sao.

## 5. Trạng thái làm sạch

- Đã thống nhất ba nhóm tài liệu `01/02/03`.
- Đã gom bộ giới thiệu, hướng dẫn, kỹ thuật triển khai và tiêu chuẩn tài liệu vào `03_TAI_LIEU_HE_THONG`.
- Đã gom bộ Endpoint 31–39 vào khu vực kỹ thuật triển khai; tài liệu 30 vẫn là canonical module architecture.
- Đã loại bỏ mô hình Help riêng của Equipment; Help dùng chung qua `FeatureHelp` với mapping Equipment chuyên biệt.
- Đã loại bỏ cách route matching bằng substring trong `FeatureHelp`.
- Các tài liệu nghiệp vụ vẫn phải được rà nội dung khi nghiệp vụ thay đổi; không tạo thêm tài liệu chỉ để ghi nhận cùng một rule.


## 6. Canonical Equipment → Endpoint Agent

Endpoint Agent là capability gắn với Equipment Asset, không phải một quy trình cấp DeviceKey độc lập.

Luồng chuẩn: Employee → Equipment Asset → Endpoint Device → Credential → Agent Installation → Inventory/LastSeen → Compliance/Alert.

Quy tắc:
- Chỉ Equipment phù hợp với thiết bị có OS mới được bật Endpoint Agent.
- DeviceKey do server quản lý và gắn với Endpoint Device; người dùng không nhập DeviceKey để bắt đầu nghiệp vụ.
- Cấp/rotate/revoke credential thực hiện từ Sổ thiết bị → Endpoint Agent của Equipment.
- Secret chỉ hiển thị một lần; server chỉ lưu hash.
- Đổi người sử dụng Equipment không tự tạo DeviceKey mới.
- Cài lại OS có thể tạo AgentInstallationId mới; server đối chiếu hardware identity trước khi quyết định giữ Endpoint hay yêu cầu IT xác minh.
- ComputerName chỉ là thuộc tính, không phải identity chính.
- /endpoint-credentials nếu còn tồn tại chỉ là security/audit surface; không phải entry point nghiệp vụ.

## 7. Quy tắc cập nhật tài liệu

Khi thay đổi Equipment/Endpoint Agent, cập nhật theo thứ tự: 10 Equipment domain → 30 Endpoint domain → 31–35 technical contract/security/runbook/data model → User Guide/Help → Quick Guide/FAQ. Không tạo tài liệu kiến trúc thứ hai cho cùng flow.
