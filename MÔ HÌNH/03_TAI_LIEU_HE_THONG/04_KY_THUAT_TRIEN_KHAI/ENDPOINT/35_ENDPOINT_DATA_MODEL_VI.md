# Mô hình dữ liệu Endpoint không dùng AD

## Quan hệ

```text
F03Employees
    │
    ▼
F03EquipmentAssets
    │
    ▼
F03EndpointDevices
    ├── F03EndpointCredentials
    ├── F03EndpointSoftwareInventory
    ├── F03EndpointServiceInventory
    ├── F03EndpointComplianceResults
    ├── F03EndpointComplianceExceptions
    └── F03EndpointAlerts
```

## Ba identity

- **Employee identity:** do FVN HRM quản lý, dùng EmployeeCode.
- **Asset identity:** do Equipment quản lý, dùng AssetCode/Id/Serial theo mô hình hiện có.
- **Endpoint identity:** do Endpoint Registry quản lý, dùng DeviceKey và credential riêng.

Ba identity không được gộp thành một khóa.

## Thay người sử dụng

Không đổi DeviceKey khi chuyển laptop từ nhân viên A sang B. Employee association của Equipment thay đổi qua workflow Equipment.

## Cài lại Windows

AgentInstallationId có thể thay đổi. Server đối chiếu credential + hardware identity + Equipment Asset trước khi tạo endpoint mới. Nếu không đủ bằng chứng thì tạo trạng thái cần xác minh, không tự merge.

## Thay mainboard

Hardware UUID/serial thay đổi phải được IT xác minh; không tự động merge.

## Đổi tên máy

ComputerName chỉ là thuộc tính, không tạo endpoint mới.

## Máy không có Equipment Asset

Có thể tồn tại trạng thái `UnmanagedDevice`/`PendingRegistration` nếu discovery hoặc Agent xuất hiện trước khi IT đăng ký asset. Sau xác minh, bind endpoint vào Equipment Asset.


## 9. Equipment ↔ Endpoint identity contract

Employee (HRM) → Equipment Asset → Endpoint Device → DeviceKey + HardwareUuid/SerialNumber + AgentInstallationId → Credential history.

EmployeeCode, EquipmentAssetId/AssetCode và DeviceKey là ba lớp identity khác nhau.

- Bàn giao: đổi quan hệ Equipment → Employee; giữ Endpoint identity.
- Reinstall: AgentInstallationId có thể đổi; đối chiếu hardware identity.
- Mainboard/hardware identity đổi: IT review.
- ComputerName đổi: không tự tạo Endpoint mới.

Không dùng EmployeeCode làm Endpoint key và không dùng DeviceKey làm Equipment asset key.
