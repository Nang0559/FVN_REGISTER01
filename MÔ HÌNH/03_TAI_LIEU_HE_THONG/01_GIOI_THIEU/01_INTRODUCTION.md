# FVN REGISTER
## Từ xử lý thủ công đến quy trình số hóa có thể kiểm soát

> **Một nền tảng dùng chung để đăng ký, phê duyệt, theo dõi thực hiện và đối soát nghiệp vụ — giảm thao tác lặp lại, làm rõ trách nhiệm và tạo dữ liệu để cải tiến.**

---

**Bộ tài liệu:** [Cổng giới thiệu](00_README.md) · [Bản đồ nghiệp vụ](02_FEATURES.md) · [Thông điệp và kế hoạch áp dụng](03_PR_AND_LAUNCH.md) · [Trình chiếu Ban giám đốc](04_PRESENTATION.md) · [Đo KPI](05_KPI_BASELINE_TEMPLATE.md) · [Sẵn sàng và tuyên bố chức năng](08_READINESS_AND_CLAIMS.md) · [Đánh giá hiệu quả đầu tư](09_BUSINESS_CASE_COST_REDUCTION.md)

## 1. Bài toán hiện tại

Khi công việc được xử lý chủ yếu bằng phiếu giấy, Excel và trao đổi trực tiếp, mỗi yêu cầu kéo theo nhiều công đoạn: lập phiếu, chuyển hồ sơ, hỏi tiến độ, nhập lại dữ liệu, tổng hợp báo cáo và tìm hồ sơ khi cần xác minh.

| Công việc thủ công | Tác động vận hành |
|---|---|
| Lập và chuyển phiếu, hỏi tình trạng | Tốn thời gian giao dịch và theo dõi |
| Nhập lại cùng thông tin vào nhiều nơi | Tăng công sức và nguy cơ sai lệch |
| Theo dõi qua Excel hoặc trao đổi riêng | Khó nhìn thấy hồ sơ đang chờ và người phụ trách |
| Đối chiếu kế hoạch với kết quả thực tế | Tốn công tìm chênh lệch và xác nhận |
| Lưu giấy tờ, hình ảnh, lịch sử rời rạc | Khó truy xuất và kiểm tra lại |
| Nhắc lịch, theo dõi thiết bị thủ công | Có nguy cơ bỏ sót công việc định kỳ |

**Chi phí ẩn không chỉ là giấy in:** còn là phút lao động lặp lại, thời gian tìm kiếm, nhắc việc và xử lý lại. Mức độ thực tế cần được đo tại từng bộ phận.

## 2. FVN REGISTER thay đổi cách làm như thế nào?

| Trước — xử lý thủ công | Sau — quy trình số hóa |
|---|---|
| Phiếu giấy, Excel, trao đổi nhiều kênh | Yêu cầu được ghi nhận trong module nghiệp vụ |
| Tự hỏi ai đang giữ hồ sơ | Theo dõi trạng thái và luồng phê duyệt |
| Nhập lại và tổng hợp thủ công | Tập trung thông tin, hỗ trợ báo cáo theo quyền |
| Tự dò kế hoạch và thực tế | Đưa trường hợp sai lệch vào luồng theo dõi |
| Tìm chứng từ, hình ảnh từ nhiều nơi | Tra cứu lịch sử và bằng chứng đã ghi nhận |

### Vòng đời nghiệp vụ mục tiêu

**Đăng ký → Kiểm tra điều kiện → Phê duyệt → Theo dõi thực hiện → Đối soát → Xử lý sai lệch → Báo cáo**

Điểm cốt lõi không phải chỉ thay phiếu giấy bằng biểu mẫu điện tử. Giá trị nằm ở việc kết nối các bước, làm rõ trách nhiệm, lưu lịch sử và giúp phát hiện việc còn tồn đọng.

## 3. Mỗi vai trò được lợi gì?

| Vai trò | Công việc thủ công cần giảm | Giá trị từ hệ thống |
|---|---|---|
| **Nhân viên** | Lập/chuyển phiếu, hỏi trạng thái, tìm lại thông tin | Gửi yêu cầu và tự theo dõi tiến độ tại một nơi |
| **Người phê duyệt** | Tìm yêu cầu, xác nhận qua nhiều kênh | Tập trung yêu cầu cần quyết định và xem ngữ cảnh xử lý |
| **Quản lý bộ phận** | Thu thập tình hình từ nhiều người/bảng tính | Theo dõi tổng quan, hồ sơ tồn đọng và vấn đề cần chú ý |
| **HR** | Nhập liệu, tổng hợp, đối chiếu từng dòng | Tập trung xác minh sai lệch, ghi nhận quyết định và chuẩn bị dữ liệu theo quy trình |
| **Quản lý thiết bị** | Tìm mã tài sản, lịch kiểm tra, bằng chứng và lịch sử | Theo dõi hồ sơ thiết bị, QR, nhiệm vụ kiểm tra và lịch sử đã ghi nhận |
| **Quản trị hệ thống** | Cấp quyền và cấu hình không nhất quán | Quản lý quyền, phạm vi dữ liệu và chính sách theo trách nhiệm |
| **IT quản trị Endpoint** | Tập hợp inventory máy Windows, phần mềm và dịch vụ từ nhiều nguồn | Hướng tới đối chiếu với chính sách được duyệt và theo dõi cảnh báo/ngoại lệ sau khi pilot được xác nhận |
| **Ban giám đốc** | Chờ báo cáo tổng hợp thủ công | Có nền tảng chỉ số để giám sát và đánh giá cải tiến |

Giá trị có thể khác nhau theo chức năng đã triển khai, dữ liệu sẵn có và cấu hình từng bộ phận. Không nên hiểu rằng mọi thao tác thủ công đã được tự động hóa hoàn toàn.

