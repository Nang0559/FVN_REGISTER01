# 22 — HRM-COMPATIBLE ATTENDANCE CALCULATION

## 1. Mục tiêu

FVN_REGISTER cho phép người dùng chọn **Bộ phận + Từ ngày + Đến ngày** và bấm **Tính giờ** mà không cần mở giao diện HRM.

Kết quả phải bám theo luật/chains thực tế của HRM và được lưu độc lập trong database FVN_REGISTER.

## 2. Nguyên tắc nguồn dữ liệu

HRM là nguồn dữ liệu và luật tính chấm công. FVN chỉ READ các bảng nguồn HRM.

FVN không mở HRM UI, không ghi HRM.dbo.tblBaoCao hoặc HRM.dbo.tblBaoCaoK, không dùng trigger để đồng bộ và không tạo một bộ công thức Work/OT độc lập với HRM.

## 3. SQL Server boundary

HRM và FVN_REGISTER cùng SQL Server nên không cần linked server.

```text
FVN_REGISTER
   │ READ
   └──────> HRM.dbo.*
   │
   └ WRITE -> FVN_REGISTER.dbo.F03Hrm*
```

Ba-part name HRM.dbo.<table> là boundary chính thức giữa hai database.

## 4. Runtime chain

```text
FVN Web
   ↓ POST /api/hrm-attendance-calculation/calculate
HrmAttendanceCalculationController
   ↓
IHrmAttendanceCalculationService
   ↓
HrmAttendanceCalculationService
   ↓
dbo.usp_CalculateHrmAttendance
   ↓
HRM source data + HRM-compatible calculation rules
   ↓
FVN local calculation context (#tblBaoCao)
   ↓
F03HrmAttendanceCalculated
   ↓
F03HrmOTActual
   ↓
F03OTEmployees.ActualHours
```

## 5. HRM calculation compatibility

Phần tính Work/OT phải bám theo chain thực tế của HRM, đặc biệt logic của sphrmvn_TimeKeepingForStaff_K và sphrmvn_FindShift_New / FindShiftOfStaff.

FVN chỉ thay persistence/context boundary:

```text
HRM: calculation → tblBaoCao / tblBaoCaoK
FVN: calculation-compatible logic → #tblBaoCao → F03HrmAttendanceCalculated → F03HrmOTActual
```

Không thay đổi business formula chỉ vì FVN có schema khác.

## 6. FVN result tables

F03HrmAttendanceCalculated là snapshot kết quả của một lần tính: CalculationBatchId, WorkDate, HrmEmployeeId, EmployeeCode, DeptCode, shift, CheckIn/CheckOut, Work minutes, OT minutes, Late/Early, Required minutes và các trường HRM leave/holiday.

F03HrmOTActual là projection OT thực tế phục vụ request OT: thời gian vào/ra, tổng phút, OT ngày/đêm, OT được ghi nhận, source attendance row và CalculationBatchId.

F03OTEmployees.ActualHours được cập nhật từ calculation result trong cùng SQL orchestration.

## 7. Calculation batch

Mỗi lần người dùng bấm Tính giờ tạo một CalculationBatchId. Batch dùng để truy vết, phân biệt các lần chạy, export đúng snapshot và đối chiếu với HRM.

Summary trả về: CalculationBatchId, DeptCode, FromDate, ToDate, EmployeeCount, CalculatedRows, StartedAt, FinishedAt, CalculationVersion.

## 8. API

```http
POST /api/hrm-attendance-calculation/calculate
GET  /api/hrm-attendance-calculation/export/attendance/{batchId}
GET  /api/hrm-attendance-calculation/export/ot/{batchId}
```

Request gồm DeptCode, FromDate và ToDate.

## 9. Application / Infrastructure responsibility

Application định nghĩa contract IHrmAttendanceCalculationService và các DTO request/result; không biết bảng HRM cụ thể.

Infrastructure HrmAttendanceCalculationService validate date range, gọi dbo.usp_CalculateHrmAttendance, dùng timeout dài cho batch, trả summary, log lỗi kỹ thuật và khôi phục timeout mặc định.

API yêu cầu authentication, nhận department/date range, truyền EmployeeCode người chạy vào TriggeredBy và không tự tính Work/OT.

## 10. Background calculation

HrmAttendanceCalculationWorker dùng cùng IHrmAttendanceCalculationService. Manual và background phải đi qua cùng dbo.usp_CalculateHrmAttendance.

