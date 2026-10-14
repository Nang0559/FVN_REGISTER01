# FVN_REGISTER Playwright UI capture

Bộ kiểm thử nằm trên nhánh `test/playwright-ui` và sử dụng Playwright Chromium để đăng nhập bằng tài khoản kiểm thử, duyệt các trạng thái giao diện nội bộ an toàn và lưu ảnh chụp màn hình.

## 1. Cấu hình GitHub Actions

Trong **Settings → Secrets and variables → Actions → Repository secrets**, tạo hai secret:

- `PLAYWRIGHT_USERNAME`: mã nhân viên/tên đăng nhập của tài khoản kiểm thử.
- `PLAYWRIGHT_PASSWORD`: mật khẩu tài khoản kiểm thử.

Không đưa thông tin đăng nhập vào mã nguồn, workflow input, commit hoặc log. Nên dùng tài khoản kiểm thử chỉ có quyền xem cần thiết và môi trường staging/test thay vì dữ liệu production.

Mặc định URL là `https://dangkyphep.fcc-vn.com.vn`. Khi chạy thủ công có thể nhập `base_url` để dùng môi trường khác. Với máy chủ không phải localhost, URL phải dùng HTTPS.

## 2. Chạy chụp toàn bộ giao diện có thể truy cập

1. Mở **Actions → Playwright UI Capture**.
2. Chọn **Run workflow** và chọn nhánh `test/playwright-ui`.
3. Nhập `base_url` nếu muốn chụp môi trường khác; để trống sẽ dùng URL mặc định.
4. Đặt giới hạn `max_states` và `max_click_targets` nếu cần. Giá trị mặc định lần lượt là 180 trạng thái và 500 lượt tương tác an toàn.
5. Chạy workflow và đợi job hoàn tất.
6. Tải artifact có tên bắt đầu bằng `playwright-ui-capture-`. Ảnh nằm trong `test-results/screenshots/`; báo cáo HTML và trace chẩn đoán được đính kèm khi Playwright tạo ra.

Workflow cũng tự chạy khi có thay đổi trong `tests/playwright/**` hoặc chính workflow trên nhánh này.

## 3. Chạy trên máy phát triển

Từ thư mục `tests/playwright`:

```bash
npm install
npx playwright install chromium
```

PowerShell:

```powershell
$env:PLAYWRIGHT_BASE_URL = "https://your-test-host"
$env:PLAYWRIGHT_USERNAME = "your-test-user"
$env:PLAYWRIGHT_PASSWORD = "your-test-password"
npx playwright test specs/authenticated-click-capture.spec.ts --project=chromium
```

Có thể điều chỉnh giới hạn qua biến môi trường:

- `PLAYWRIGHT_MAX_STATES` — số trạng thái DOM/URL khác nhau tối đa (mặc định 180).
- `PLAYWRIGHT_MAX_CLICK_TARGETS` — số lần nhấp tối đa (mặc định 500).
- `PLAYWRIGHT_MAX_DEPTH` — độ sâu duyệt tối đa (mặc định 12).

## 4. Phạm vi và giới hạn an toàn

- Crawler đăng nhập một lần, chụp trạng thái đầu tiên sau đăng nhập, rồi khám phá liên kết, nút và tab hiển thị trong cùng origin; sau mỗi lượt thử, nó cố gắng khôi phục màn hình cha.
- Các thao tác có dấu hiệu ghi dữ liệu/nghiệp vụ như lưu, tạo, sửa, xóa, gửi, duyệt/từ chối, phân công, cấp/thu hồi quyền, đồng bộ, nhập/tải lên và đăng xuất bị bỏ qua. Không thể bảo đảm bộ lọc ngôn ngữ nhận diện mọi nút nghiệp vụ, vì vậy hãy dùng tài khoản chỉ có quyền đọc và môi trường test.
- Chỉ các màn hình mà tài khoản có quyền truy cập và có thể khám phá qua giao diện mới được chụp. Các trang yêu cầu quyền khác, luồng cần dữ liệu đầu vào, iframe, cửa sổ mới hoặc điều hướng không hiển thị có thể cần kịch bản chuyên biệt.
- Đây là crawler khám phá giao diện theo giới hạn, không phải chứng minh tuyệt đối rằng mọi tổ hợp chức năng đều đã được chụp. Tăng giới hạn có thể tăng thời gian chạy đáng kể.
- Nếu đăng nhập thất bại, kiểm tra secret, URL và quyền của tài khoản. Khi có lỗi, tải artifact để kiểm tra ảnh/trace và log của job.

