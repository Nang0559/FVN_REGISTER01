# FVN REGISTER — BẢN ĐỒ NGHIỆP VỤ VÀ GIÁ TRỊ

> Tài liệu tham chiếu cho giới thiệu sản phẩm. Mô tả năng lực theo nghiệp vụ, không thay thế kiểm thử nghiệm thu từng chức năng.

## 1. Một hệ thống, một vòng đời xử lý

**Đăng ký → Kiểm tra điều kiện → Phê duyệt → Theo dõi thực hiện → Đối chiếu → Xử lý sai lệch → Báo cáo**

| Lớp năng lực | Vai trò trong quy trình | Giá trị quản trị |
|---|---|---|
| Biểu mẫu và kiểm tra điều kiện | Thu thập thông tin, kiểm tra dữ liệu trước khi gửi | Giảm hồ sơ thiếu hoặc sai ngay từ đầu |
| Phê duyệt điện tử | Chuyển yêu cầu theo chính sách và người có thẩm quyền | Rõ người xử lý, trạng thái và lịch sử quyết định |
| Kế hoạch và kết quả thực tế | Phân biệt điều đã được duyệt với điều thực sự xảy ra | Không đồng nhất “đã duyệt” với “đã thực hiện” |
| Đối soát và xử lý ngoại lệ | So sánh kế hoạch với dữ liệu thực tế | Tập trung vào trường hợp cần xác minh |
| Công việc và thông báo | Tập trung việc cần làm, trạng thái và nhắc việc | Hạn chế bỏ sót, giảm hỏi tiến độ thủ công |
| Lịch và bảng điều khiển | Tổng hợp thông tin theo quyền | Nhìn được lịch, tình hình và việc tồn đọng |
| Báo cáo và xuất dữ liệu | Tổng hợp theo phạm vi dữ liệu được cấp | Hỗ trợ kiểm soát và lập báo cáo |
| Phân quyền và lịch sử | Giới hạn chức năng/phạm vi dữ liệu, lưu vết xử lý | Hỗ trợ truy xuất và trách nhiệm giải trình |

## 2. Nghiệp vụ và giá trị

### Nghỉ phép
Đăng ký, phê duyệt, theo dõi trạng thái và lịch nghiệp vụ; hỗ trợ báo cáo theo phạm vi quyền.

### Làm thêm giờ (OT)
Tiếp nhận đăng ký, kiểm tra theo quy tắc/hạn mức được cấu hình, theo dõi phê duyệt và hỗ trợ đối chiếu với dữ liệu thực tế. Hạn mức phải lấy từ cấu hình nghiệp vụ hiện hành, không suy diễn từ giao diện.

### Công tác
Theo dõi yêu cầu, quyết định phê duyệt và trạng thái thực hiện; khi có dữ liệu thực tế phù hợp, hỗ trợ đối chiếu và làm rõ chênh lệch.

### Lịch làm việc chung
Tổng hợp lịch công ty, ca làm và thông tin nghiệp vụ liên quan theo ngày. Lịch là góc nhìn tổng hợp, không thay thế dữ liệu gốc của từng module.

### Chấm công, đối soát và HR Review
Hỗ trợ xem xét khác biệt giữa kế hoạch và dữ liệu thực tế; theo dõi trường hợp cần xác minh, trách nhiệm và bằng chứng theo chính sách. HRM/nguồn chấm công chính thức vẫn là nguồn chuẩn của dữ liệu và quy tắc tính công.

### Thiết bị và kiểm tra
Nghiệp vụ thiết bị gồm đăng ký/phê duyệt, QR, nhập Excel theo schema phòng ban, kiểm tra dữ liệu theo dòng trước khi ghi nhận, giao thiết bị cho người phụ trách, bàn giao trách nhiệm, checklist theo phiên bản, bằng chứng, sửa chữa, thông báo và báo cáo. Các thao tác import, camera/QR, checklist, bàn giao và notification phải được xác minh từng luồng trên môi trường nghiệm thu trước khi công bố là đã vận hành chính thức.

### Kiểm kê và tuân thủ máy tính đầu cuối (Endpoint)

Năng lực IT được thiết kế để thu thập dữ liệu máy Windows, phần mềm và dịch vụ Windows qua tác nhân cài trên máy; chuẩn hóa dữ liệu; so sánh với danh mục/chính sách được phê duyệt; đưa cảnh báo hoặc ngoại lệ vào quy trình quản trị chung. Dữ liệu kiểm kê thực tế không tự động trở thành danh sách cho phép; tác nhân trên máy không phải kênh thực thi lệnh từ xa tùy ý.

Đây là phạm vi riêng với nghiệp vụ đăng ký thiết bị. Trước khi giới thiệu là đã vận hành, cần xác minh đăng ký kết nối của tác nhân, quản lý thông tin xác thực, dữ liệu kiểm kê không bị ghi trùng khi gửi lại, chính sách/luồng phê duyệt, cảnh báo tuân thủ, thu hồi/hết hạn và thử nghiệm trên máy thật. Xem [08 — Kiểm soát sẵn sàng và tuyên bố chức năng](08_READINESS_AND_CLAIMS.md).

## 3. Giá trị theo vai trò

| Vai trò | Cách làm thủ công hiện tại | Giá trị kỳ vọng |
|---|---|---|
| Nhân viên | Lập/chuyển phiếu, hỏi trạng thái | Gửi yêu cầu và tự theo dõi tiến độ |
| Người phê duyệt | Tìm phiếu, xác nhận qua nhiều kênh | Tập trung yêu cầu cần quyết định |
| Quản lý bộ phận | Tổng hợp từ nhiều nguồn | Theo dõi tình hình và việc tồn đọng |
| HR | Nhập, tổng hợp, đối chiếu | Tập trung xác minh ngoại lệ và kết quả |
| Quản lý thiết bị | Tìm thông tin và lịch sử rời rạc | Tra cứu theo mã QR và hồ sơ đã ghi nhận |
| Quản trị viên | Cấp quyền/cấu hình phân tán | Quản lý quyền và cấu hình theo trách nhiệm |
| Ban giám đốc | Nhận báo cáo sau khi tổng hợp | Có cơ sở theo dõi chỉ số sau khi đo |

## 4. Nguyên tắc kiểm soát

- Quyền phê duyệt không tự động cấp quyền xem mọi dữ liệu.
- Lịch là góc nhìn tổng hợp; trạng thái gốc thuộc module nghiệp vụ.
- Yêu cầu đã duyệt là kế hoạch; dữ liệu thực tế cần đối chiếu theo nguồn chuẩn.
- Sai lệch cần có người xử lý và lý do/bằng chứng theo chính sách.
- Không tự tuyên bố ROI; hiệu quả phải dựa trên số liệu trước/sau.

## 5. Dùng tài liệu nào?

- Giới thiệu tổng thể: 01_INTRODUCTION.md
- Trình chiếu Ban giám đốc: 04_PRESENTATION.md
- Thông điệp và kế hoạch áp dụng: 03_PR_AND_LAUNCH.md
- Đo KPI: 05_KPI_BASELINE_TEMPLATE.md
- Kiểm soát sẵn sàng và tuyên bố chức năng: 08_READINESS_AND_CLAIMS.md
- Đánh giá hiệu quả đầu tư: 09_BUSINESS_CASE_COST_REDUCTION.md

**Thông điệp cốt lõi:** kết nối quy trình để giảm thao tác lặp lại, làm rõ trách nhiệm và tạo dữ liệu cho cải tiến liên tục.
