# Tự động chụp và lập danh mục giao diện FVN_REGISTER

Pipeline này chạy Playwright trên GitHub Actions, truy cập ứng dụng Staging đã triển khai, phát hiện route từ mã Razor và liên kết HTML, mở một số menu/tab an toàn, chụp PNG full-page và tạo ZIP cùng báo cáo HTML/JSON.

## 1. Thiết lập một lần

Vào **GitHub → Nang0559/FVN_REGISTER01 → Settings → Secrets and variables → Actions**.

Thêm **Repository secrets**:
- `STAGING_BASE_URL`: URL HTTPS của bản Staging đã chạy, ví dụ `https://staging.example.com`.
- `UI_LOGIN_USERNAME`: tài khoản kiểm thử riêng, có quyền xem các màn hình cần tài liệu.
- `UI_LOGIN_PASSWORD`: mật khẩu tài khoản kiểm thử.

Hai secret đăng nhập có thể bỏ trống nếu ứng dụng không cần đăng nhập, nhưng khi đó màn hình sau xác thực sẽ không được thu thập.

Nếu biểu mẫu đăng nhập dùng selector khác mặc định, thêm **Repository variables**:
- `UI_USERNAME_SELECTOR`
- `UI_PASSWORD_SELECTOR`
- `UI_SUBMIT_SELECTOR`
- `UI_POST_LOGIN_SELECTOR`: selector xuất hiện sau khi đăng nhập thành công, khuyến nghị cấu hình.

Dùng staging với dữ liệu nhân sự/thiết bị đã ẩn danh. Không dùng tài khoản production hoặc ghi thông tin bí mật vào source code.

## 2. Chạy để lấy ZIP

1. Mở tab **Actions** của repository.
2. Chọn workflow **UI Screenshots**.
3. Chọn **Run workflow** → branch `feature/equipment-change-approval-workflow` → **Run workflow**.
4. Mở lần chạy vừa tạo, chờ job **Capture staging UI** hoàn tất.
5. Ở phần **Artifacts**, tải `UI_Screenshots-<số lần chạy>`.
6. Giải nén gói artifact. Bên trong có `UI_Screenshots.zip`, thư mục ảnh PNG và các báo cáo.
7. Giải nén tiếp `UI_Screenshots.zip` để xem `ui-screenshots/`, `index.html` và `ui-capture-report.json`.

Artifact được giữ 30 ngày. Tải ZIP về máy để lưu lâu dài hoặc đính kèm vào báo cáo/hướng dẫn sử dụng.

## 3. Khi nào workflow tự chạy?

Khi push thay đổi liên quan đến UI, Shared, Application, API, Playwright, package hoặc workflow lên nhánh `feature/equipment-change-approval-workflow`, workflow tự chạy. Có thể chạy thủ công bất cứ lúc nào qua **Actions → UI Screenshots → Run workflow**.

**Lưu ý:** workflow chụp bản Staging hiện có; nó không tự build/deploy ứng dụng lên IIS. URL Staging phải truy cập được từ GitHub-hosted runner. URL localhost trên máy cá nhân không dùng được trong runner này.

## 4. Phạm vi phát hiện và tương tác

- Tự đọc các chỉ thị `@page` trong `FVN_REGISTER.Web/**/*.razor` để tìm route tĩnh.
- Đi theo liên kết cùng domain được render trong HTML.
- Thử mở các control khai báo là tab, dropdown/menu và nút mở điều hướng; không click tùy tiện các nút submit, duyệt, xóa hay cập nhật nghiệp vụ.
- Chụp tối đa 500 route và tối đa 8 trạng thái tương tác an toàn trên mỗi trang; các ngưỡng nằm trong workflow.
- Route chứa tham số như `/equipment/{id}` không được tự điền ID, vì vậy cần bổ sung URL dữ liệu mẫu an toàn vào `playwright/ui-routes.json` nếu muốn chụp các trang chi tiết.
- Các màn hình phụ thuộc vai trò khác nhau cần tài khoản kiểm thử riêng và chạy riêng theo vai trò; một tài khoản không thể thấy màn hình mà nó không có quyền truy cập.
- Một số component có điều hướng chỉ qua callback hoặc menu phức tạp vẫn cần cấu hình thêm selector/kịch bản. Kiểm tra báo cáo và xác nhận danh mục route trước khi dùng làm tài liệu chính thức.

## 5. Đọc báo cáo

- `index.html`: danh sách ảnh, URL, tiêu đề, trạng thái HTTP và lỗi ghi nhận; mở bằng trình duyệt sau khi giải nén ZIP.
- `ui-capture-report.json`: dữ liệu chi tiết để xử lý tự động hoặc đối chiếu giữa các lần chạy.
- `ui-screenshots/*.png`: ảnh full-page. Tên ảnh dựa trên route và trạng thái UI.

HTTP 200 không đồng nghĩa nghiệp vụ đúng; đây là công cụ lập tài liệu giao diện, không thay thế kiểm thử chức năng.
