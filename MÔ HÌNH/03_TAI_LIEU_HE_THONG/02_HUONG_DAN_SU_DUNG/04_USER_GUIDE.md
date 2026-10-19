# HƯỚNG DẪN SỬ DỤNG HỆ THỐNG FVN REGISTER (USER GUIDE)

Tài liệu này hướng dẫn chi tiết các thao tác nghiệp vụ trên hệ thống **FCC Smart Portal (FVN Register)** dành cho toàn bộ các vai trò: Nhân viên (User), Cấp quản lý phê duyệt (Approver), Nhân sự/Tổng vụ (HR/GA Admin) và Quản trị hệ thống (SuperAdmin).

---

## MỤC LỤC
1. [Tổng quan Không gian làm việc & Lịch cá nhân](#1-tổng-quan-không-gian-làm-việc--lịch-cá-nhân)
2. [Hướng dẫn Nghiệp vụ Nhân sự (Nghỉ phép, OT, Công tác)](#2-hướng-dẫn-nghiệp-vụ-nhân-sự-nghỉ-phép-ot-công-tác)
3. [Hướng dẫn Phê duyệt & Xử lý Đối soát Chấm công](#3-hướng-dẫn-phê-duyệt--xử-lý-đối-soát-chấm-công)
4. [Quản lý Thiết bị & Import Dữ liệu Linh hoạt](#4-quản-lý-thiết-bị--import-dữ-liệu-linh-hoạt)
5. [Cổng Thông tin Nội bộ & Biểu mẫu Công khai](#5-cổng-thông-tin-nội-bộ--biểu-mẫu-công-khai)
6. [Quản trị Bảo mật, Endpoint & Phân quyền](#6-quản-trị-bảo-mật-endpoint--phân-quyền)
7. [Cấu hình Động cơ Lõi (Dành cho Admin/HR)](#7-cấu-hình-động-cơ-lõi-dành-cho-adminhr)
8. [Phụ lục A — Bộ ảnh chụp màn hình bổ sung](#phụ-lục-a-bộ-ảnh-chụp-màn-hình-bổ-sung)

---

## 1. TỔNG QUAN KHÔNG GIAN LÀM VIỆC & LỊCH CÁ NHÂN

### 1.1. Bảng điều khiển Trung tâm (Workspace Dashboard)
Ngay sau khi đăng nhập thành công, hệ thống đưa bạn đến màn hình **Workspace** trung tâm. Giao diện này tự động co giãn thông tin dựa trên quyền hạn (`Capability`) và phạm vi dữ liệu (`Data Scope`) của bạn.

![Giao diện Tổng quan sau khi đăng nhập](../../screenshots/state-1-depth-0-8f4e87f9.png)

**Các thao tác chính trên màn hình:**
* **Cụm nút Thao tác nhanh (Góc trên bên phải):** Bấm trực tiếp vào **`+ TẠO LEAVE`**, **`+ TẠO OT`**, hoặc **`+ TẠO CÔNG TÁC`** để mở nhanh biểu mẫu đăng ký mà không cần tìm kiếm trên thanh menu.
* **Tổng quan phòng ban (Dành cho Quản lý):** Theo dõi tỷ lệ đi làm thực tế (Real-time), số lượng nhân sự đang nghỉ phép, đi công tác hoặc làm thêm giờ trong ngày.
* **Thẻ chỉ số công việc:** Kiểm tra nhanh số lượng đơn đang chờ duyệt, số giờ OT lũy kế trong tháng, số ngày phép đã dùng và số thiết bị đang quản lý.
* **Nhiệm vụ của tôi (Action Items):** Khu vực cực kỳ quan trọng hiển thị các yêu cầu cần bạn giải trình hoặc xác nhận (Ví dụ: *Xác nhận đối soát OT*, *Xác nhận đối soát chấm công*). Các trạng thái được gắn nhãn màu rõ ràng:
  * 🔴 **Rejected / Cảnh báo đỏ:** Cần bổ sung bằng chứng hoặc đã bị HR từ chối.
  * 🟡 **Chờ duyệt / HR request:** Đang trong luồng xử lý hoặc HR yêu cầu giải trình thêm.
  * 🟢 **Complete:** Đã đối soát và chốt công thành công.

---

### 1.2. Lịch làm việc cá nhân (Work Calendar)
Menu truy cập: **Tổng quan** ➔ **Lịch làm việc** (`/calendar`).

Đây là nơi nhân viên tự làm chủ dữ liệu chấm công của mình. Hệ thống tự động đồng bộ dữ liệu quẹt thẻ (In/Out) từ máy chấm công HRM và đặt cạnh dữ liệu đơn từ đã đăng ký.

![Giao diện Lịch làm việc cá nhân](../../screenshots/state-2-depth-1--calendar-f69e2163.png)

**Hướng dẫn tra cứu và xử lý trên Lịch:**
1. **Nhận diện Cảnh báo thông minh (Anomaly Detection):** 
   * Chú ý hộp thông báo màu cam **"Cần xử lý trong tháng"** ở đầu trang và biểu tượng dấu hỏi màu cam **`?`** gắn trực tiếp trên ô ngày (Ví dụ: Ngày 01/10 phát hiện 61 phút OT thực tế nhưng đơn đang chờ duyệt hoặc chưa làm đơn).
2. **Bộ lọc sự kiện thông minh:** Bạn có thể bấm vào các tab lọc nhanh phía trên lịch để tách biệt dữ liệu:
   * **`Tất cả`**: Hiển thị toàn bộ ca làm việc, giờ vào/ra và đơn từ.
   * **`Công ty`**: Chỉ xem lịch nghỉ lễ, lịch làm việc chuẩn của nhà máy.
   * **`Chấm công`**: Chỉ xem dữ liệu giờ quẹt thẻ thực tế (`In - Out`) và số giờ công tính toán (`8h / 8h`).
   * **`Nghỉ phép` / `Làm thêm giờ` / `Công tác`**: Lọc riêng các ngày có phát sinh đơn nghiệp vụ tương ứng.
3. **Chuyển đổi chế độ xem:** Sử dụng nút **`Tháng`** (xem dạng lưới lịch truyền thống) hoặc **`Danh sách`** (xem dạng bảng liệt kê chi tiết theo dòng) ở góc phải lịch.

---

### 1.3. Trung tâm công việc (Work Inbox)
Menu truy cập: **Tổng quan** ➔ **Trung tâm công việc** (`/work/inbox`).

![Giao diện Trung tâm công việc](../../screenshots/state-3-depth-1--work-inbox-e9455449.png)

**Cách sử dụng:**
* Mọi thông báo cá nhân, yêu cầu giải trình đối soát công (`ATTENDANCE`, `OT`) và các đơn chờ bạn phê duyệt (`Approval Inbox`) được gom về một danh sách duy nhất.
* Bấm trực tiếp vào từng thẻ thông báo để đi thẳng tới màn hình xử lý chi tiết của vụ việc đó.

---

## 2. HƯỚNG DẪN NGHIỆP VỤ NHÂN SỰ (NGHỈ PHÉP, OT, CÔNG TÁC)

### 2.1. Quản lý Nghỉ phép (Leave Management)

#### A. Xem tổng quan và lịch sử nghỉ phép
Menu truy cập: **Nghỉ phép** ➔ **Tổng quan** (`/leave`) hoặc **Lịch sử nghỉ phép** (`/leave/history`).

![Tổng quan Nghỉ phép](../../screenshots/state-11-depth-1--leave-a42bd6b1.png)

#### B. Các bước tạo đơn xin nghỉ phép mới
Menu truy cập: **Nghỉ phép** ➔ **Tạo đơn mới** (`/leave/create`).

![Màn hình Đăng ký nghỉ phép](../../screenshots/state-13-depth-1--leave-create-44320abb.png)

1. Bấm nút **`+ ĐĂNG KÝ NGHỈ`** màu xanh để mở form nhập liệu.
2. Chọn **Hình thức nghỉ** (Phép năm, Nghỉ ốm, Nghỉ việc riêng có hưởng lương...).
3. Chọn **Từ ngày - Đến ngày** và ca nghỉ tương ứng.
4. Nhập lý do xin nghỉ chi tiết.
5. **Chọn người phê duyệt:** Hệ thống áp dụng cơ chế **Auto-Routing (Định tuyến tự động)** dựa trên chức vụ (`Position`) của bạn trong HRM để lọc ra đúng danh sách Quản lý có thẩm quyền duyệt, giúp bạn không bao giờ chọn nhầm sếp.
6. Bấm **Gửi đơn** để kích hoạt luồng phê duyệt.

---

### 2.2. Quản lý Làm thêm giờ (Overtime - OT)

#### A. Các bước tạo đơn đăng ký Tăng ca (OT)
Menu truy cập: **Làm thêm giờ (OT)** ➔ **Đăng ký OT** (`/ot/create`).


1. **Thông tin đơn:**
   * Chọn **Ngày OT**, **Giờ bắt đầu** và **Giờ kết thúc** dự kiến.
   * Chọn **Loại OT** (Tăng ca ngày thường, Tăng ca ngày nghỉ hàng tuần, hoặc Holiday OT).
   * Nhập **Lý do tổng quát** cho ca làm thêm.
2. **Kiểm soát Hạn mức (Quota):** Quan sát hộp **"Số dư giờ OT"** ở cột bên phải. Hệ thống tự động tính toán số giờ OT trong tuần, trong tháng (giới hạn 40h) và trong năm (giới hạn 200h/300h) để cảnh báo ngay nếu vượt trần luật lao động.
3. **Danh sách nhân viên tham gia:** 
   * Nếu bạn tự đăng ký cho mình, thông tin của bạn sẽ được ghi nhận.
   * Nếu bạn là Tổ trưởng/Quản lý đăng ký cho cả chuyền, bấm nút **`+ THÊM NHÂN VIÊN PHÒNG BAN`** để chọn hàng loạt nhân viên cùng tăng ca.
4. **Người phê duyệt:** Kiểm tra người duyệt ở mục Cấp phê duyệt (do hệ thống tự động định tuyến theo Policy) và bấm nút **`GỬI ĐƠN OT`**.

#### B. Tra cứu danh sách đơn OT toàn hệ thống
Menu truy cập: **Quản lý OT** ➔ **Danh sách đơn OT** (`/ot/list`).

![Danh sách đơn tăng ca](../../screenshots/state-39-depth-1--ot-list-b1fc7114.png)

* Sử dụng bộ lọc **Từ ngày - Đến ngày**, **Trạng thái** (`Pending`, `Approved`, `Rejected`) và ô tìm kiếm từ khóa để rà soát các đơn tăng ca đã phát sinh trong bộ phận.

---

### 2.3. Quản lý Công tác (Business Trip)

Menu truy cập: **Công tác** ➔ **Đăng ký công tác** (`/trip/create`).

![Màn hình Đăng ký công tác](../../screenshots/state-20-depth-1--trip-create-b0a46d6e.png)

**Các bước thực hiện:**
1. Nhập **Ngày bắt đầu** và **Ngày kết thúc** chuyến công tác.
2. Điền đầy đủ **Địa điểm công tác**, **Khách hàng / đối tác** làm việc và **Mục đích công tác**.
3. Khai báo **Phương tiện di chuyển** (Ô tô công ty, máy bay, tàu...), **Chi phí dự kiến (VNĐ)** và thông tin **Lưu trú** (Khách sạn).
4. Nếu đi theo đoàn, nhập mã các nhân viên đi cùng (cách nhau bằng dấu phẩy) vào ô **Nhân viên đi cùng**.
5. Chọn **Người phê duyệt** hợp lệ từ danh sách thả xuống.
6. Bạn có thể bấm **`LƯU NHÁP`** nếu chưa chốt lịch trình, hoặc bấm **`GỬI DUYỆT`** để trình ký ngay lập tức.

---

## 3. HƯỚNG DẪN PHÊ DUYỆT & XỬ LÝ ĐỐI SOÁT CHẤM CÔNG

### 3.1. Xử lý Danh sách chờ phê duyệt (Dành cho Quản lý / Approver)
Menu truy cập: **Phê duyệt** ➔ **Danh sách chờ duyệt** (`/approvals`).

![Màn hình Danh sách chờ phê duyệt](../../screenshots/state-10-depth-1--approvals-24f57bb6.png)

**Thao tác của Người phê duyệt:**
1. Toàn bộ đơn Nghỉ phép, OT, Công tác, Yêu cầu thiết bị, Phiếu bàn giao quyền của cấp dưới đều đổ về bảng tập trung này.
2. Bạn có thể lọc theo **Loại đơn** hoặc gạt nút **`Chờ tôi duyệt`** để chỉ hiện những đơn đang dừng ở cấp của bạn.
3. Kiểm tra thông tin, tiến trình duyệt và bấm nút **Phê duyệt (Approve)** hoặc **Từ chối (Reject)** (kèm lý do bắt buộc) ngay trên từng dòng.

---

### 3.2. Quy trình Đối soát Chấm công Tự động (Reconciliation Engine)
Hệ thống sở hữu động cơ đối soát tự động giữa **Kế hoạch (Đơn đã duyệt)** và **Thực tế (Dữ liệu quẹt thẻ HRM)**:
* **Trường hợp Khớp (`Matched`):** Thời gian quẹt thẻ khớp với đơn OT/Nghỉ phép ➔ Hệ thống tự động chuyển trạng thái **Complete** (Màu xanh), nhân viên và HR không cần can thiệp.
* **Trường hợp Lệch (`Mismatch`):** Ví dụ đăng ký OT 3 tiếng nhưng chỉ quẹt thẻ 2 tiếng, hoặc có quẹt thẻ nhưng không có đơn ➔ Hệ thống gắn cờ cảnh báo trên **Lịch làm việc** và đẩy nhiệm vụ vào mục **Nhiệm vụ của tôi** trên trang chủ.
  1. **Nhân viên:** Bấm vào nhiệm vụ để gửi phản hồi giải trình (`Confirmation`) và đính kèm bằng chứng (`Evidence`).
  2. **Nhân sự (HR):** Kiểm tra giải trình, bấm chấp thuận (`Approve`) hoặc yêu cầu bổ sung thêm (`NeedMoreEvidence`) trước khi chốt sổ tính lương.

---

## 4. QUẢN LÝ THIẾT BỊ & IMPORT DỮ LIỆU LINH HOẠT

Phân hệ Thiết bị cho phép các phòng ban (IT, GA, QA, Sản xuất) quản lý tài sản, gắn mã QR, theo dõi lịch sử bảo trì/sửa chữa với cấu trúc cột dữ liệu tùy biến động (Flexible Schema).

### 4.1. Sổ quản lý thiết bị toàn hệ thống
Menu truy cập: **Thiết bị** ➔ **Sổ quản lý thiết bị** (`/equipment`).


**Các thao tác nghiệp vụ:**
* Theo dõi toàn bộ danh sách máy móc, thiết bị, người đang giữ, phòng ban quản lý và trạng thái hoạt động.
* Thực hiện các giao dịch vòng đời tài sản: **Tạo yêu cầu bàn giao**, **Khai báo kiểm tra định kỳ (Checksheet qua QR)** và **Báo hỏng/Yêu cầu sửa chữa** thông qua luồng phê duyệt chuẩn.

---

### 4.2. Import Excel Thiết bị qua vùng đệm an toàn (Staging Workspace)
Menu truy cập: **Thiết bị** ➔ **Import Excel thiết bị** (`/equipment/workspace`).

Để tránh việc tải file Excel bị lỗi làm hỏng dữ liệu gốc, hệ thống cung cấp cơ chế kiểm duyệt qua vùng đệm (Staging):

![Màn hình Nhập dữ liệu thiết bị từ Excel](../../screenshots/state-24-depth-1--equipment-workspace-656cb5f6.png)

**Quy trình 4 bước Import chuẩn:**
1. **Chọn phòng ban & Mẫu dữ liệu:** Tại mục `1. Chọn phòng ban và mẫu dữ liệu`, chọn đúng bộ phận của bạn (Ví dụ: *13 - General Affairs*) và chọn phiên bản mẫu thiết bị tương ứng.
2. **Tải file mẫu:** Bấm nút **`TẢI MẪU DỮ LIỆU`** để lấy file Excel chuẩn có sẵn các cột đặc thù của phòng ban đó. Điền dữ liệu thiết bị vào file.
3. **Upload & Kiểm tra lỗi tự động (Staging):** Tải file Excel lên hệ thống. Hệ thống sẽ tạo ra một **Hồ sơ cha (Batch)** trong bảng lịch sử phía trên, tự động đếm **Tổng dòng**, phân loại số dòng **Hợp lệ** và số dòng **Lỗi**.
4. **Xử lý lỗi & Ghi nhận:** Bấm nút **`MỞ HỒ SƠ`** ở dòng tương ứng để xem chi tiết từng dòng bị lỗi định dạng. Sau khi dữ liệu đã hợp lệ 100%, thực hiện xác nhận để đẩy chính thức vào Sổ quản lý thiết bị.

---

## 5. CỔNG THÔNG TIN NỘI BỘ & BIỂU MẪU CÔNG KHAI

Giúp số hóa hoàn toàn bảng tin giấy và thay thế các công cụ khảo sát bên ngoài (như Google Forms), đảm bảo dữ liệu nằm an toàn trong máy chủ công ty.

### 5.1. Xem bản tin và Tham gia khảo sát (Dành cho Nhân viên)
Menu truy cập: **Công khai** ➔ **Thông tin công khai** (`/public-information`).

![Bảng thông tin công khai](../../screenshots/state-4-depth-1--public-information-4ea37cde.png)

* Nhân viên đọc các thông báo chính thức từ BGĐ và phòng GA/HR (có đính kèm văn bản đóng dấu mộc).
* Bấm sang mục **Biểu mẫu đăng ký** (`/public-forms`) để điền các phiếu khảo sát, đăng ký suất ăn, đăng ký xe đưa đón hoặc sự kiện công ty.

---

### 5.2. Quản trị Nội dung & Thiết kế Biểu mẫu động (Dành cho HR/GA Admin)

#### A. Đăng tải thông báo mới
Menu truy cập: **Công khai** ➔ **Quản lý nội dung công khai** (`/admin/public-information`).

![Màn hình Quản lý nội dung công khai](../../screenshots/state-6-depth-1--admin-public-information-5c84d052.png)

1. Chọn **Loại nội dung** (Thông báo, Tin tức, Nội dung HR...).
2. Nhập **Tiêu đề**, **Tóm tắt**, **Nội dung chi tiết** và thiết lập khoảng thời gian **Hiệu lực từ - đến**.
3. Gạt nút **Đánh dấu quan trọng** nếu muốn ghim thông báo lên đầu.
4. Đính kèm file PDF/Word/Ảnh văn bản gốc và bấm **`LƯU NHẬP`** hoặc xuất bản.

#### B. Tự thiết kế Biểu mẫu khảo sát / Đăng ký
Menu truy cập: **Công khai** ➔ **Quản lý biểu mẫu đăng ký** (`/admin/public-forms`).

![Màn hình Thiết kế Biểu mẫu đăng ký](../../screenshots/state-7-depth-1--admin-public-forms-b7bb1924.png)

1. Khai báo **Mã biểu mẫu**, **Tiêu đề**, **Thời gian mở/đóng form**.
2. Cấu hình tùy chọn: গạt nút **`Cho phép đăng ký nhiều lần`** hoặc **`Yêu cầu Approval`** (nếu đơn đăng ký cần sếp duyệt).
3. Chọn **Đối tượng đăng ký** (Áp dụng cho *Toàn công ty* hoặc giới hạn theo phòng ban/vị trí trích xuất trực tiếp từ HRM).
4. **Thiết kế câu hỏi động:** Bấm **`+ THÊM CÂU HỎI`**, đặt mã câu hỏi (`Q1`, `Q2`...), nhập nội dung câu hỏi, chọn kiểu dữ liệu đầu vào (`Text`, trắc nghiệm, số...) và đánh dấu **Bắt buộc**. Bấm **`LƯU NHẬP`** để hoàn tất.

#### C. Tổng hợp và Xuất kết quả khảo sát
Menu truy cập: **Công khai** ➔ **Tổng hợp đăng ký biểu mẫu** (`/admin/public-forms/submissions`).

![Màn hình Tổng hợp đăng ký biểu mẫu](../../screenshots/state-8-depth-1--admin-public-forms-submissions-43541eaf.png)

* Chọn biểu mẫu cần thống kê, lọc theo phòng ban/thời gian, bấm **`XEM`** để kiểm tra danh sách nhân viên đã nộp và bấm **`XUẤT EXCEL`** để lấy báo cáo.

---

## 6. QUẢN TRỊ BẢO MẬT, ENDPOINT & PHÂN QUYỀN

### 6.1. Phiếu thay đổi quyền / Bàn giao công việc (Access Handover)
Menu truy cập: **Thay đổi quyền** ➔ **Phiếu thay đổi quyền / bàn giao** (`/security/access-change`).

Khi có nhân sự nghỉ việc hoặc luân chuyển bộ phận, quyền hạn trên hệ thống không được cấp phát tùy tiện mà phải thông qua phiếu bàn giao có phê duyệt:

![Màn hình Phiếu thay đổi quyền và bàn giao](../../screenshots/state-9-depth-1--security-access-change-69558bb3.png)

**Quy trình thực hiện:**
1. **Nguyên tắc bảo mật cốt lõi:** Người tiếp nhận công việc phải là người trực tiếp đăng nhập và lập phiếu (Hệ thống sẽ từ chối nếu người lập không phải là người tiếp nhận).
2. Tại tab **`LẬP PHIẾU`**, chọn **Nghiệp vụ** cần nhận bàn giao (Nghỉ phép, OT, Thiết bị...) và chọn tên **Nhân sự cũ**.
3. Hệ thống tự động quét và liệt kê danh sách các quyền (`Function`/`Capability`) của nhân sự cũ trong mục **Quyền sẽ chuyển**.
4. Nhập **Lý do** bàn giao và bấm **`GỬI PHIẾU PHÊ DUYỆT`**. Sau khi các cấp quản lý duyệt xong, phiếu sẽ chuyển sang tab **`IT XỬ LÝ`** để kích hoạt quyền chính thức.

---

### 6.2. Quản trị Thiết bị đầu cuối (Endpoint Governance & Inventory)
Dành cho bộ phận IT kiểm soát an toàn thông tin và tính tuân thủ phần mềm trên các máy tính trạm trong nhà máy.

#### A. Thiết lập Danh mục Phần mềm/Dịch vụ chuẩn (Allowlist Catalog)
Menu truy cập: **Endpoint Governance** ➔ **Danh mục phần mềm / dịch vụ** (`/endpoint-governance`).

![Màn hình Endpoint Governance Catalog](../../screenshots/state-21-depth-1--endpoint-governance-6b3b51a9.png)

1. Khai báo các phần mềm hoặc dịch vụ được phép cài đặt (`Allowed`), nhà phát hành (`Publisher`) và ràng buộc phiên bản (`Version constraint`).
2. Tích chọn đầy đủ 6 mục trong **Checklist trước khi đưa catalog vào Common Approval** (đảm bảo đã rà soát nghiệp vụ và chuẩn hóa tên).
3. Lưu bản nháp (`Draft`) hoặc Import từ Excel, sau đó trình duyệt để ban hành tiêu chuẩn tuân thủ (`Compliance Policy`).

#### B. Giám sát Kiểm kê Endpoint (Inventory)
Menu truy cập: **Endpoint Governance** ➔ **Danh mục Endpoint** (`/endpoint-inventory`).

![Màn hình Danh mục Endpoint](../../screenshots/state-22-depth-1--endpoint-inventory-00116dfc.png)

* Theo dõi trạng thái kết nối (`Online`/`Offline`) của các máy tính có cài đặt Endpoint Agent, số lượng phần mềm đang chạy, tình trạng Antivirus và đối chiếu mức độ tuân thủ theo Catalog đã ban hành.

---

## 7. CẤU HÌNH ĐỘNG CƠ LÕI (DÀNH CHO ADMIN/HR)

Phần này hướng dẫn Quản trị viên cấu hình các quy tắc vận hành tự động của hệ thống tại cụm menu **Dữ liệu**.

### 7.1. Quản lý Dữ liệu gốc (Master Data) & Phân quyền tự động

* **Quản lý Bộ phận (`/admin/departments`) & Nhân viên (`/admin/employees`):** Tra cứu cơ cấu tổ chức và hồ sơ nhân sự được đồng bộ từ HRM.
  ![Quản lý Bộ phận](../../screenshots/state-25-depth-1--admin-departments-94af9429.png)
* **Quản lý Hình thức nghỉ (`/admin/leave-types`):** Định nghĩa các mã nghỉ phép (`AL`, `SL`, `UL`...), cấu hình loại nghỉ có trừ phép năm hay không và ánh xạ với mã chấm công HRM.
* **Mapping Role HRM ➔ F03User (`/admin/hrm-role-rules`):** Thiết lập luật gán quyền tự động. Ví dụ: Cấu hình hễ nhân sự thuộc phòng *57 - IT* có chức vụ *Sub-Leader* thì tự động gán quyền *Approver*; nhân sự phòng *13 - General Affairs* chức vụ *Manager* thì gán quyền *Admin*.
  ![Mapping Role HRM sang User](../../screenshots/state-32-depth-1--admin-hrm-role-rules-731fb3d4.png)

---

### 7.2. Cấu hình Ma trận & Chính sách Phê duyệt (Approval Engine)

#### A. Quản lý Người phê duyệt (`/admin/approvers`)

* Hệ thống tự động đọc cơ cấu HRM để đề xuất danh sách người duyệt mới hoặc thay đổi cấp duyệt. Admin chỉ cần bấm **`ÁP DỤNG HRM`** để cập nhật hàng loạt, hoặc thêm/sửa từng người duyệt theo từng phòng ban, từng loại đơn (`Leave`, `Overtime`, `Trip`, `Equipment`) và cấp duyệt (`Cấp 1`, `Cấp 2`, `Cấp 3`).

#### B. Thiết lập Chính sách Phê duyệt (`/admin/approval-policies`)

* Định nghĩa một đơn từ của một **Phòng ban** và **Chức vụ người yêu cầu** cụ thể sẽ phải đi qua mấy cấp duyệt, cấp nào là bắt buộc. 
* Hệ thống áp dụng nguyên tắc **Snapshot Invariant**: Khi nhân viên vừa bấm gửi đơn, lộ trình duyệt sẽ được đóng băng theo đúng Policy tại giây phút đó, việc thay đổi Policy sau này không làm gãy các đơn cũ đang chạy.

---

### 7.3. Cấu hình Chính sách Đối soát & Tính công HRM

#### A. Chính sách xử lý đối soát công (`/admin/execution-policies`)

* Thiết lập thời gian giới hạn (**SLA**) cho từng phân hệ (`ATTENDANCE`, `LEAVE`, `OT`, `TRIP`): Quy định nhân viên có bao nhiêu giờ để phản hồi giải trình (VD: `48 giờ`), HR có bao nhiêu giờ để xử lý, cho phép khiếu nại tối đa mấy vòng và cách thức chốt công khi đến ngày khóa sổ lương (`Payroll cutoff`).

#### B. Rule ký hiệu chấm công (`/admin/attendance-symbol-rules`) & Tính giờ HRM (`/hrm/attendance-calculation`)
![Rule ký hiệu chấm công](../../screenshots/state-35-depth-1--admin-attendance-symbol-rules-e1b4aa6b.png)

* **Rule ký hiệu chấm công:** Định nghĩa cách hệ thống phiên dịch từ `Loại ngày` + `Ca làm việc` + `Giờ In/Out` sang các ký hiệu hiển thị trên Lịch cá nhân (Ví dụ: `C1`, `HC`, `K(hours)`). Có sẵn công cụ **Test Rule** ở cuối trang để Admin nhập thử giờ vào/ra và bấm **`TÍNH THỬ`** kiểm tra kết quả ngay lập tức.

![Tính giờ theo HRM](../../screenshots/state-37-depth-1--hrm-attendance-calculation-8fe5444f.png)

* **Tính giờ theo HRM:** Chạy batch tính toán giờ công từ dữ liệu thô của HRM và lưu vào bảng đệm của FVN Register. Hệ thống tuân thủ tuyệt đối nguyên tắc **Read-Only Boundary**: Chỉ đọc dữ liệu từ HRM để đối soát, tuyệt đối không ghi đè ngược lại làm ảnh hưởng tới phần mềm tính lương hiện hữu.

---

### 7.4. Quản lý Tiến trình chạy ngầm (Background Jobs)
Menu truy cập: **Dữ liệu** ➔ **Lịch chạy background** (`/admin/background-jobs`).


**Hướng dẫn vận hành cho Admin:**
* Đây là bảng điều khiển toàn bộ các tác vụ tự động hóa của hệ thống: *Đồng bộ HRM*, *Gửi email trong hàng đợi*, *Leo thang phê duyệt (Escalation)*, *Sinh lịch kiểm tra thiết bị*, *Đối soát thực thi công*...
* **Thay đổi chu kỳ chạy:** Chọn nhanh các mốc thời gian (`5 phút`, `15 phút`, `30 phút`, `1 giờ`, `6 giờ`, `24 giờ`) hoặc nhập số phút tùy chỉnh rồi bấm **`LƯU`**. Thay đổi sẽ có hiệu lực ngay trong vòng 15 giây mà không cần khởi động lại máy chủ.
* **Kích hoạt khẩn cấp:** Bấm vào biểu tượng nút **Play (`▶`)** màu xanh ở cột bên phải của bất kỳ tác vụ nào nếu bạn muốn ép hệ thống chạy tác vụ đó ngay lập tức (Ví dụ: Cần đồng bộ gấp một nhân viên vừa mới vào công ty).

---

## PHỤ LỤC A. BỘ ẢNH CHỤP MÀN HÌNH BỔ SUNG

Thư mục `MÔ HÌNH/screenshots` có **69 ảnh** theo manifest: **60 ảnh ở trạng thái sẵn sàng** và **9 ảnh chưa sẵn sàng**. Phần nội dung chính phía trên đã dùng 20 ảnh; phụ lục này bổ sung các ảnh sẵn sàng còn lại để tổng tài liệu tham chiếu đủ 60 ảnh. Một số ảnh ghi lại các tab hoặc trạng thái thao tác khác nhau của cùng một màn hình, nên được giữ riêng thay vì coi là ảnh trùng.

### Lịch làm việc và trung tâm công việc

**Ảnh 56 — Năm & ngày nghỉ**  
![Năm & ngày nghỉ](../../screenshots/state-56-depth-1--admin-work-calendar-ee29b723.png)  
Đường dẫn: `/admin/work-calendar` · Tệp: `state-56-depth-1--admin-work-calendar-ee29b723.png`

**Ảnh 58 — Open**  
![Open](../../screenshots/state-58-depth-2--calendar-1a165859.png)  
Đường dẫn: `/calendar` · Tệp: `state-58-depth-2--calendar-1a165859.png`

**Ảnh 59 — Open**  
![Open](../../screenshots/state-59-depth-2--calendar-f995a9d4.png)  
Đường dẫn: `/calendar` · Tệp: `state-59-depth-2--calendar-f995a9d4.png`


### Nghiệp vụ, chấm công và báo cáo

**Ảnh 12 — Lịch sử nghỉ phép**  
![Lịch sử nghỉ phép](../../screenshots/state-12-depth-1--leave-history-1cac4286.png)  
Đường dẫn: `/leave/history` · Tệp: `state-12-depth-1--leave-history-1cac4286.png`

**Ảnh 14 — Tổng quan**  
![Tổng quan](../../screenshots/state-14-depth-1--ot-003940c0.png)  
Đường dẫn: `/ot` · Tệp: `state-14-depth-1--ot-003940c0.png`

**Ảnh 15 — Lịch sử OT**  
![Lịch sử OT](../../screenshots/state-15-depth-1--ot-history-d33a6413.png)  
Đường dẫn: `/ot/history` · Tệp: `state-15-depth-1--ot-history-d33a6413.png`

**Ảnh 17 — Yêu cầu OT từ nhân viên**  
![Yêu cầu OT từ nhân viên](../../screenshots/state-17-depth-1--ot-supervisor-requests-4ae53c61.png)  
Đường dẫn: `/ot/supervisor-requests` · Tệp: `state-17-depth-1--ot-supervisor-requests-4ae53c61.png`

**Ảnh 18 — Tổng quan**  
![Tổng quan](../../screenshots/state-18-depth-1--trip-fcd38afb.png)  
Đường dẫn: `/trip` · Tệp: `state-18-depth-1--trip-fcd38afb.png`

**Ảnh 19 — Lịch sử công tác**  
![Lịch sử công tác](../../screenshots/state-19-depth-1--trip-history-8a0cb941.png)  
Đường dẫn: `/trip/history` · Tệp: `state-19-depth-1--trip-history-8a0cb941.png`

**Ảnh 36 — Trạng thái phòng ban**  
![Trạng thái phòng ban](../../screenshots/state-36-depth-1--leave-department-status-d9fb7004.png)  
Đường dẫn: `/leave/department-status` · Tệp: `state-36-depth-1--leave-department-status-d9fb7004.png`

**Ảnh 40 — Hạn mức OT**  
![Hạn mức OT](../../screenshots/state-40-depth-1--admin-ot-limit-rules-8f27179d.png)  
Đường dẫn: `/admin/ot-limit-rules` · Tệp: `state-40-depth-1--admin-ot-limit-rules-8f27179d.png`

**Ảnh 41 — Bảng công tính lương**  
![Bảng công tính lương](../../screenshots/state-41-depth-1--payroll-table-3ebfa058.png)  
Đường dẫn: `/payroll/table` · Tệp: `state-41-depth-1--payroll-table-3ebfa058.png`

**Ảnh 42 — Trung tâm báo cáo**  
![Trung tâm báo cáo](../../screenshots/state-42-depth-1--reports-2484c266.png)  
Đường dẫn: `/reports` · Tệp: `state-42-depth-1--reports-2484c266.png`

**Ảnh 43 — Nghỉ phép**  
![Nghỉ phép](../../screenshots/state-43-depth-1--report-leave-23bbe355.png)  
Đường dẫn: `/report/leave` · Tệp: `state-43-depth-1--report-leave-23bbe355.png`

**Ảnh 44 — OT**  
![OT](../../screenshots/state-44-depth-1--report-ot-b6ce71f3.png)  
Đường dẫn: `/report/ot` · Tệp: `state-44-depth-1--report-ot-b6ce71f3.png`

**Ảnh 45 — Công tác**  
![Công tác](../../screenshots/state-45-depth-1--report-operations-5d00a7c9.png)  
Đường dẫn: `/report/operations` · Tệp: `state-45-depth-1--report-operations-5d00a7c9.png`

**Ảnh 46 — Thiết bị**  
![Thiết bị](../../screenshots/state-46-depth-1--report-operations-4e430460.png)  
Đường dẫn: `/report/operations` · Tệp: `state-46-depth-1--report-operations-4e430460.png`

**Ảnh 47 — Chấm công**  
![Chấm công](../../screenshots/state-47-depth-1--report-operations-1f62d69e.png)  
Đường dẫn: `/report/operations` · Tệp: `state-47-depth-1--report-operations-1f62d69e.png`


### Thông tin công khai và biểu mẫu

**Ảnh 5 — Biểu mẫu đăng ký**  
![Biểu mẫu đăng ký](../../screenshots/state-5-depth-1--public-forms-02074056.png)  
Đường dẫn: `/public-forms` · Tệp: `state-5-depth-1--public-forms-02074056.png`

**Ảnh 57 — THÔNG TIN**  
![THÔNG TIN](../../screenshots/state-57-depth-2--thong-tin-cong-khai-9bdcbe3e.png)  
Đường dẫn: `/thong-tin-cong-khai` · Tệp: `state-57-depth-2--thong-tin-cong-khai-9bdcbe3e.png`

**Ảnh 60 — Open**  
![Open](../../screenshots/state-60-depth-2--admin-public-information-19432fd5.png)  
Đường dẫn: `/admin/public-information` · Tệp: `state-60-depth-2--admin-public-information-19432fd5.png`

**Ảnh 61 — Open**  
![Open](../../screenshots/state-61-depth-2--admin-public-information-33af45bd.png)  
Đường dẫn: `/admin/public-information` · Tệp: `state-61-depth-2--admin-public-information-33af45bd.png`

**Ảnh 62 — ARCHIVE**  
![ARCHIVE](../../screenshots/state-62-depth-2--admin-public-information-f9f859e0.png)  
Đường dẫn: `/admin/public-information` · Tệp: `state-62-depth-2--admin-public-information-f9f859e0.png`

**Ảnh 63 — Open**  
![Open](../../screenshots/state-63-depth-2--admin-public-forms-a7bf0c6f.png)  
Đường dẫn: `/admin/public-forms` · Tệp: `state-63-depth-2--admin-public-forms-a7bf0c6f.png`

**Ảnh 64 — Open**  
![Open](../../screenshots/state-64-depth-2--admin-public-forms-submissions-6a5496b7.png)  
Đường dẫn: `/admin/public-forms/submissions` · Tệp: `state-64-depth-2--admin-public-forms-submissions-6a5496b7.png`


### Phê duyệt và bảo mật

**Ảnh 49 — Security Center**  
![Security Center](../../screenshots/state-49-depth-1--admin-security-4bd4f3a4.png)  
Đường dẫn: `/admin/security` · Tệp: `state-49-depth-1--admin-security-4bd4f3a4.png`

**Ảnh 51 — Đăng ký chức năng hệ thống**  
![Đăng ký chức năng hệ thống](../../screenshots/state-51-depth-1--admin-security-function-registry-9dac5b62.png)  
Đường dẫn: `/admin/security/function-registry` · Tệp: `state-51-depth-1--admin-security-function-registry-9dac5b62.png`

**Ảnh 65 — LẬP PHIẾU**  
![LẬP PHIẾU](../../screenshots/state-65-depth-2--security-access-change-f723bc77.png)  
Đường dẫn: `/security/access-change` · Tệp: `state-65-depth-2--security-access-change-f723bc77.png`

**Ảnh 66 — PHIẾU CỦA TÔI**  
![PHIẾU CỦA TÔI](../../screenshots/state-66-depth-2--security-access-change-4fc829a1.png)  
Đường dẫn: `/security/access-change` · Tệp: `state-66-depth-2--security-access-change-4fc829a1.png`

**Ảnh 67 — CHỜ TÔI DUYỆT**  
![CHỜ TÔI DUYỆT](../../screenshots/state-67-depth-2--security-access-change-709debda.png)  
Đường dẫn: `/security/access-change` · Tệp: `state-67-depth-2--security-access-change-709debda.png`

**Ảnh 68 — IT XỬ LÝ**  
![IT XỬ LÝ](../../screenshots/state-68-depth-2--security-access-change-52a5dacb.png)  
Đường dẫn: `/security/access-change` · Tệp: `state-68-depth-2--security-access-change-52a5dacb.png`

**Ảnh 69 — LÀM MỚI**  
![LÀM MỚI](../../screenshots/state-69-depth-2--approvals-d9cb0bc0.png)  
Đường dẫn: `/approvals` · Tệp: `state-69-depth-2--approvals-d9cb0bc0.png`


### Quản trị và cấu hình hệ thống

**Ảnh 26 — Nhân viên**  
![Nhân viên](../../screenshots/state-26-depth-1--admin-employees-8b7744d7.png)  
Đường dẫn: `/admin/employees` · Tệp: `state-26-depth-1--admin-employees-8b7744d7.png`

**Ảnh 31 — Conflict HRM**  
![Conflict HRM](../../screenshots/state-31-depth-1--admin-hrm-sync-review-e146b0c0.png)  
Đường dẫn: `/admin/hrm-sync-review` · Tệp: `state-31-depth-1--admin-hrm-sync-review-e146b0c0.png`

**Ảnh 48 — Quản trị viên**  
![Quản trị viên](../../screenshots/state-48-depth-1--admin-managers-3c076b93.png)  
Đường dẫn: `/admin/managers` · Tệp: `state-48-depth-1--admin-managers-3c076b93.png`

**Ảnh 50 — Thiết bị & phiên đăng nhập**  
![Thiết bị & phiên đăng nhập](../../screenshots/state-50-depth-1--admin-sessions-0ff96757.png)  
Đường dẫn: `/admin/sessions` · Tệp: `state-50-depth-1--admin-sessions-0ff96757.png`

**Ảnh 52 — Email mẫu**  
![Email mẫu](../../screenshots/state-52-depth-1--admin-email-templates-46ef5414.png)  
Đường dẫn: `/admin/email-templates` · Tệp: `state-52-depth-1--admin-email-templates-46ef5414.png`

**Ảnh 53 — Cấu hình SMTP**  
![Cấu hình SMTP](../../screenshots/state-53-depth-1--admin-email-profiles-b30b6e68.png)  
Đường dẫn: `/admin/email-profiles` · Tệp: `state-53-depth-1--admin-email-profiles-b30b6e68.png`

**Ảnh 54 — Quản trị ngôn ngữ**  
![Quản trị ngôn ngữ](../../screenshots/state-54-depth-1--admin-localization-abe59762.png)  
Đường dẫn: `/admin/localization` · Tệp: `state-54-depth-1--admin-localization-abe59762.png`

**Ảnh 55 — Email chờ gửi**  
![Email chờ gửi](../../screenshots/state-55-depth-1--admin-email-queue-1824bb90.png)  
Đường dẫn: `/admin/email-queue` · Tệp: `state-55-depth-1--admin-email-queue-1824bb90.png`


### Ảnh chưa đưa vào hướng dẫn

Manifest hiện đánh dấu 9 ảnh là `ready=false`; các ảnh này không được nhúng như ảnh hướng dẫn chính thức để tránh minh họa trạng thái chưa xác nhận. Khi chụp lại hoặc xác nhận được trạng thái, có thể bổ sung sau.
