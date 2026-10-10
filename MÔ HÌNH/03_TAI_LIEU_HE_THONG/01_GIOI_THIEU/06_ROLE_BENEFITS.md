# FVN REGISTER — LỢI ÍCH THEO VAI TRÒ

> Tài liệu hỗ trợ giới thiệu nội bộ. Đây là lợi ích mục tiêu cần được xác nhận khi nghiệm thu và đo vận hành, không phải cam kết tiết kiệm định lượng.

## 1. Thông điệp tổng thể

FVN REGISTER hướng tới thay chuỗi xử lý phân tán bằng quy trình có thể theo dõi:

**Yêu cầu → Phê duyệt → Thực hiện → Đối soát → Xử lý → Báo cáo**

Giá trị của một vai trò không chỉ đến từ màn hình riêng, mà từ việc dữ liệu và trạng thái giữa các bước được kết nối.

## 2. Ma trận giá trị theo vai trò

| Vai trò | Trước đây — thao tác thủ công điển hình | FVN REGISTER hỗ trợ | Giá trị cần đo |
|---|---|---|---|
| Nhân viên | Lập phiếu, chuyển giấy, hỏi tình trạng, tìm lại nội dung đã gửi | Tạo yêu cầu theo nghiệp vụ, xem trạng thái và lịch liên quan | Phút tạo/theo dõi một yêu cầu; số lượt hỏi trạng thái |
| Người phê duyệt | Nhận hồ sơ qua nhiều kênh, tra thông tin, xác nhận thủ công | Tập trung danh sách cần xử lý, xem nội dung và quyết định theo quyền | Thời gian xử lý một yêu cầu; số việc quá hạn |
| Quản lý bộ phận | Hỏi từng người và tổng hợp nhiều bảng | Theo dõi tình hình, yêu cầu tồn và báo cáo được cấp quyền | Giờ tổng hợp; thời gian phát hiện tồn đọng |
| HR | Thu thập phiếu, nhập số liệu, dò lệch kế hoạch/thực tế | Theo dõi Leave/OT/Trip và trường hợp đối soát, lưu kết quả xem xét theo quy trình | Phút đối soát; tỷ lệ làm lại; thời gian tìm bằng chứng |
| Quản lý thiết bị | Tìm hồ sơ thiết bị, lịch kiểm tra, sửa chữa và ảnh minh chứng ở nhiều nơi | Tra cứu hồ sơ, QR, nhiệm vụ kiểm tra và lịch sử được ghi nhận | Thời gian truy xuất; tỷ lệ nhiệm vụ hoàn thành đúng hạn |
| IT quản trị Endpoint | Tập hợp inventory máy Windows, phần mềm và dịch vụ; rà soát phần mềm ngoài danh mục | Đối chiếu inventory với chính sách được duyệt, theo dõi cảnh báo/ngoại lệ sau khi pilot hoàn tất | Tỷ lệ endpoint có inventory hợp lệ; cảnh báo cần xử lý; thời gian xử lý ngoại lệ |
| Quản trị hệ thống | Cấp quyền và điều chỉnh cấu hình thiếu tập trung | Kiểm soát capability và phạm vi dữ liệu, cấu hình theo chính sách | Thời gian xử lý yêu cầu phân quyền; số lỗi cấp quyền |
| Ban giám đốc | Nhận số liệu sau khi các bộ phận tổng hợp | Xem báo cáo/tổng quan trong phạm vi được cung cấp và theo dõi chỉ số cải tiến | Thời gian chuẩn bị báo cáo; mức đầy đủ và đúng hạn của KPI |

## 3. Giá trị theo chuỗi quy trình

### Đăng ký
Thu thập dữ liệu có cấu trúc và kiểm tra điều kiện theo chức năng đã triển khai. Mục tiêu: giảm hồ sơ thiếu thông tin và yêu cầu phải làm lại.

### Phê duyệt
Theo dõi trạng thái và trách nhiệm rõ ràng. Mục tiêu: giảm thao tác chuyển hồ sơ và việc hỏi tiến độ; thời gian chờ phải được đo tách khỏi thời gian lao động.

### Thực hiện
Phân biệt kế hoạch đã được duyệt với sự kiện thực tế. Mục tiêu: tránh coi “đã duyệt” đồng nghĩa với “đã thực hiện đúng”.

### Đối soát và xử lý sai lệch
Tập trung trường hợp cần xác minh, người xử lý và bằng chứng. Mục tiêu: giảm thời gian tìm chênh lệch và làm rõ kết quả xử lý.

### Báo cáo và cải tiến
Dùng dữ liệu nhất quán để theo dõi khối lượng, thời gian và tồn đọng. Mục tiêu: ra quyết định dựa trên dữ liệu đã xác nhận, không dựa trên ước đoán.

## 4. Các giới hạn cần nói rõ

- Work Calendar là góc nhìn tổng hợp; dữ liệu gốc thuộc module nghiệp vụ.
- HRM/nguồn chấm công chính thức tiếp tục là nguồn chuẩn cho dữ liệu và quy tắc tính công.
- Mức tự động hóa phụ thuộc tích hợp dữ liệu, cấu hình và việc nghiệm thu.
- Dashboard chỉ hiển thị dữ liệu/capability đã cấp; không đồng nghĩa mọi quản lý đều xem được mọi dữ liệu.
- QR, checklist, import Excel, schema, evidence và lịch sử thiết bị chỉ được công bố là vận hành chính thức sau khi xác nhận trên môi trường triển khai.
- Tài liệu không khẳng định hệ thống thay thế HRM hay phần mềm tính lương.

## 5. Câu hỏi cần xác nhận trước khi công bố

| Nội dung | Người xác nhận | Bằng chứng |
|---|---|---|
| Nghiệp vụ/module đã sẵn sàng sử dụng | Chủ nghiệp vụ / IT | UAT hoặc danh sách nghiệm thu |
| Quy trình phê duyệt thực tế | Chủ nghiệp vụ | Cấu hình và kịch bản kiểm thử |
| Nguồn dữ liệu HRM và giới hạn tích hợp | HR / IT | Mô tả luồng dữ liệu |
| Báo cáo, export và phạm vi quyền | IT / chủ báo cáo | Ma trận quyền và kiểm thử |
| Quy trình thiết bị/QR/checklist | Quản lý thiết bị / IT | Kịch bản nghiệm thu |
| KPI trước–sau | Bộ phận sử dụng / tài chính | Phiếu đo và nguồn xác nhận |

**Thông điệp thống nhất:** mỗi vai trò bớt thao tác tìm kiếm và theo dõi thủ công, còn doanh nghiệp có thêm khả năng kiểm soát trạng thái, trách nhiệm và hiệu quả quy trình.
