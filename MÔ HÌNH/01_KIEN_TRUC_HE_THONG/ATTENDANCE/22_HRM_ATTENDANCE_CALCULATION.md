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

Catch-up chạy **từng ngày một** (mỗi ngày một dòng `CalculationRun` company-wide riêng, theo thứ tự tăng dần, dừng ở ngày lỗi đầu tiên) nên nếu API bị restart thì các ngày đã `Succeeded` được giữ lại và lần sau chỉ tính phần còn lại. Mọi lần tính company-wide (catch-up và 00:30) giữ applock session `FVN_REGISTER:ATTENDANCE:COMPANY-RUN`; nếu đã có lần khác đang chạy thì bỏ qua. Khi lấy được khoá, worker đóng các dòng `Running` company-wide của worker đã quá `BackgroundWorkers:StaleMinutes` (đánh dấu `Failed`). Session SQL mồ côi từ process cũ xem/kill bằng `SQL/22_06_AttendanceCalculationSessions.sql`.

Lịch (Calendar) không chờ backfill đồng bộ: `IAttendanceBackfillCoordinator` chờ tối đa `Calendar:AttendanceBackfill:WaitSeconds` (mặc định 3s) rồi trả dữ liệu hiện có kèm cảnh báo "đang cập nhật"; backfill của nhân viên đó tiếp tục chạy nền (timeout `BackgroundTimeoutSeconds`, mặc định 300s), tối đa một backfill cho mỗi nhân viên.

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

## 19. Ranh giới Attendance và Calendar Symbol Rule

Phần xác định **ca và kết quả chấm công** không thuộc Calendar Symbol Rule Engine.

`F03HrmAttendanceCalculated` là nguồn chính thức cho:
- `ShiftId` / `ShiftAbbr`;
- `CheckInTime` / `CheckOutTime`;
- `RequiredMinutes`;
- Work minutes;
- OT minutes và OT recognized minutes do HRM calculation cung cấp.

Calendar Symbol Rule Engine chỉ diễn giải kết quả đó thành ký hiệu hiển thị. Không được tính lại ca bằng cách lấy `CheckOut - CheckIn - RequiredHours` và không được tạo các ký hiệu giả như `K0.25` chỉ vì chênh vài phút so với giờ ca.

Luồng chuẩn:

```text
HRM
  ↓
dbo.usp_CalculateHrmAttendance
  ↓
F03HrmAttendanceCalculated
  ├── Shift / Ca ─────────────── giữ nguyên
  ├── In / Out ───────────────── giữ nguyên
  ├── OT minutes ─────────────── input cho Rule
  └── Required minutes ───────── input cho Rule
              ↓
      Calendar Symbol Rules
              ↓
       Work / OT symbols
```

## 20. Nguyên tắc tính ký hiệu OT

Ký hiệu OT phải được tính theo **khoảng OT được cấu hình trong Rule**, không tính bằng tổng số giờ vượt `RequiredMinutes`.

Block mặc định của nghiệp vụ hiện tại là 15 phút. Rule phải xác định cách lượng hóa phần thời gian nằm trong OT window; phần thời gian nhỏ hơn một block không tự sinh ra ký hiệu fractional OT nếu Rule không cho phép.

Ví dụ: C1 có Work window 06:00–14:00 và OT window 14:00–18:00. Attendance `05:49 → 14:05` vẫn là C1 và phần OT chỉ có 5 phút; với block 15 phút/FLOOR thì OT được tính là 0, không phải `K0.25`.

`05:47 → 18:05` của C1 có OT nằm trong OT window là 4 giờ; phần 5 phút sau đó không làm tăng block, nên ký hiệu OT là `K4`.

## 21. Day Type và prefix ký hiệu

Day Type phải lấy từ Company Calendar/holiday classification, không suy ra chỉ từ thứ trong tuần.

- Ngày thường → prefix Work theo Rule ngày thường.
- Thứ 7 **nghỉ công ty** → `T`.
- Chủ nhật → `CN`.
- Ngày lễ quốc gia → `NL`.
- Thứ 7 đi làm bình thường, không được khai báo là ngày nghỉ công ty → vẫn là ngày thường về màu và classification, không tự đổi sang `T`.

