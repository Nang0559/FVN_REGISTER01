# FVN REGISTER — DOCUMENTATION STANDARD

> Quy định cách tổ chức, viết và duy trì toàn bộ tài liệu FVN REGISTER để tài liệu thiết kế, vận hành và trợ giúp luôn cùng một mô hình.

## 1. Source of truth

| Nội dung | Source of truth | Tài liệu phụ |
|---|---|---|
| Kiến trúc runtime + Work Calendar/Action | Tài liệu kiến trúc hiện hành trong `MÔ HÌNH/01_KIEN_TRUC_HE_THONG/` | User Guide / FAQ |
| Execution Reconciliation | Tài liệu nghiệp vụ hiện hành trong `MÔ HÌNH/02_NGHIEP_VU_MODULE/` | User Guide / FAQ |
| Approval route + snapshot | Tài liệu Approval + implementation hiện hành | User Guide / FAQ |
| Security / RBAC / data scope | Tài liệu Security + implementation hiện hành | User Guide / FAQ |
| Product readiness | `MÔ HÌNH/23_PRODUCT_READINESS_PLAN.md` | Release / launch |
| User-facing documentation | `MÔ HÌNH/03_TAI_LIEU_HE_THONG/` | README links |

Không tạo architecture document song song để mô tả lại cùng một rule.

## 2. Documentation layers

```mermaid
flowchart TB
    A[Architecture Source of Truth] --> B[Domain / Contract Design]
    B --> C[Implementation / Readiness]
    C --> D[User Guide / Help]
    D --> E[Quick Guide / FAQ]
    D --> F[Presentation / Launch]
```

- **Architecture:** hệ thống phải hoạt động thế nào.
- **Domain design:** rule của từng nghiệp vụ.
- **Implementation/readiness:** trạng thái đã kiểm chứng; không biến kế hoạch thành fact.
- **User Guide / Help:** người dùng phải làm gì và thấy gì.
- **Quick Guide/FAQ:** tra cứu nhanh; không định nghĩa rule mới.
- **Presentation:** diễn giải giá trị và flow; không chứa contract kỹ thuật độc lập.

## 3. Quy tắc không mâu thuẫn

1. Một thuật ngữ chỉ có một nghĩa chuẩn.
2. Một business rule chỉ có một nơi định nghĩa chính.
3. Guide không được mô tả behavior chưa có evidence runtime hoặc implementation rõ ràng.
4. Presentation có thể đơn giản hóa nhưng không được đảo nghĩa business state.
5. FAQ chỉ giải thích behavior đã được định nghĩa; nếu rule đổi, FAQ phải đổi cùng change set.
6. Tài liệu lịch sử phải ghi rõ **Historical / Reference**.
7. Không dùng “đã hoàn thành”, “đã certify” hoặc “production ready” nếu readiness evidence chưa có.

## 4. Thuật ngữ chuẩn

| Thuật ngữ | Nghĩa chuẩn |
|---|---|
| Request | Bản ghi nghiệp vụ do người dùng tạo |
| Planned | Kế hoạch nghiệp vụ đã có hiệu lực |
| Actual | Thực tế từ nguồn execution/attendance chính thức |
| Approval | Quyết định phê duyệt theo workflow |
| Approval Snapshot | Snapshot bất biến của hierarchy/approver tại Submit |
| Reconciliation | Đối chiếu Planned và Actual |
| Confirmation | Xác nhận nguyên nhân/kết quả mismatch |
| Evidence | Chứng cứ gắn với confirmation, có review lifecycle riêng |
| HR Resolution | Quyết định xử lý case execution của HR |
| Action | Work item cần người dùng xử lý; không phải business result |
| Notification | Kênh delivery/read; không phải approval |
| Calendar | Projection/navigation theo ngày; không phải business source of truth |
| Dashboard | Aggregation theo capability và data scope |
| Data Scope | Phạm vi dữ liệu server cho phép truy cập |
| Capability | Quyền thực hiện một chức năng/hành động |
| Registration Opportunity | Cơ hội mở form đăng ký cho ngày/module đủ điều kiện |

## 5. Work Calendar wording chuẩn

Calendar phải được mô tả theo thứ tự:

**Company calendar → Shift → Attendance → Registration → Issues/Actions → Registration Opportunity**.

Registration và Confirmation là hai loại interaction khác nhau:

- **Registration:** tạo/sửa request nghiệp vụ.
- **Confirmation:** xử lý mismatch/action đã tồn tại.
- **Detail:** mở dữ liệu nghiệp vụ hiện có.
- **Info:** chỉ xem thông tin.

Ngày tương lai chưa có Actual attendance là trạng thái bình thường; Calendar không tạo mismatch chỉ vì ngày chưa tới.

## 6. Approval wording chuẩn

- **Pending:** đang chờ workflow xử lý.
- **Approved:** request đã hoàn tất điều kiện approval theo business workflow.
- **Rejected:** request bị từ chối.
- **Escalated:** hệ thống chuyển cấp do timeout; không đồng nghĩa Rejected.
- Sau Submit, Approval Snapshot là cơ sở xử lý request; thay đổi cấu hình approver về sau không sửa lịch sử request đã Submit.

## 7. Reconciliation wording chuẩn

```text
Approved Planned + Actual
        ↓
   Reconciliation
    ↙          ↘
 Matched      Mismatch
    ↓             ↓
 Resolved     Confirmation
                   ↓
              Evidence/Review
                   ↓
              HR Resolution
```

Approved không đồng nghĩa Actual. Action Completed không đồng nghĩa business Approved/Matched. Evidence Reject/NeedMoreEvidence không tự động đóng reconciliation. HR correction không bypass calculation pipeline.

## 8. Security wording chuẩn

Luôn phân biệt:

**Capability = được làm gì?**  
**Data Scope = được làm trên dữ liệu nào?**

UI filter không cấp quyền. Server phải áp dụng scope cho query và export.

## 9. Quy trình cập nhật tài liệu

```mermaid
flowchart LR
    CHANGE[Architecture / Rule Change] --> SOURCE[Update Source of Truth]
    SOURCE --> CODE[Align Implementation]
    CODE --> GUIDE[Update User Guide / Help]
    GUIDE --> FAQ[Update Quick Guide / FAQ]
    FAQ --> PR[Update Presentation / README]
    PR --> VERIFY[Cross-document review]
```

## 10. Checklist review

- [ ] Tài liệu chỉ tham chiếu current contract.
- [ ] Không còn tên bảng/controller/service legacy trong phần current behavior.
- [ ] Status terminology thống nhất.
- [ ] Calendar không bị mô tả như source of truth.
- [ ] Action không bị mô tả như business result.
- [ ] Approval Snapshot được mô tả nhất quán.
- [ ] Capability và Data Scope được phân biệt.
- [ ] Future date không bị coi là missing attendance.
- [ ] Registration không bị trộn với Confirmation/Action.
- [ ] Guide/FAQ không claim behavior chưa được chứng minh.
- [ ] Link giữa các tài liệu còn hợp lệ.

## 11. Versioning

Tài liệu current ghi branch/revision khi có thay đổi lớn. Không dùng số phiên bản để tạo một architecture source song song; lịch sử thay đổi ghi trong changelog của source-of-truth.
