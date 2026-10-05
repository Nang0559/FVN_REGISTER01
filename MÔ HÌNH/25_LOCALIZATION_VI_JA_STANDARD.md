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
LocalizationStore (embedded JSON)
        ↓
optional runtime overlay from Language Center
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

## 5. Login và phạm vi lưu ngôn ngữ

Login có selector:

- Tiếng Việt
- 日本語

Lựa chọn được lưu bằng key `fvn.ui.language` trong **browser local storage**. Mặc định khi không có lựa chọn hoặc giá trị không hợp lệ là `vi-VN`.

Đây là preference của từng trình duyệt/circuit, **không phải setting của server và không ghi vào user/DB/claim**. Server không dùng ngôn ngữ UI để thay đổi nghiệp vụ, API contract hoặc dữ liệu. Vì vậy mở cùng tài khoản trên một browser khác sẽ bắt đầu theo preference của browser đó.

Các giá trị persisted chính thức là `vi-VN` và `ja-JP`; parser vẫn chấp nhận `vi`/`ja` để tương thích với browser/legacy value và chuẩn hóa về hai giá trị chính thức.

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
python3 ./scripts/validate-ui-localization.py --output artifacts/localization-audit.json
```

Validator là **gate** cho parity VI↔JA, parity file module, duplicate key, key được sử dụng nhưng thiếu, placeholder mismatch và giá trị rỗng. Candidate hard-code chỉ là audit để phân loại, không tự động coi mọi literal là lỗi.

Audit là công cụ phát hiện candidate, không được tự động coi mọi string là UI text. Business/data literal phải được phân loại và giữ nguyên khi cần.

## 8. Lifecycle render khi đổi ngôn ngữ

- `ILanguageService.LanguageChanged` là tín hiệu duy nhất cho UI re-render khi preference/text overlay thay đổi.
- `AppBase` tự subscribe/unsubscribe và gọi `InvokeAsync(StateHasChanged)`, nên các page/component kế thừa `AppComponentBase` không phải tự viết lại boilerplate.
- Layout/NavMenu có lifecycle riêng và vẫn subscribe trực tiếp vì chúng không kế thừa `AppBase`.
- Không reload nghiệp vụ/API chỉ để đổi ngôn ngữ.

## 9. Definition of Done

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

## 10. Trạng thái triển khai

Nền localization, catalog, persistence, Login selector, authenticated shell, route status và audit script đã được triển khai trên branch `feature/i18n-vi-ja`. Các module Presentation còn lại phải được rà theo checklist mục 6 trước khi tuyên bố release-ready.

## 11. Áp dụng thay đổi từ Language Center khi đang chạy

- Language Center ghi vào các file `{lang}.{module}.json` trên **máy chủ API** (thư mục `Localization` trong thư mục publish của API, hoặc `Localization:SourceRoot`). Catalog nhúng trong assembly `FVN_REGISTER.Shared` chỉ là bản mặc định.
- Client tải bản đang hiệu lực qua `GET api/localization/runtime?sinceVersion=` (công khai, chỉ đọc, trả về `Unchanged` khi version không đổi) và `LocalizationStore.ApplyOverrides` đặt nó lên trên catalog nhúng. Thứ tự ưu tiên: ja overlay → ja nhúng → vi overlay → vi nhúng → chính key.
- Fallback được kiểm thử: nếu key không có ở JA thì dùng VI; nếu không có ở cả hai thì trả chính key. Runtime overlay cũng tuân theo cùng thứ tự này.
- `LanguageService.InitializeAsync` tải tối đa mỗi 30 giây; Language Center gọi `RefreshOverridesAsync(force: true)` sau khi lưu/xóa/nhập nên người sửa thấy ngay. Người dùng khác thấy khi tải trang hoặc điều hướng kế tiếp.
- Xóa một key trên Language Center **không** làm key biến mất khỏi giao diện nếu catalog nhúng vẫn còn key đó; chỉ khi build lại thì key mới hết.
- Mọi thao tác ghi tuần tự hóa bằng một khóa và ghi file nguyên tử (file tạm rồi thay thế). Chỉ chạy **một** tiến trình API ghi vào thư mục này.
- Thư mục `Localization` của API nằm trong thư mục publish; nếu quy trình deploy ghi đè thư mục đó, các chỉnh sửa trên máy chủ sẽ mất. Đặt `Localization:SourceRoot` tới một thư mục ngoài thư mục deploy (và sao chép bản đã chỉnh về repo định kỳ) để tránh.
