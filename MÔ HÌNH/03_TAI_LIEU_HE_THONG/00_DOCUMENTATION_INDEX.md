# FVN REGISTER — DOCUMENTATION HUB

> Cổng vào duy nhất cho tài liệu giới thiệu, hướng dẫn sử dụng, trợ giúp, vận hành và tiêu chuẩn tài liệu.

## 1. Ba lớp tài liệu

```text
MÔ HÌNH/
├── 01_KIEN_TRUC_HE_THONG/      Kiến trúc, security, approval, framework dùng chung
├── 02_NGHIEP_VU_MODULE/        Quy tắc và thiết kế nghiệp vụ từng module
└── 03_TAI_LIEU_HE_THONG/       Tài liệu người dùng, Help, triển khai, kỹ thuật và quản trị tài liệu
```

## 2. Tài liệu người dùng và Help

- `01_GIOI_THIEU/01_INTRODUCTION.md` — giới thiệu hệ thống.
- `01_GIOI_THIEU/02_FEATURES.md` — bản đồ tính năng.
- `01_GIOI_THIEU/03_PR_AND_LAUNCH.md` — giới thiệu, business case và rollout.
- `02_HUONG_DAN_SU_DUNG/04_USER_GUIDE.md` — hướng dẫn sử dụng đầy đủ.
- `02_HUONG_DAN_SU_DUNG/05_QUICK_GUIDE.md` — tra cứu nhanh.
- `02_HUONG_DAN_SU_DUNG/06_FAQ.md` — câu hỏi thường gặp.
- `02_HUONG_DAN_SU_DUNG/EQUIPMENT/` — hướng dẫn chi tiết Equipment.

`FeatureHelp` là Help UI dùng chung. Nội dung Help phải tham chiếu tài liệu chuẩn; không tạo Help riêng cho từng module nếu nội dung có thể dùng chung.

## 3. Tài liệu kỹ thuật / triển khai

- `04_KY_THUAT_TRIEN_KHAI/ENDPOINT/` — toàn bộ tài liệu Endpoint Inventory & Compliance.
- `../01_KIEN_TRUC_HE_THONG/EXECUTION/19_EXECUTION_RESOLUTION_POLICY.md` — thiết kế policy, SLA, appeal, final decision và payroll boundary của Execution Reconciliation.
- `02_HUONG_DAN_SU_DUNG/EXECUTION_RECONCILIATION_GUIDE.md` — hướng dẫn nhân viên và HR xử lý phản hồi/đối soát.
- Các tài liệu triển khai khác được đưa vào cùng lớp này thay vì duy trì thư mục `docs/` độc lập.

## 4. Tiêu chuẩn tài liệu

`05_TIEU_CHUAN_TAI_LIEU/08_DOCUMENTATION_STANDARD.md` là chuẩn duy trì tài liệu.

Nguyên tắc:

1. Business/architecture source of truth định nghĩa rule.
2. Implementation phải khớp source of truth.
3. User Guide/FAQ/Help chỉ diễn giải behavior đã được chứng minh.
4. Không tạo architecture document song song để mô tả lại cùng một rule.
5. Không để `docs/` và `MÔ HÌNH/DOCUMENTATION` trở thành hai kho tài liệu độc lập.

## 5. Thứ tự cập nhật

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

## 6. Quy ước link

Tất cả link nội bộ phải dùng đường dẫn tương đối theo vị trí mới. Khi di chuyển tài liệu, phải rà lại inbound/outbound links trước khi xóa vị trí cũ.

## 7. Source of truth nghiệp vụ

Các tài liệu thiết kế current dưới `MÔ HÌNH/` vẫn là nguồn rule của hệ thống; không sao chép chúng sang thư mục Help. Documentation Hub chỉ điều hướng và diễn giải.
