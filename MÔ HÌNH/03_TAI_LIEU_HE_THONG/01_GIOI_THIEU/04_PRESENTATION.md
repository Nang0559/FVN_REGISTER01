---
marp: true
theme: default
paginate: true
size: 16:9
style: |
  section {
    font-family: "Aptos", "Yu Gothic UI", sans-serif;
    color: #183247;
    padding: 42px 56px;
    background: #FFFFFF;
  }
  h1 { color: #123B5D; font-size: 31px; }
  h2 { color: #187C80; font-size: 23px; }
  strong { color: #123B5D; }
  small { color: #5B6B7A; }
  table { font-size: 18px; }
  section.lead { background: #123B5D; color: #FFFFFF; }
  section.lead h1, section.lead h2, section.lead strong { color: #FFFFFF; }
  section.lead h1 { font-size: 46px; }
  section.lead h2 { font-size: 28px; }
---

<!-- _class: lead -->

# FVN REGISTER

## Từ xử lý thủ công đến quy trình số hóa có thể kiểm soát

**Một nền tảng · Một luồng xử lý · Dữ liệu để cải tiến**

Nghỉ phép　|　Làm thêm giờ　|　Công tác　|　Chấm công/đối soát　|　Thiết bị

---

# 01 — BÀI TOÁN KHÔNG CHỈ LÀ GIẤY TỜ

## Mỗi giao dịch kéo theo nhiều thao tác

<div style="display:flex;gap:18px;align-items:stretch">
<div style="flex:1;border:2px solid #D8A45B;border-radius:12px;padding:18px">

### Cách làm thủ công

- Lập và chuyển phiếu
- Hỏi người duyệt, hỏi tiến độ
- Nhập lại dữ liệu
- Tổng hợp và đối chiếu
- Tìm hồ sơ khi cần kiểm tra

</div>
<div style="flex:1;border:2px solid #187C80;border-radius:12px;padding:18px">

### Tác động vận hành

- Thời gian giao dịch và chờ
- Công việc hành chính lặp lại
- Nguy cơ sai lệch dữ liệu
- Khó thấy việc tồn đọng
- Mất thời gian truy xuất

</div>
</div>

**Chi phí ẩn = xử lý + chờ + nhập lại + tìm kiếm + làm lại**

<small>Đây là nhóm chi phí cần đo tại nhà máy, không phải số liệu tiết kiệm đã được xác nhận.</small>

---

# 02 — THAY ĐỔI CÁCH LÀM VIỆC

<div style="display:flex;gap:14px;align-items:stretch">
<div style="flex:1;background:#F7F0E5;border-radius:12px;padding:18px">

## TRƯỚC

**Phiếu / Excel / trao đổi riêng**

↓  

Chuyển phiếu · Hỏi tiến độ

↓  

Nhập lại · Đối chiếu thủ công

↓  

Tìm hồ sơ · Tổng hợp báo cáo

</div>
<div style="flex:1;background:#E8F4F3;border-radius:12px;padding:18px">

## SAU

**Đăng ký trên hệ thống**

↓

Phê duyệt theo luồng

↓

Theo dõi trạng thái và kết quả

↓

Đối soát · Xử lý ngoại lệ · Báo cáo

</div>
</div>

**Không chỉ thay giấy bằng màn hình — mà kết nối các bước và trách nhiệm.**

---

# 03 — MỘT VÒNG ĐỜI XUYÊN SUỐT

<div style="display:flex;gap:8px;align-items:center;justify-content:center;margin-top:30px">
<div style="flex:1;text-align:center;border:2px solid #187C80;border-radius:10px;padding:15px"><strong>01</strong><br>ĐĂNG KÝ</div>
<div style="font-size:24px">→</div>
<div style="flex:1;text-align:center;border:2px solid #187C80;border-radius:10px;padding:15px"><strong>02</strong><br>PHÊ DUYỆT</div>
<div style="font-size:24px">→</div>
<div style="flex:1;text-align:center;border:2px solid #187C80;border-radius:10px;padding:15px"><strong>03</strong><br>THỰC HIỆN</div>
</div>
<div style="text-align:center;font-size:28px;margin:12px">↓</div>
<div style="display:flex;gap:8px;align-items:center;justify-content:center">
<div style="flex:1;text-align:center;border:2px solid #D8A45B;border-radius:10px;padding:15px"><strong>06</strong><br>BÁO CÁO</div>
<div style="font-size:24px">←</div>
<div style="flex:1;text-align:center;border:2px solid #D8A45B;border-radius:10px;padding:15px"><strong>05</strong><br>XỬ LÝ SAI LỆCH</div>
<div style="font-size:24px">←</div>
<div style="flex:1;text-align:center;border:2px solid #D8A45B;border-radius:10px;padding:15px"><strong>04</strong><br>ĐỐI SOÁT</div>
</div>

<small>Phê duyệt xác lập kế hoạch; dữ liệu thực tế được đối chiếu theo nguồn nghiệp vụ chính thức.</small>

---

# 04 — CÁC NGHIỆP VỤ TRÊN CÙNG NỀN TẢNG

<div style="display:grid;grid-template-columns:1fr 1fr 1fr;gap:12px">
<div style="border:1px solid #C8D7E0;border-radius:10px;padding:14px">

### NGHỈ PHÉP

Đăng ký · Phê duyệt · Lịch

</div>
<div style="border:1px solid #C8D7E0;border-radius:10px;padding:14px">

### LÀM THÊM GIỜ

Đăng ký · Kiểm tra quy tắc · Đối chiếu

</div>
<div style="border:1px solid #C8D7E0;border-radius:10px;padding:14px">

### CÔNG TÁC

Yêu cầu · Phê duyệt · Kết quả

</div>
<div style="border:1px solid #C8D7E0;border-radius:10px;padding:14px">

### LỊCH LÀM VIỆC

Lịch công ty · Ca · Trạng thái

</div>
<div style="border:1px solid #C8D7E0;border-radius:10px;padding:14px">

### CHẤM CÔNG / ĐỐI SOÁT

Nhận diện chênh lệch · HR xem xét

</div>
<div style="border:1px solid #C8D7E0;border-radius:10px;padding:14px">

### THIẾT BỊ

QR · Kiểm tra · Bằng chứng · Lịch sử

</div>
</div>

<small>Công bố chức năng theo trạng thái thực tế đã nghiệm thu. HRM vẫn là nguồn chuẩn của dữ liệu và quy tắc tính công.</small>

---

# 05 — MỖI VAI TRÒ ĐƯỢC GÌ?

| VAI TRÒ | GIÁ TRỊ VẬN HÀNH |
|:--|:--|
| **Nhân viên** | Gửi yêu cầu và tự theo dõi trạng thái |
| **Người phê duyệt** | Tập trung yêu cầu cần quyết định |
| **Quản lý bộ phận** | Thấy tình hình và việc còn tồn đọng |
| **HR** | Tập trung xác minh sai lệch và xử lý ngoại lệ |
| **Quản lý thiết bị** | Tra cứu thiết bị, nhiệm vụ và lịch sử |
| **Ban giám đốc** | Có nền tảng chỉ số để theo dõi cải tiến |

**Ít thao tác theo dõi thủ công hơn; trách nhiệm và trạng thái rõ ràng hơn.**

---

# 06 — TỪ ĐỐI SOÁT ĐẾN KIỂM SOÁT

<div style="text-align:center;margin-top:10px">
<div style="display:inline-block;border:2px solid #187C80;border-radius:10px;padding:11px 26px">KẾ HOẠCH ĐÃ DUYỆT</div>
<div style="font-size:22px">＋</div>
<div style="display:inline-block;border:2px solid #187C80;border-radius:10px;padding:11px 26px">KẾT QUẢ THỰC TẾ TỪ NGUỒN CHUẨN</div>
<div style="font-size:22px">↓</div>
<div style="display:inline-block;background:#E8F4F3;border-radius:10px;padding:11px 26px"><strong>ĐỐI CHIẾU VÀ NHẬN DIỆN CHÊNH LỆCH</strong></div>
<div style="font-size:22px">↓</div>
<div style="display:inline-block;border:2px solid #D8A45B;border-radius:10px;padding:11px 26px">XÁC MINH · BẰNG CHỨNG · NGƯỜI XỬ LÝ</div>
<div style="font-size:22px">↓</div>
<div style="display:inline-block;border:2px solid #187C80;border-radius:10px;padding:11px 26px">HR XEM XÉT VÀ GHI NHẬN KẾT QUẢ</div>
</div>

**Không thay thế HRM:** FVN REGISTER hỗ trợ theo dõi/đối soát; nguồn dữ liệu và quy tắc tính công chính thức vẫn được tôn trọng.

---

# 07 — HIỆU QUẢ PHẢI ĐƯỢC ĐO

<div style="display:flex;gap:14px;align-items:stretch">
<div style="flex:1;border-radius:12px;background:#F7F0E5;padding:18px">

## ĐO TRƯỚC

- Phút xử lý mỗi yêu cầu
- Lượt nhập lại / nhắc việc
- Thời gian chờ phê duyệt
- Giờ lập báo cáo
- Tỷ lệ sai và làm lại

</div>
<div style="flex:1;border-radius:12px;background:#E8F4F3;padding:18px">

## ĐO SAU

- Cùng định nghĩa chỉ số
- Cùng phạm vi nghiệp vụ
- Kỳ đo có thể so sánh
- Có chi phí triển khai/vận hành
- Có người xác nhận kết quả

</div>
</div>

### Quy đổi giờ công

**Giờ công giải phóng = Số giao dịch × (Phút trước − Phút sau) ÷ 60**

<small>Giờ công giải phóng là năng lực có thể tái sử dụng, không tự động đồng nghĩa với tiết kiệm tiền mặt.</small>

---

# 08 — ĐỀ XUẤT: ĐO THỬ, XÁC NHẬN, MỞ RỘNG

<div style="display:flex;gap:10px;align-items:stretch;margin-top:24px">
<div style="flex:1;text-align:center;border:2px solid #187C80;border-radius:10px;padding:14px"><strong>01</strong><br><br>ĐO CƠ SỞ<br><small>Quy trình hiện tại</small></div>
<div style="flex:1;text-align:center;border:2px solid #187C80;border-radius:10px;padding:14px"><strong>02</strong><br><br>THỬ NGHIỆM<br><small>Phạm vi chọn lọc</small></div>
<div style="flex:1;text-align:center;border:2px solid #D8A45B;border-radius:10px;padding:14px"><strong>03</strong><br><br>ĐO VÀ XÁC NHẬN<br><small>Kết quả thực tế</small></div>
<div style="flex:1;text-align:center;border:2px solid #D8A45B;border-radius:10px;padding:14px"><strong>04</strong><br><br>MỞ RỘNG<br><small>Khi có bằng chứng</small></div>
</div>

---

<!-- _class: lead -->

# FVN REGISTER

## Giảm thao tác lặp lại. Tăng minh bạch. Cải tiến dựa trên dữ liệu.

<small>Hiệu quả định lượng sẽ được xác nhận qua đo lường trước–sau.</small>