## 4. Phạm vi nghiệp vụ trên cùng nền tảng

- **Nghỉ phép:** đăng ký, phê duyệt, theo dõi trạng thái và lịch nghiệp vụ.
- **Làm thêm giờ (OT):** đăng ký, kiểm tra quy tắc/hạn mức được cấu hình, phê duyệt và hỗ trợ đối chiếu với dữ liệu thực tế.
- **Công tác:** theo dõi yêu cầu, quyết định phê duyệt và trạng thái thực hiện; đối chiếu khi có dữ liệu thực tế phù hợp.
- **Lịch làm việc chung:** tổng hợp lịch công ty, ca làm và trạng thái nghiệp vụ theo ngày; lịch không thay thế dữ liệu gốc của từng module.
- **Chấm công, đối soát và HR Review:** hỗ trợ xem chênh lệch, theo dõi trường hợp cần xác minh, trách nhiệm và bằng chứng. HRM/nguồn chấm công chính thức vẫn là nguồn chuẩn của dữ liệu và quy tắc tính công.
- **Thiết bị và kiểm tra:** quản lý yêu cầu/phê duyệt, QR, thông tin thiết bị, kiểm tra định kỳ, bằng chứng, sửa chữa và lịch sử theo phạm vi đã triển khai.
- **Kiểm kê và tuân thủ máy Windows (Endpoint):** năng lực IT nhằm thu thập inventory máy, phần mềm và Windows Service qua Agent, đối chiếu với chính sách được phê duyệt và theo dõi cảnh báo/ngoại lệ. Chỉ công bố là vận hành chính thức sau pilot, kiểm thử bảo mật và nghiệm thu trên đúng branch.
- **Trung tâm công việc và thông báo:** tập trung việc cần xử lý, trạng thái và nhắc việc theo cấu hình.
- **Bảng điều khiển và báo cáo:** tổng hợp theo quyền và phạm vi dữ liệu; quyền xem và xuất dữ liệu được kiểm soát riêng.
- **Dữ liệu phục vụ kỳ tính lương:** hỗ trợ tạo snapshot khi các điều kiện sẵn sàng được đáp ứng; không thay thế hệ thống HRM/payroll chính thức.

## 5. Giá trị quản trị cốt lõi

**Năng suất:** giảm các thao tác chuyển phiếu, hỏi tiến độ, nhập lại và tổng hợp lặp lại.

**Minh bạch:** rõ trạng thái, người phụ trách và lịch sử quyết định.

**Chất lượng dữ liệu:** kiểm tra đầu vào và làm rõ chênh lệch giữa kế hoạch với thực tế.

**Kiểm soát vận hành:** tập trung việc cần xử lý và trường hợp chưa hoàn tất theo cấu hình.

**Truy xuất:** dễ tìm thông tin, lịch sử và bằng chứng đã ghi nhận.

**Cải tiến liên tục:** tạo dữ liệu để đo tải công việc, thời gian xử lý và chất lượng quy trình.

## 6. Đánh giá hiệu quả áp dụng

Chưa có dữ liệu đường cơ sở được xác nhận trong tài liệu này. Vì vậy, không đưa số giờ tiết kiệm, phần trăm giảm lỗi hay ROI giả định thành kết quả thực tế.

| Chỉ số | Cách đo |
|---|---|
| Phút lao động/giao dịch | Tổng phút làm việc thực tế của các vai trò / số giao dịch |
| Lượt thao tác thủ công | Số lượt chuyển, nhập lại, nhắc, tìm hồ sơ và đối chiếu |
| Thời gian chờ phê duyệt | Từ lúc gửi đến lúc ra quyết định; tách khỏi thời gian lao động |
| Tỷ lệ làm lại | Hồ sơ cần sửa/xử lý lại / tổng hồ sơ |
| Tỷ lệ đúng hạn | Công việc hoàn thành đúng hạn / công việc đến hạn |
| Giờ tổng hợp báo cáo | Tổng thời gian lao động thực tế trong kỳ |
| Thời gian truy xuất | Thời gian tìm hồ sơ/bằng chứng theo mẫu kiểm tra |
| Tỷ lệ sử dụng hệ thống | Giao dịch được xử lý trên hệ thống / tổng giao dịch thuộc phạm vi |

### Công thức quy đổi

- **Giờ công giải phóng** = số giao dịch × (phút lao động trước − phút lao động sau) / 60.
- **Giá trị năng lực quy đổi** = giờ công giải phóng × chi phí lao động/giờ đã được doanh nghiệp thống nhất.
- **Lợi ích ròng** = lợi ích định lượng được chấp nhận − chi phí triển khai, đào tạo, vận hành và duy trì trong cùng kỳ phân tích.

Giờ công giải phóng là năng lực có thể chuyển sang công việc khác, không tự động đồng nghĩa với tiết kiệm tiền mặt. Không tính trùng giờ công với chi phí làm lại.

## 7. Đề xuất với Ban giám đốc

**Đo đường cơ sở → Thử nghiệm có kiểm soát → Đo lại → Xác nhận kết quả → Quyết định mở rộng**

1. Chọn một vài nghiệp vụ có khối lượng đủ đại diện.
2. Ghi nhận quy trình hiện tại, thời gian lao động và số giao dịch.
3. Sau khi áp dụng, đo cùng chỉ số và cùng cách tính.
4. Xác nhận số liệu với bộ phận sử dụng và người phụ trách tài chính/nhân sự.
5. Báo cáo rõ lợi ích đã đo, chi phí phát sinh và phần việc còn thủ công.

> **FVN REGISTER không chỉ số hóa biểu mẫu. Hệ thống hướng tới kết nối quy trình, làm rõ trách nhiệm và tạo dữ liệu để quản lý hiệu quả hơn.**
