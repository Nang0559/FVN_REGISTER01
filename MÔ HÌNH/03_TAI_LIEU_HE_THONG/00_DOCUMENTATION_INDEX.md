# FVN REGISTER — DOCUMENTATION HUB

> Cổng vào cho tài liệu giới thiệu, hướng dẫn sử dụng, trợ giúp, vận hành và tiêu chuẩn tài liệu.

## 1. Ba lớp tài liệu

```text
MÔ HÌNH/
├── 01_KIEN_TRUC_HE_THONG/      Kiến trúc, security, approval, framework dùng chung
├── 02_NGHIEP_VU_MODULE/        Quy tắc và thiết kế nghiệp vụ từng module
└── 03_TAI_LIEU_HE_THONG/       Tài liệu giới thiệu, người dùng, triển khai và quản trị
```

## 2. Giới thiệu và trình bày với Ban giám đốc

Bộ tài liệu giới thiệu, trình chiếu và đánh giá hiệu quả nằm tại [01_GIOI_THIEU](01_GIOI_THIEU/00_README.md).

- [00 — Cổng vào bộ tài liệu giới thiệu](01_GIOI_THIEU/00_README.md)
- [01 — Giới thiệu tổng thể](01_GIOI_THIEU/01_INTRODUCTION.md)
- [02 — Bản đồ tính năng và giá trị](01_GIOI_THIEU/02_FEATURES.md)
- [03 — Thông điệp và kế hoạch áp dụng](01_GIOI_THIEU/03_PR_AND_LAUNCH.md)
- [04 — Trình chiếu Ban giám đốc](01_GIOI_THIEU/04_PRESENTATION.md)
- [05 — Biểu mẫu đo đường cơ sở và KPI](01_GIOI_THIEU/05_KPI_BASELINE_TEMPLATE.md)
- [06 — Lợi ích theo vai trò](01_GIOI_THIEU/06_ROLE_BENEFITS.md)
- [07 — Kịch bản demo](01_GIOI_THIEU/07_DEMO_STORYBOARD.md)
- [08 — Kiểm soát sẵn sàng và tuyên bố chức năng](01_GIOI_THIEU/08_READINESS_AND_CLAIMS.md)
- [09 — Đánh giá hiệu quả đầu tư](01_GIOI_THIEU/09_BUSINESS_CASE_COST_REDUCTION.md)

## 3. Tài liệu người dùng và Help

- `02_HUONG_DAN_SU_DUNG/04_USER_GUIDE.md` — hướng dẫn sử dụng đầy đủ.
- `02_HUONG_DAN_SU_DUNG/05_QUICK_GUIDE.md` — tra cứu nhanh.
- `02_HUONG_DAN_SU_DUNG/06_FAQ.md` — câu hỏi thường gặp.
- `02_HUONG_DAN_SU_DUNG/EQUIPMENT/` — hướng dẫn chi tiết Equipment.

`FeatureHelp` là Help UI dùng chung. Nội dung Help phải tham chiếu tài liệu chuẩn; không tạo Help riêng cho từng module nếu nội dung có thể dùng chung.

## 4. Tài liệu kỹ thuật / triển khai

- `04_KY_THUAT_TRIEN_KHAI/ENDPOINT/` — tài liệu Endpoint Inventory & Compliance.
- Thư mục `docs/` ở gốc repository hiện vẫn tồn tại và chứa ghi chú kỹ thuật/kiểm toán. Đây không phải cổng tài liệu người dùng hay bộ tài liệu PR; các ghi chú cần được phân loại/hợp nhất dần vào đúng khu vực chuẩn trong `MÔ HÌNH/`. Không dùng ghi chú rời làm bằng chứng nghiệm thu nếu chưa đối chiếu branch và commit hiện tại.

## 5. Tiêu chuẩn tài liệu

`05_TIEU_CHUAN_TAI_LIEU/08_DOCUMENTATION_STANDARD.md` là chuẩn duy trì tài liệu.

Nguyên tắc:
1. Business/architecture source of truth định nghĩa rule.
2. Implementation phải khớp source of truth.
3. User Guide/FAQ/Help chỉ diễn giải behavior đã được chứng minh.
4. Không tạo tài liệu kiến trúc song song để mô tả lại cùng một rule.
5. Không để `docs/` và `MÔ HÌNH/DOCUMENTATION` trở thành hai kho độc lập.

## 6. Thứ tự cập nhật

```text
Architecture / Business Rule
        ↓
Implementation
        ↓
User Guide / Help
        ↓
Quick Guide / FAQ
        ↓
Presentation / rollout
```

## 7. Quy ước link

Tất cả link nội bộ phải dùng đường dẫn tương đối theo vị trí mới. Khi di chuyển tài liệu, rà lại link vào/ra trước khi xóa vị trí cũ.

## 8. Source of truth nghiệp vụ

Các tài liệu thiết kế current dưới `MÔ HÌNH/` vẫn là nguồn rule của hệ thống; không sao chép chúng sang thư mục Help. Documentation Hub chỉ điều hướng và diễn giải.

## 9. Equipment / Endpoint Agent — tài liệu theo luồng

| Nhu cầu | Tài liệu chính |
|---|---|
| Hiểu Equipment business | `02_NGHIEP_VU_MODULE/EQUIPMENT/10_EQUIPMENT_REGISTER.md` |
| Hiểu bàn giao/trách nhiệm vận hành | `02_NGHIEP_VU_MODULE/EQUIPMENT/11_EQUIPMENT_COMPLETE.md` |
| Hiểu Endpoint architecture | `02_NGHIEP_VU_MODULE/ENDPOINT/30_ENDPOINT_INVENTORY_COMPLIANCE.md` |
| Cấp/rotate/revoke credential | `04_KY_THUAT_TRIEN_KHAI/ENDPOINT/33_ENDPOINT_CREDENTIAL_SECURITY.md` |
| Cài Agent / rollout | `04_KY_THUAT_TRIEN_KHAI/ENDPOINT/34_ENDPOINT_DEPLOYMENT_RUNBOOK_VI.md` |
| Identity/schema | `04_KY_THUAT_TRIEN_KHAI/ENDPOINT/35_ENDPOINT_DATA_MODEL_VI.md` |
| Hướng dẫn người dùng | `02_HUONG_DAN_SU_DUNG/EQUIPMENT/12_EQUIPMENT_HELP.md` |

Không dùng DeviceKey làm business entry point. DeviceKey chỉ là technical endpoint identity được server tạo/quản lý.
