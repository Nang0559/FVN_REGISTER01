# Hướng dẫn xử lý phản hồi công / đối soát chấm công

## 1. Dành cho nhân viên

Khi hệ thống phát hiện chênh lệch giữa đăng ký thực hiện và chấm công, nhân viên xử lý từ Lịch của tôi, Trung tâm công việc hoặc thông báo được liên kết với vụ việc.

### Bước 1 — Gửi phản hồi

Mở vụ việc, kiểm tra ngày làm việc, dữ liệu đăng ký, dữ liệu chấm công và gửi phản hồi/bằng chứng.

### Bước 2 — Chờ HR xử lý

Khi HR đang xử lý, nhân viên không cần gửi lại cùng một phản hồi trừ khi được yêu cầu bổ sung bằng chứng.

### Bước 3 — Nhận kết quả HR

Sau khi HR đưa ra kết quả, vụ việc có thể xuất hiện ở trạng thái Chờ nhân viên xác nhận.

Nhân viên có hai lựa chọn:
- Chấp nhận: kết thúc vụ việc theo kết quả HR.
- Khiếu nại: mở một vòng xử lý lại theo chính sách đã lưu của vụ việc.

Nếu chính sách yêu cầu bằng chứng mới khi khiếu nại, phải bổ sung bằng chứng phát sinh sau kết quả HR gần nhất.

### Bước 4 — Khiếu nại có giới hạn

Số vòng khiếu nại không phải là không giới hạn. Hệ thống kiểm tra chính sách đã được lưu cùng vụ việc.

Khi đạt số vòng tối đa, vụ việc chuyển sang Chờ quyết định cuối và không tạo thêm vòng khiếu nại thông thường.

## 2. Dành cho HR / người xử lý được chỉ định

### Nhận việc

Người dùng phải có capability/function phù hợp để xử lý execution review.

Nhiều HR có thể được cấp quyền xử lý cùng loại việc, nhưng một vụ việc chỉ có một người được claim/khóa xử lý tại một thời điểm.

### Claim vụ việc

Khi bắt đầu xử lý:
1. Mở vụ việc.
2. Claim/nhận xử lý.
3. Kiểm tra bằng chứng và lịch sử.
4. Thực hiện resolution.

Người khác vẫn có thể xem vụ việc nhưng không thể thay đổi dữ liệu khi đang bị người khác giữ claim.

### Release

Khi không tiếp tục xử lý, HR có thể release claim để người xử lý khác tiếp tục.

Claim hết hạn cũng có thể được mở lại theo cơ chế khóa của hệ thống.

## 3. Sau khi HR đưa ra resolution

Resolution thông thường chưa đồng nghĩa với việc đóng vĩnh viễn vụ việc.

Nếu chính sách yêu cầu nhân viên xác nhận:
- hệ thống tạo action cho nhân viên;
- ActionId của reconciliation trỏ tới action hiện tại;
- HR action đã hoàn thành vẫn được giữ trong lịch sử.

Nếu nhân viên khiếu nại, hệ thống tạo HR appeal action mới.

## 4. Hết hạn phản hồi của nhân viên

Hệ thống xử lý timeout theo policy snapshot của vụ việc:
- Escalate: chuyển sang quyết định cuối.
- Auto-accept: ghi nhận chấp nhận kết quả HR theo chính sách.

Không được hiểu timeout là một vòng khiếu nại mới.

## 5. Trạng thái trên Lịch của tôi

| Hiển thị | Ý nghĩa |
|---|---|
| Cần xử lý | Có action hiện tại cần người dùng xử lý |
| Chờ nhân viên xác nhận | HR đã có kết quả, nhân viên cần chấp nhận/khiếu nại |
| Đang xem xét khiếu nại | HR đang xử lý vòng khiếu nại |
| Chờ quyết định cuối | Cần bước quyết định cuối theo policy |
| Đã giải quyết | Quy trình đã hoàn tất |

## 6. Chính sách xử lý

HR/Admin có quyền cấu hình policy tại Quản trị → Chính sách đối soát thực hiện nếu được cấp function Execution.PolicyManage.

Các nhóm cấu hình chính:
- SLA nhân viên;
- cách xử lý timeout;
- SLA HR;
- cho phép khiếu nại;
- số vòng khiếu nại tối đa;
- yêu cầu bằng chứng mới;
- yêu cầu quyết định cuối;
- vị trí HRM của cấp quyết định cuối;
- các quy tắc liên quan payroll/correction.

### Lưu ý về phiên bản policy

Policy mới chỉ áp dụng theo thời gian hiệu lực. Một vụ việc đã tạo sẽ giữ policy snapshot của nó, vì vậy việc sửa policy sau đó không làm thay đổi ngầm quy tắc của vụ việc đang tồn tại.

## 7. Payroll

Vụ việc đối soát chưa được giải quyết tiếp tục được coi là chưa sẵn sàng cho payroll theo cơ chế hiện tại.

Cấu hình PayrollCutoffMode hiện là thông tin policy; không nên hiểu rằng hệ thống tự động bỏ qua hoặc tự động đóng mọi vụ việc tại thời điểm khóa payroll.

## 8. Lịch sử

Mỗi vụ việc có lịch sử chuyển trạng thái, người thao tác, lý do và các vòng xử lý. Khi cần kiểm tra, mở chi tiết vụ việc và xem timeline/history thay vì chỉ dựa vào trạng thái hiện tại.
