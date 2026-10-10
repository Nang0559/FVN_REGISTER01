# FVN REGISTER — KIỂM SOÁT MỨC ĐỘ SẴN SÀNG VÀ TUYÊN BỐ CHỨC NĂNG

> Mục tiêu: bảo đảm tài liệu giới thiệu đúng với khả năng đã được xác minh; không biến thiết kế, mã nguồn hoặc lợi ích kỳ vọng thành cam kết vận hành.

## 1. Nguyên tắc công bố

Mỗi nội dung giới thiệu phải thuộc một trong ba nhóm:

| Nhóm | Khi nào được dùng | Cách diễn đạt |
|---|---|---|
| **Đã xác nhận vận hành** | Đã build, kiểm thử theo luồng, xác nhận quyền/dữ liệu và có bằng chứng UAT trên đúng phiên bản | “Đã vận hành”, kèm phạm vi và ngày xác nhận |
| **Có triển khai, cần xác minh** | Có mô tả, code hoặc giao diện nhưng chưa đủ bằng chứng đầu-cuối | “Có hỗ trợ”, “đang xác minh”, nêu điều kiện/cấu hình |
| **Mục tiêu / lợi ích kỳ vọng** | Là mục tiêu cải tiến, chưa có đo lường sau áp dụng | “Hướng tới”, “có thể giúp”, “sẽ đo bằng…” |

Không dùng từ “tự động hoàn toàn”, “đã sẵn sàng toàn bộ”, “đã tiết kiệm X giờ/X đồng” hoặc “không còn sai sót” khi chưa có bằng chứng tương ứng.

## 2. Những giới hạn phải giữ trong mọi tài liệu

- FVN REGISTER kết nối quy trình; không mặc nhiên thay thế HRM hay hệ thống tính lương chính thức.
- Đăng ký đã được phê duyệt là kế hoạch; không đồng nghĩa với kết quả thực tế đã hoàn thành.
- Lịch làm việc là góc nhìn tổng hợp; dữ liệu gốc vẫn thuộc module/nguồn dữ liệu nghiệp vụ tương ứng.
- Dashboard, báo cáo, dữ liệu và thao tác được giới hạn theo quyền và phạm vi đã cấp.
- QR, camera, nhập Excel, schema, checklist, bằng chứng và lịch sử thiết bị chỉ được giới thiệu là đã sẵn sàng khi kiểm tra thành công trên môi trường trình diễn/nghiệm thu.
- Lợi ích thời gian, chi phí và ROI chỉ được công bố sau khi có đường cơ sở, kết quả sau áp dụng và chi phí được xác nhận.

## 3. Bằng chứng cần có theo nhóm nghiệp vụ

| Nhóm | Kiểm tra tối thiểu trước khi nói “đã sẵn sàng” |
|---|---|
| Đăng ký và phê duyệt | Tạo/sửa/gửi yêu cầu; chọn đúng người duyệt; duyệt/từ chối; trạng thái và lịch sử nhất quán |
| Nghỉ phép / OT / công tác | Kịch bản hợp lệ và không hợp lệ; quy tắc nghiệp vụ; quyền; hủy/sửa; lịch sử |
| Chấm công / đối soát / HR Review | Nguồn dữ liệu thực tế; so sánh kế hoạch-thực tế; xử lý sai lệch; bằng chứng; lịch sử quyết định |
| Thiết bị | Luồng yêu cầu/phê duyệt; QR; camera nếu trình diễn; nhập Excel; schema phòng ban; checklist; sửa chữa; lịch sử |
| Kiểm kê/tuân thủ máy tính đầu cuối | Đăng ký kết nối của tác nhân; thông tin xác thực an toàn; dữ liệu kiểm kê không bị ghi trùng; chính sách/danh sách cho phép có phê duyệt; cảnh báo tuân thủ; thu hồi/hết hạn; nhật ký kiểm tra; thử nghiệm trên máy thật |
| Trung tâm công việc / thông báo | Việc được giao đúng người; thông báo; trạng thái sau xử lý; tránh thông báo trùng |
| Dashboard / báo cáo / xuất dữ liệu | Số liệu đúng; bộ lọc; phân quyền; phạm vi dữ liệu; quyền xuất; dữ liệu rỗng được thể hiện đúng |
| An toàn truy cập | API kiểm tra quyền phía máy chủ; dữ liệu ngoài phạm vi không thể truy cập trực tiếp |
| Dữ liệu phục vụ tính lương | Định nghĩa snapshot, điều kiện sẵn sàng và đối chiếu với nguồn chính thức; không tuyên bố thay thế payroll |