Worker không mặc định tính lại toàn bộ kỳ 21→20 sau mỗi restart. Worker đọc các CalculationRun thành công có scope company-wide và chỉ catch-up phần còn thiếu tới ngày hôm qua; mỗi ngày tiếp tục tính ngày hôm qua.

## 11. Calendar historical coverage

Lịch cá nhân dùng coverage-aware lazy backfill. Khi người dùng chuyển sang một tháng, hệ thống kiểm tra coverage của đúng nhân viên và khoảng ngày yêu cầu. Nếu có gap, hệ thống chỉ tính employee + khoảng gap; không kích hoạt calculation toàn công ty.

```text
GET /api/calendar/me?from=YYYY-MM-01&to=YYYY-MM-dd
        ↓
EnsureEmployeeRangeAsync(employee, range)
        ↓
CalculationRun coverage?
   ├── Có → đọc kết quả
   └── Thiếu → tính đúng employee + đúng khoảng thiếu
        ↓
F03HrmAttendanceCalculated
        ↓
Calendar provider
```

Coverage không được xác định bằng COUNT(*) đơn thuần. Hệ thống ghép các CalculationRun thành công để tìm khoảng còn thiếu. Calendar chỉ backfill tới hôm nay, không tính ngày tương lai.

## 12. History / payroll lock

`F03HrmAttendanceCalculated` là current-state mutable store. Khi kỳ payroll đã `Locked` hoặc `Exported`, calendar không được recalculation/reopen kỳ đó.

`F03HrmAttendanceHistory` là nguồn đọc cho dữ liệu đã archive. AttendanceCalendarModuleProvider đọc current result trước và fallback sang history khi current không có row cho ngày đó.

```text
Open/current period
    → current attendance + lazy backfill nếu thiếu

Locked/Exported/archived period
    → F03HrmAttendanceHistory
    → không recalculation
```

## 13. Concurrency

`dbo.usp_CalculateHrmAttendance` dùng `sp_getapplock` theo ngày chấm công. Background calculation và calendar lazy backfill của cùng ngày không được phép cùng ghi current attendance; calendar có thể chờ ngắn khi background đang xử lý ngày đó.

`F03HrmAttendanceCalculationRun.EmployeeCode` ghi nhận scope employee-specific để calendar phân biệt coverage chính xác.

## 14. Export

Excel đọc snapshot F03HrmAttendanceCalculated và projection F03HrmOTActual theo CalculationBatchId, không export trực tiếp từ HRM.dbo.tblBaoCao. Attendance export lấy từ F03HrmAttendanceCalculated; OT export lấy F03HrmOTActual và join về snapshot tương ứng để lấy metadata/display value.

## 15. Kiểm thử đối chiếu

Baseline thực tế đã xác nhận:

```text
Employee: FCC1331
HrmEmployeeId: 207
Date: 2026-09-18
BCMaCa = 1
BCTGDen = 07:50:01
BCTGVe = 20:02:25
BCTGLamNgay = 480
BCTGQuaGioNgay = 197
BCTGThemNgay = 197
BCTGLamToi = 0
BCTGQuaGioToi = 0
BCTGThemToi = 0
BCTGDiMuonNgay = 0
BCTGVeSomNgay = 0
BCTGQuyDinh = 480
BCTinhLamThem = 1
```

FVN phải đối chiếu các trường tương ứng trong F03HrmAttendanceCalculated.

## 16. Không quay lại kiến trúc cũ

Không tạo lại công thức Work/OT C# riêng, pipeline reconciliation thay thế calculation engine, attendance staging làm nguồn tính chính, trigger trên HRM result table, ghi ngược vào HRM hoặc phụ thuộc HRM UI.

## 17. SQL deployment

```text
22_00_HrmAttendanceTables.sql
22_01_InterSectionTime3.sql
22_02_HrmCompatibleTimeKeepingForStaff.sql
22_03_CalculateHrmAttendance.sql
```

00_Deploy_All.sql là entry point deploy.

## 18. Nguyên tắc cuối

> **HRM sở hữu luật và dữ liệu chấm công. FVN sở hữu lần chạy, snapshot kết quả và giao diện sử dụng.**

FVN phải chạy độc lập về UI nhưng không được tạo một HRM thứ hai về business formula.