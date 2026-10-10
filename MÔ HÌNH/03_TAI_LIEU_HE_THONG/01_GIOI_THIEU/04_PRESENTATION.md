---
marp: true
theme: default
paginate: true
size: 16:9
style: |
  section {
    font-family: "Aptos", "Yu Gothic UI", "Meiryo", sans-serif;
    color: #183247;
    font-size: 20px;
    line-height: 1.25;
    padding: 42px 56px;
    background: #FFFFFF;
  }
  h1 {
    color: #123B5D;
    font-size: 34px;
    margin-bottom: 14px;
  }
  h2 {
    color: #187C80;
    font-size: 24px;
    margin-top: 8px;
  }
  strong { color: #123B5D; }
  table {
    width: 100%;
    border-collapse: collapse;
    margin-top: 18px;
    font-size: 18px;
  }
  th {
    background: #123B5D;
    color: #FFFFFF;
    border: 1px solid #D4E0E7;
    padding: 11px 12px;
    text-align: left;
  }
  td {
    border: 1px solid #D4E0E7;
    padding: 10px 12px;
    vertical-align: top;
  }
  tr:nth-child(even) td { background: #F2F7F8; }
  blockquote {
    border-left: 8px solid #D8A45B;
    background: #F7F1E7;
    color: #123B5D;
    padding: 10px 18px;
    margin-top: 18px;
  }
  section.lead {
    background: #123B5D;
    color: #FFFFFF;
    justify-content: center;
  }
  section.lead h1, section.lead h2, section.lead strong {
    color: #FFFFFF;
  }
  section.lead h1 { font-size: 50px; }
  section.lead h2 { font-size: 30px; }
  section.lead blockquote {
    background: transparent;
    color: #FFFFFF;
    border-left-color: #D8A45B;
  }
---

<!-- _class: lead -->

# FVN REGISTER

## Từ xử lý thủ công đến quy trình số hóa có thể kiểm soát

**Một nền tảng · Một luồng xử lý · Dữ liệu để cải tiến**

Nghỉ phép　|　Làm thêm giờ　|　Công tác　|　Chấm công/đối soát　|　Thiết bị

> Giảm thao tác lặp lại. Làm rõ trách nhiệm. Đo hiệu quả thực tế.

---

# 01 — BÀI TOÁN KHÔNG CHỈ LÀ GIẤY TỜ

## Một yêu cầu thường kéo theo nhiều công đoạn

| Điểm phát sinh | Công việc thủ công | Tác động vận hành |
|:--|:--|:--|
| Đăng ký | Lập phiếu, bổ sung thông tin, chuyển hồ sơ | Tốn thời gian của người gửi |
| Phê duyệt | Tìm yêu cầu, hỏi tiến độ, nhắc người xử lý | Phát sinh giao dịch và thời gian chờ |
| Tổng hợp | Nhập lại dữ liệu vào nhiều bảng | Tăng công sức và nguy cơ sai lệch |
| Đối chiếu | Tự tìm chênh lệch giữa kế hoạch và thực tế | Tốn công xác minh, xử lý lại |
| Tra cứu | Tìm hồ sơ, hình ảnh và lịch sử ở nhiều nơi | Khó truy xuất và kiểm tra lại |

> **Chi phí ẩn = xử lý lặp lại + nhập lại + tìm kiếm + nhắc việc + làm lại**

Đây là các nhóm chi phí cần đo tại nhà máy, **không phải số tiết kiệm đã được xác nhận**.

---

# 02 — THAY ĐỔI CÁCH LÀM VIỆC

| TRƯỚC · THỦ CÔNG | CƠ CHẾ THAY ĐỔI | SAU · QUY TRÌNH SỐ HÓA |
|:--|:--|:--|
| Phiếu giấy, Excel, trao đổi riêng | Ghi nhận theo nghiệp vụ | Thông tin tập trung hơn |
| Tự hỏi ai đang xử lý | Trạng thái và luồng phê duyệt | Dễ theo dõi tiến độ |
| Nhập lại, tổng hợp nhiều lần | Tái sử dụng dữ liệu đã ghi nhận | Hạn chế thao tác lặp lại |
| Tự dò tìm chênh lệch | Theo dõi trường hợp cần xác minh | Tập trung vào ngoại lệ |
| Tìm chứng từ ở nhiều nơi | Lưu lịch sử theo quy trình | Tăng khả năng truy xuất |

**Vòng đời kết nối**

**ĐĂNG KÝ → KIỂM TRA → PHÊ DUYỆT → THỰC HIỆN → ĐỐI SOÁT → XỬ LÝ → BÁO CÁO**

---

# 03 — MỘT NỀN TẢNG, NHIỀU NGHIỆP VỤ

| Nghiệp vụ | Quy trình được hỗ trợ | Giá trị hướng tới |
|:--|:--|:--|
| **Nghỉ phép** | Đăng ký, phê duyệt, theo dõi lịch và trạng thái | Giảm hỏi tiến độ và tổng hợp rời rạc |
| **Làm thêm giờ** | Đăng ký, kiểm tra quy tắc được cấu hình, phê duyệt và đối chiếu | Làm rõ kế hoạch đã duyệt và kết quả thực tế |
| **Công tác** | Theo dõi yêu cầu, phê duyệt và thực hiện | Tập trung tình trạng và lịch sử |
| **Lịch / chấm công / đối soát** | Tổng hợp lịch, ca và trường hợp cần xác minh | Nhìn rõ ngoại lệ cần xử lý |
| **Thiết bị** | Hồ sơ, QR, kiểm tra, bằng chứng, sửa chữa và lịch sử theo phạm vi đã xác nhận | Tăng khả năng truy xuất vòng đời thiết bị |
| **Công việc / báo cáo** | Việc cần xử lý, thông báo, bảng điều khiển và báo cáo theo quyền | Hỗ trợ theo dõi tình hình và tồn đọng |

*HRM/nguồn chấm công chính thức vẫn là nguồn chuẩn của dữ liệu và quy tắc tính công. Chức năng cụ thể cần được xác nhận trên môi trường nghiệm thu.*

---

# 04 — GIÁ TRỊ CHO TỪNG VAI TRÒ

| Vai trò | Giảm công việc thủ công | Giá trị nhận được |
|:--|:--|:--|
| **Nhân viên** | Chuyển phiếu, hỏi tình trạng, tìm lại nội dung | Gửi yêu cầu và tự theo dõi trạng thái |
| **Người phê duyệt** | Tìm hồ sơ qua nhiều kênh, nhắc lại thông tin | Tập trung yêu cầu cần quyết định |
| **Quản lý bộ phận** | Hỏi từng người, ghép nhiều bảng | Theo dõi tình hình và việc tồn đọng trong phạm vi được cấp |
| **HR** | Nhập liệu, tổng hợp, dò chênh lệch | Tập trung xác minh và xử lý ngoại lệ |
| **Quản lý thiết bị** | Tìm mã, lịch kiểm tra, bằng chứng và lịch sử | Tra cứu hồ sơ và nhiệm vụ đã ghi nhận |
| **Ban giám đốc** | Chờ các bộ phận tổng hợp số liệu | Có cơ sở đo hiệu quả và ưu tiên cải tiến |

**Lợi ích là giảm việc lặp lại và tăng khả năng kiểm soát — không phải xóa bỏ mọi thao tác của con người.**

---

# 05 — TỪ ĐỐI SOÁT ĐẾN KIỂM SOÁT

| Bước | Nội dung | Vai trò của hệ thống |
|:--|:--|:--|
| **01 · Kế hoạch** | Yêu cầu đã được phê duyệt | Giữ trạng thái và lịch sử quyết định |
| **02 · Thực tế** | Dữ liệu thực tế từ nguồn nghiệp vụ chính thức | Hiển thị dữ liệu phù hợp để đối chiếu |
| **03 · Sai lệch** | Kế hoạch và thực tế chưa khớp | Đưa trường hợp cần xác minh vào luồng theo dõi |
| **04 · Xử lý** | Người phụ trách, lý do và bằng chứng | Hỗ trợ ghi nhận tiến trình và kết quả |
| **05 · Kết quả** | Trường hợp được xem xét và giải quyết | Hỗ trợ truy xuất lịch sử theo quy trình |

> Không đồng nhất “đã phê duyệt” với “đã thực hiện đúng”. HRM vẫn là nguồn chuẩn của dữ liệu và quy tắc tính công.

---

# 06 — HIỆU QUẢ PHẢI ĐƯỢC ĐO

| Chỉ số | Đo trước và sau áp dụng |
|:--|:--|
| **Thời gian xử lý** | Phút lao động thực tế cho mỗi giao dịch |
| **Thao tác lặp lại** | Lượt nhập lại, nhắc việc, tìm hồ sơ và đối chiếu |
| **Thời gian phê duyệt** | Từ lúc gửi đến lúc quyết định; tách riêng thời gian chờ |
| **Chất lượng xử lý** | Tỷ lệ hồ sơ phải sửa, xử lý lại hoặc quá hạn |
| **Báo cáo / truy xuất** | Giờ tổng hợp và thời gian tìm hồ sơ/bằng chứng |
| **Chi phí** | Khoản chi thực tế giảm trừ chi phí triển khai/vận hành |

### Công thức quy đổi giờ công

**Giờ công giải phóng = Số giao dịch × (Phút lao động trước − Phút lao động sau) ÷ 60**

*Giờ công giải phóng là năng lực có thể chuyển sang việc khác; không mặc nhiên là tiết kiệm tiền mặt. Không công bố ROI khi chưa xác nhận số liệu và chi phí.*

---

# 07 — ĐỀ XUẤT: ĐO THỬ, XÁC NHẬN, MỞ RỘNG

| Giai đoạn | Việc cần làm | Kết quả cần có |
|:--|:--|:--|
| **01 · Đo cơ sở** | Ghi khối lượng giao dịch và thời gian xử lý thủ công | Số liệu trước áp dụng |
| **02 · Thử nghiệm** | Chọn nghiệp vụ đã xác nhận sẵn sàng, hướng dẫn người dùng | Phạm vi và cách đo thống nhất |
| **03 · Đo lại** | Dùng cùng định nghĩa KPI và phạm vi so sánh | Kết quả sau áp dụng |
| **04 · Xác nhận** | Bộ phận nghiệp vụ và người phụ trách số liệu kiểm tra | Lợi ích, chi phí và tồn tại đã được xác nhận |
| **05 · Quyết định** | Đánh giá kết quả và phần việc còn thủ công | Duy trì, cải tiến hoặc mở rộng |

---

<!-- _class: lead -->

# FVN REGISTER

## Giảm thao tác lặp lại. Tăng minh bạch. Cải tiến dựa trên dữ liệu.

> Hiệu quả định lượng sẽ được xác nhận bằng kết quả đo trước và sau áp dụng.