## 4. Cổng xác nhận trước buổi trình chiếu

- [ ] Xác nhận đúng repository, branch, commit và môi trường sẽ demo.
- [ ] Chạy restore/build/test theo phạm vi phát hành hiện hành và lưu kết quả.
- [ ] Xác nhận cơ sở dữ liệu, script triển khai/kiểm tra và dữ liệu mẫu.
- [ ] Thử đầu-cuối ít nhất một quy trình đăng ký → phê duyệt → lịch sử.
- [ ] Thử tình huống đối soát kế hoạch/thực tế nếu trình bày về đối soát.
- [ ] Kiểm tra quyền của nhân viên, người phê duyệt, HR, quản lý và quản trị viên.
- [ ] Kiểm tra dashboard, báo cáo, bộ lọc và xuất dữ liệu nếu trình diễn.
- [ ] Kiểm tra QR/camera/nhập Excel/checklist thiết bị nếu trình diễn.
- [ ] Dùng tài khoản và dữ liệu mẫu an toàn; chuẩn bị ảnh dự phòng nếu môi trường demo lỗi.
- [ ] Người phụ trách nghiệp vụ ký xác nhận phạm vi đã kiểm thử.
- [ ] Tách số liệu đã đo khỏi lợi ích định tính hoặc giả thuyết.

## 5. Lưu ý từ việc rà soát repository

Tại thời điểm rà soát tài liệu giới thiệu, tài liệu **MÔ HÌNH/23_PRODUCT_READINESS_PLAN.md** ghi Phase 0 là **IN PROGRESS** và các phase tiếp theo là **TODO**. Tuy nhiên, phần thông tin đầu tài liệu này đang tham chiếu repository **Nang0559/Reggister** và branch **feature/work-calendar-action-implementation**, không trùng repository/branch đang rà soát là **Nang0559/FVN_REGISTER01** / **feature/equipment-change-approval-workflow**.

Vì vậy:

1. Không dùng trạng thái trong file readiness đó làm chứng nhận phát hành cho branch hiện tại.
2. Cần cập nhật hoặc lập biên bản readiness mới theo đúng commit của branch hiện tại.
3. Chỉ đổi một mục sang “đã xác nhận vận hành” sau khi có bằng chứng build/test/UAT phù hợp.
4. Bản trình chiếu hiện mô tả **giá trị và quy trình mục tiêu**; không phải biên bản xác nhận tất cả module đã sẵn sàng production.

Tài liệu **docs/ENDPOINT_GOVERNANCE_STATUS.md** cũng ghi branch **feature/i18n-vi-ja**, không phải branch đang rà soát; nó chỉ là ghi chú kỹ thuật tham khảo, không phải chứng nhận pilot cho branch hiện tại.

Trong thư mục **.github/workflows** của branch đang rà soát, workflow hiện có là kiểm tra đa ngôn ngữ; không nên diễn giải nó thành bằng chứng rằng toàn bộ solution build, UAT và luồng nghiệp vụ đã PASS.

## 6. Phiếu xác nhận trước khi phát hành tài liệu

| Trường | Giá trị cần điền |
|---|---|
| Repository / branch | Nang0559/FVN_REGISTER01 / feature/equipment-change-approval-workflow |
| Commit được kiểm tra | Ghi SHA đầy đủ |
| Môi trường kiểm tra | Development / UAT / Demo / Production |
| Các module đã kiểm tra | Ghi rõ phạm vi |
| Build / test | PASS / FAIL / Chưa chạy, kèm liên kết hoặc log |
| UAT nghiệp vụ | PASS / FAIL / Chưa chạy, kèm người xác nhận |
| Hạn chế còn lại | Ghi rõ chức năng hoặc điều kiện chưa được xác nhận |
| Người xác nhận nghiệp vụ | Họ tên/chức danh theo quy định nội bộ |
| Ngày xác nhận | YYYY-MM-DD |

**Quy tắc cuối:** khi bằng chứng còn thiếu, hãy mô tả trung thực là “hướng tới” hoặc “cần xác minh”. Một tài liệu PR đáng tin cậy phải giúp ban giám đốc hiểu cả giá trị kỳ vọng lẫn điều kiện để chứng minh giá trị đó.
