# FVN REGISTER — QUICK GUIDE

## 1. 60 giây đầu tiên

**Login → Dashboard → xem Action/Notification → mở Calendar → chọn module.**

## 2. Tạo request

**Create → Validate/Preview → Submit → theo dõi status.**

## 3. Phê duyệt

**Action/Approval → Detail → kiểm tra → Approve hoặc Reject + Reason.**

`Escalated` = chuyển cấp do timeout, **không phải Rejected**.

## 4. Calendar

Một ngày có thể có: ca làm việc, attendance, Leave/OT/Trip, company holiday/workday, request status/approval status, Issue `?` và Action, Registration Opportunity.

**Ngày tương lai chưa có attendance = bình thường.**

Click ngày để chọn `Detail / Confirmation / Registration / Info`.

### Ký hiệu chấm công

- Ca `C1/C2/C3/HC/...` lấy từ HRM `F03HrmAttendanceCalculated`.
- Không tính lại ca từ In/Out ở Calendar.
- Rule Calendar quyết định symbol Work/OT.
- T7 nghỉ công ty → `T...`.
- CN → `CN...`.
- Lễ quốc gia → `NL...`.
- T7 đi làm bình thường → classification/màu ngày thường, không tự đổi thành `T`.
- OT dùng block 15 phút theo Rule; `C1 05:49→14:05` không tạo `K0.25`; `C1 05:47→18:05` cho OT chuẩn 4 giờ → `K4`.

Kíp 12h được split theo Rule, ví dụ `T8 + TK4`, `CN8 + CNK4`, `NL8 + NLK4` thay vì gộp thành một số giờ tổng.

`?` là reconciliation state, không phải symbol. Ví dụ `C3 + K4 + ?` gồm ca từ HRM, OT symbol từ Rule và mismatch do Execution Reconciliation.

## 5. Trạng thái cần nhớ

| Trạng thái | Ý nghĩa |
|---|---|
| Draft | Chưa gửi |
| Pending | Đang chờ workflow |
| Approved | Business request đã được approval |
| Rejected | Bị từ chối |
| Escalated | Timeout → chuyển cấp; không phải Rejected |
| Mismatch | Planned và Actual khác nhau |
| Resolved | Reconciliation đã được giải quyết |
| Action Open | Có việc cần xử lý |
| Action Completed | Work item đã đóng; không tự đồng nghĩa Approved |

## 6. 8 nguyên tắc

1. Notification ≠ Approval.
2. Action ≠ Business Result.
3. Calendar ≠ Source of Truth.
4. Approved ≠ Actual.
5. Mismatch ≠ Rejected.
6. HR correction ≠ Direct DB Edit.
7. View ≠ Export.
8. Capability ≠ Data Scope.

## 7. Khi có dấu `?`

Mở marker → đọc Issue → chọn ActionOption. Không tự đổi dữ liệu để làm mất mismatch.

## 8. Quản trị Calendar Symbol Rules

Rule được cấu hình trên UI; không sửa code khi thay đổi ký hiệu/block.

**Test Rule:** nhập DayType + Shift/Pattern + In/Out → kiểm tra Work/OT segment, block, rounding và symbol trước khi Active.

Quyền sửa Rule phải qua capability RBAC và Feature Operator Assignment nếu feature đã được cấu hình operator. Có quyền xem Calendar không đồng nghĩa có quyền sửa Rule.

## 9. Execution Reconciliation — luồng sau HR Resolution

HR Resolution không nhất thiết đóng case ngay. Nếu policy yêu cầu employee decision:

**HR Resolution → AwaitingEmployeeDecision → Accept / Appeal**

- Accept → Resolved.
- Appeal → AppealReviewing → HR Resolution tiếp theo.
- Đạt MaxAppealRounds → FinalDecisionPending.
- Timeout → Escalate hoặc Auto-accept theo policy snapshot.

ActionId luôn trỏ action hiện tại; Calendar và Action Center dùng cùng action chain. Nhiều HR có thể xử lý cùng function nhưng claim/lock bảo đảm một case chỉ có một người mutate tại một thời điểm.