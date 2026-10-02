# 25 — Chuẩn Localization Web/UI VI–JA

## 1. Phạm vi

Tính năng hỗ trợ hai ngôn ngữ chỉ áp dụng cho Presentation/Web UI:

- `vi-VN`: Tiếng Việt — mặc định.
- `ja-JP`: Tiếng Nhật.

Backend/API/Core/Contract/Application/Infrastructure/SQL tiếp tục dùng tiếng Việt và không phải xây cơ chế localization.

## 2. Nguyên tắc

1. Không dịch business code, enum, status code, function code, route, DTO hoặc giá trị database.
2. Không hard-code text hiển thị cho người dùng trong Razor/component/service UI.
3. Button, label, table header, tab, dialog, tooltip, placeholder, validation, alert, snackbar, empty state, notification, task, calendar, dashboard và trạng thái hiển thị đều phải dùng `ILanguageService`.
4. Mỗi key phải có cả VI và JA.
5. Câu có tham số phải dùng `Language.T(key, args...)`, không nối chuỗi theo ngôn ngữ tiếng Việt rồi dịch.
6. Ngôn ngữ được chọn tại Login và được lưu ở browser storage. Sau khi đăng nhập, người dùng vẫn có thể đổi ngôn ngữ ở shell.
7. Khi đổi ngôn ngữ, UI phải render lại mà không reload nghiệp vụ/API.
8. API message hiện tại vẫn tiếng Việt. UI ưu tiên error/code ổn định khi cần hiển thị bản dịch; không thay đổi contract API chỉ để localization.

## 3. Kiến trúc đã triển khai

```text
Login language selector
        ↓
ILanguageService
        ↓
LanguageService
        ↓
LanguageCode (vi-VN / ja-JP)
        ↓
LanguageCatalog + LanguageCatalogAdditional
        ↓
Presentation/Web UI
```

Service được đăng ký scoped tại `FVN_REGISTER.Web/Program.cs` và được import toàn bộ trong `FVN_REGISTER.Shared/_Imports.razor`.

## 4. Key convention

```text
common.*
login.*
nav.*
status.*
message.*
validation.*
execution.*
payroll.*
leave.*
```

Module mới phải thêm vocabulary vào catalog trước khi đánh dấu hoàn thành UI.

## 5. Login

Login có selector:

- Tiếng Việt
- 日本語

Lựa chọn được lưu bằng key `fvn.ui.language` trong browser storage. Mặc định khi không có lựa chọn là `vi-VN`.

## 6. Phạm vi rà soát bắt buộc

- Login / authentication / 2FA / forgot password
- Layout / navigation / profile / sessions
- Dashboard
- Leave
- OT
- Trip
- Attendance / Work Calendar
- Execution / Confirmation / Evidence
- HR Review
- Approval / Re-approval
- Equipment
- Employees / Departments
- HRM Sync / HRM Attendance
- Security / Access
- Public Information
- History
- Notification / Task
- Payroll / Report
- Dialogs / reusable Components

## 7. Audit

Chạy:

```powershell
pwsh ./scripts/audit-ui-localization.ps1
```

Khi rà soát release:

```powershell
pwsh ./scripts/audit-ui-localization.ps1 -Strict
```

Audit là công cụ phát hiện candidate, không được tự động coi mọi string là UI text. Business/data literal phải được phân loại và giữ nguyên khi cần.

## 8. Definition of Done

Chỉ đánh dấu localization hoàn thành khi:

- [ ] Login cho chọn VI/JA.
- [ ] Lựa chọn được lưu và khôi phục.
- [ ] Shell có thể đổi ngôn ngữ sau login.
- [ ] Tất cả UI text của các module hiện hữu đã được rà.
- [ ] Không còn UI text tiếng Anh/Việt hard-code ngoài các trường hợp đã phân loại.
- [ ] Mỗi localization key có VI + JA.
- [ ] Button/action không bị bỏ sót.
- [ ] Dialog/validation/notification/task/calendar/dashboard không bị bỏ sót.
- [ ] Status code/database values không bị dịch hoặc thay đổi.
- [ ] API/Core/Application/Infrastructure/SQL không bị thay đổi nghiệp vụ để phục vụ localization.
- [ ] Audit source hoàn tất.
- [ ] Build solution thành công.
- [ ] Smoke test Login → đổi VI/JA → đăng nhập → shell → module.

## 9. Trạng thái triển khai

Nền localization, catalog, persistence, Login selector, authenticated shell, route status và audit script đã được triển khai trên branch `feature/i18n-vi-ja`. Các module Presentation còn lại phải được rà theo checklist mục 6 trước khi tuyên bố release-ready.
