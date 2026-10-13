# Tự động chụp UI FVN_REGISTER bằng Playwright

Workflow `.github/workflows/ui-screenshots.yml` chụp các trang UI cùng domain có thể khám phá từ liên kết HTML, lưu ảnh PNG full-page và đóng gói `UI_Screenshots.zip`. Workflow chạy khi push có thay đổi liên quan UI trên nhánh `feature/equipment-change-approval-workflow`, hoặc chạy thủ công trong tab **Actions → UI Screenshots → Run workflow**.

## 1. Điều kiện bắt buộc

Workflow này **không tự triển khai ứng dụng lên Staging**. Nó truy cập một bản Staging đã được triển khai và đang hoạt động. Cần thiết lập repository secret:

- `STAGING_BASE_URL`: URL gốc của ứng dụng Staging, ví dụ `https://staging.example.com`.

Không dùng URL localhost của máy cá nhân trong GitHub-hosted runner. Nếu muốn pipeline tự deploy, cần bổ sung bước deploy phù hợp hạ tầng thật (IIS, Azure App Service, container...) và cấu hình quyền/secret tương ứng.

## 2. Đăng nhập để chụp màn hình yêu cầu xác thực

Có thể thiết lập repository secrets:

- `UI_LOGIN_USERNAME`: tài khoản kiểm thử chuyên dụng.
- `UI_LOGIN_PASSWORD`: mật khẩu tài khoản kiểm thử.

Nếu trang đăng nhập dùng selector khác mặc định, thiết lập repository Variables:

- `UI_USERNAME_SELECTOR`
- `UI_PASSWORD_SELECTOR`
- `UI_SUBMIT_SELECTOR`
- `UI_POST_LOGIN_SELECTOR` (selector chỉ xuất hiện sau khi đăng nhập thành công, không bắt buộc).

Selector mặc định dò các input username/email, password và nút submit thông dụng. Nên dùng tài khoản staging có quyền xem các màn hình cần tài liệu; không dùng tài khoản production hoặc tài khoản cá nhân. Không ghi mật khẩu vào source code, log hoặc artifacts.

## 3. Chạy trên máy phát triển

Yêu cầu Node.js 22 và URL Staging có thể truy cập:

```bash
npm ci
npx playwright install chromium
BASE_URL=https://staging.example.com npm run ui:capture
```

PowerShell:

```powershell
$env:BASE_URL = "https://staging.example.com"
npm ci
npx playwright install chromium
npm run ui:capture
```

Nếu cần đăng nhập, đặt thêm biến môi trường `UI_LOGIN_USERNAME` và `UI_LOGIN_PASSWORD`.

## 4. Tải kết quả từ GitHub Actions

1. Mở repository → **Actions** → chọn lần chạy **UI Screenshots** thành công.
2. Trong phần **Artifacts**, tải `UI_Screenshots-<run_number>`.
3. Giải nén để lấy `UI_Screenshots.zip`, ảnh PNG và `ui-capture-report.json`.

ZIP chứa thư mục ảnh và báo cáo URL/tiêu đề/trạng thái HTTP của các trang đã chụp. Artifact được lưu 30 ngày; tải về và lưu lại nếu cần dùng lâu dài.

## 5. Phạm vi và giới hạn

- Script bắt đầu từ URL gốc, theo các liên kết cùng domain và chụp tối đa 250 URL (có thể đổi `UI_MAX_PAGES`).
- Các trang chỉ mở bằng thao tác JavaScript không có liên kết HTML, các menu chưa mở, tab/modal, dữ liệu cần thao tác trước và route không được liên kết có thể không tự khám phá được. Nên bổ sung danh sách route nghiệp vụ/kịch bản tương tác rõ ràng khi cần bao phủ đầy đủ.
- Trang cần đăng nhập sẽ chỉ được chụp sau khi cấu hình thông tin đăng nhập hợp lệ.
- Đây là công cụ chụp ảnh tài liệu, không thay thế kiểm thử nghiệp vụ hay xác nhận rằng mọi trang đã được chụp. Kiểm tra `ui-capture-report.json` và số ảnh sau mỗi lần chạy.
- Không đưa dữ liệu nhân sự/thiết bị thật hoặc thông tin nhạy cảm vào môi trường chụp. Dùng dữ liệu staging đã ẩn danh.
