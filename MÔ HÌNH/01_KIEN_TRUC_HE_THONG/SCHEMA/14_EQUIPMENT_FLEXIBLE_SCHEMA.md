# 14 — Equipment Excel Import trên Shared Excel Platform

## Mục tiêu

Equipment sử dụng **Shared Excel Platform** làm canonical owner cho toàn bộ workbook mechanics, schema/version và import staging. Equipment chỉ giữ business mapping và domain validation.

Phòng ban vẫn có thể dùng các bộ cột khác nhau mà không tạo bảng schema Excel riêng cho từng module.

## 1. Kiến trúc canonical

Excel file → IExcelPlatform → Inspect → Schema/version → Header/field mapping → Preview/validation → Import staging → Equipment business mapping → F03EquipmentAsset.

Canonical tables:
- F03ExcelSchemas
- F03ExcelSchemaVersions
- F03ExcelSchemaFields
- F03ExcelImportBatches
- F03ExcelImportRows
- F03ExcelImportErrors

Equipment không sở hữu các bảng F03EquipmentSchemas, F03EquipmentFieldDefinitions, F03EquipmentImportBatches, F03EquipmentImportRows hoặc F03EquipmentImportErrors.

Các bảng legacy trên đã được loại khỏi deployment path và được cleanup bởi SQL/16_EquipmentFlexibleImport_LegacyCleanup.sql.

## 2. Equipment business fields

Các trường lõi vẫn thuộc domain Equipment: EquipmentCode, EquipmentName, Specification, SerialNumber, AssetCode, PurchasePrice, PurchaseDate, ExpectedDepreciationDate, DeptCode, Location, QR và Note.

Field mở rộng của từng phòng ban được khai báo trong F03ExcelSchemaFields và dữ liệu business mở rộng có thể được lưu vào F03EquipmentAssets.CustomDataJson.

## 3. Import lifecycle

Upload → Inspect → Schema mapping → Preview → Validation → Shared Excel staging → Equipment business mapping → Commit → F03EquipmentAssets.

Không tạo workbook reader/parser riêng trong Equipment.

## 4. Authorization

Capability: Equipment.Import = 2306, Scope = Department.

Authorization phải dùng AuthorizationService và scope department tương ứng; không dùng IsAdmin làm bypass nghiệp vụ.

## 5. Quy tắc cho module mới

1. Đăng ký ModuleCode / EntityCode.
2. Sử dụng IExcelPlatform.
3. Dùng F03Excel* cho schema/version/import metadata.
4. Chỉ giữ adapter cho business mapping/validation.
5. Không tạo bảng F03<Module>Schema, F03<Module>ImportBatch, F03<Module>ImportRow.
6. Không instantiate ExcelPlatform trực tiếp.
7. Không instantiate NPOI ngoài ExcelPlatform.

## 6. Trạng thái migration

Equipment và Endpoint Governance đã chuyển sang Shared Excel Platform ở application layer. Deployment scripts bootstrap Shared Excel Platform và cleanup legacy Equipment Excel metadata theo thứ tự: 001 → 002 → 003 migration → legacy cleanup.