`F03CompanyHolidays` là source để phân biệt ngày nghỉ công ty và ngày lễ quốc gia theo `HolidayType`; không hard-code danh sách ngày lễ trong Calendar UI.

## 22. Split rule cho ca/kíp đặc biệt

Rule phải hỗ trợ nhiều segment để biểu diễn các ca/kíp có cả Work và OT, thay vì gộp thành một giá trị tổng.

Ví dụ chuẩn hiện tại:

| Day Type | Kíp | Kết quả |
|---|---|---|
| T7 nghỉ công ty | 06:00–18:00 | `T8` + `TK4` |
| T7 nghỉ công ty | 10:00–22:00 | `T28` + `T4` |
| T7 nghỉ công ty | 18:00–06:00 | `T38` + `TK4` |
| CN | 06:00–18:00 | `CN8` + `CNK4` |
| CN | 10:00–22:00 | `CNC28` + `CN4` |
| CN | 18:00–06:00 | `CNC38` + `CNK4` |
| Lễ quốc gia | 06:00–18:00 | `NL8` + `NLK4` |
| Lễ quốc gia | 10:00–22:00 | `NLC28` + `NL4` |
| Lễ quốc gia | 18:00–06:00 | `NLC38` + `NLK4` |

Các giá trị lẻ được lượng hóa theo block 15 phút; Rule có thể tách phần Work và OT theo segment. Ví dụ 11h45 của kíp 06:00–18:00 phải trở thành `T7.75 + TK4`, không phải `T11.75`.

## 23. Dấu `?` là reconciliation state, không phải symbol

`?` không được lưu chung với ký hiệu `C1/K4/T8/...`.

- Symbol Rule quyết định Work/OT symbol.
- Execution Reconciliation quyết định có mismatch/action hay không.
- Khi Actual OT > 0 nhưng không có Approved OT tương ứng, reconciliation có thể tạo `?`.
- Khi Approved OT xuất hiện hoặc Actual OT biến mất sau recalculation, reconciliation có thể auto-resolve theo policy.

Do đó một ngày có thể có:

```text
C3
K4
?
```

mà ba thành phần này có nguồn và lifecycle độc lập.

## 24. UI quản trị Rule

Rule phải được quản lý bằng dữ liệu cấu hình, không yêu cầu sửa C# khi thay đổi nghiệp vụ.

Mỗi Rule tối thiểu phải định nghĩa:

- RuleCode / RuleName;
- DayType;
- Shift/Pattern;
- Priority và hiệu lực;
- Segment Work/OT;
- Start/End time;
- Symbol type/prefix/template;
- Value mode (duration/fixed);
- Block minutes;
- Rounding mode;
- Split/sequence;
- IsActive.

Màn hình quản trị phải có **Test Rule** để nhập ngày, DayType, ca, In/Out và xem kết quả trước khi kích hoạt. Test phải hiển thị cả input HRM, OT window, block, phần thời gian được nhận và symbol sinh ra.

Không để `WorkCalendar.razor` hoặc `workCalendar.js` chứa business formula cho symbol.

## 25. Phân quyền Rule

Quản trị Rule không tạo cơ chế authorization riêng. Endpoint/UI phải áp dụng đồng thời:

`RBAC capability + Organization/ManagedScope (nếu áp dụng) + Feature Operator Assignment (nếu feature đã cấu hình operator) + Business State.`

Rule management là configuration capability; người có quyền xem Calendar không mặc nhiên được sửa Rule.

## 26. Nguyên tắc cuối của Attendance/Calendar

> **HRM quyết định ca và dữ liệu chấm công; Calendar Rule quyết định cách biểu diễn Work/OT; Execution Reconciliation quyết định dấu `?`; Company Calendar quyết định DayType.**

Khi thay đổi cách hiển thị ký hiệu, ưu tiên sửa Rule trên UI/DB và test Rule, không sửa công thức HRM và không tạo calculation engine thứ hai.