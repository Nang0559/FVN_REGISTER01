# HR Case Assistant – thiết kế triển khai và vận hành

> Phạm vi: luồng xử lý phản hồi có bằng chứng của nhân viên (OT, chấm công, nghỉ phép), lấy dữ liệu HR có phân quyền, đối chiếu, tạo bản nháp và chờ HR duyệt. Đây là trợ lý nghiệp vụ có hồ sơ và audit, không phải chatbot hỏi đáp chung chung.

## 1. Mục tiêu và nguyên tắc

Nhân viên có thể gửi phản hồi như: “Tôi đăng ký 2 giờ làm thêm ngày 01/10 nhưng bảng công chỉ ghi nhận 1,5 giờ. Nhờ HR kiểm tra.”

Hệ thống cần xác định bằng chứng có đúng nhân viên/ngày không, phiếu đăng ký ghi bao nhiêu, actual đã xác nhận bao nhiêu, bảng công ghi nhận bao nhiêu, sai lệch nằm ở bước nào và dữ liệu nào còn thiếu.

Nguyên tắc bắt buộc:

1. FVN_REGISTER là nguồn dữ liệu nghiệp vụ và nguồn phân quyền chính thức.
2. AI API không kết nối trực tiếp hoặc ghi vào database vận hành FVN_REGISTER.
3. Mọi truy vấn facts đi qua AIHRCaseController của FVN API; controller kiểm tra quyền và managed scope trước khi gọi IReportDispatcher.
4. Quy tắc so sánh được thực hiện bằng code xác định. LLM chỉ diễn đạt kết quả và soạn bản nháp.
5. Không có dữ liệu thì báo thiếu bằng chứng; không tự hiểu null là 0.
6. Không tự sửa chấm công/OT/nghỉ phép, không tự phê duyệt nghiệp vụ và không gửi câu trả lời cho nhân viên.
7. HR duyệt hoặc chỉnh sửa bản nháp; mọi quyết định được lưu lịch sử.

## 2. Kiến trúc tổng thể

~~~mermaid
flowchart TD
    E["Nhân viên / HR"] --> UI["Blazor: /ai/hr-assistant"]
    UI --> API["FVN_REGISTER API - JWT"]
    API --> AUTH["IAuthorizationService + ManagedScope"]
    AUTH --> REPORT["IReportDispatcher"]
    REPORT --> DB[("FVN_REGISTER DB / Report Views")]
    API -->|facts đã kiểm tra quyền| AI["FVN_REGISTER.AI.Api"]
    UI -->|upload evidence| AI
    AI --> STORE["Private original document store"]
    AI --> Q[("FVN_AI SQL queue + metadata")]
    Q --> W["AI Worker"]
    W --> AV["ClamAV scan"]
    AV --> EX["PDF text / XLSX / CSV / TXT extraction"]
    EX --> CAND["Extraction candidate + page/line evidence"]
    CAND --> CASE["HR Reconciliation Service"]
    CASE --> RULES["Deterministic comparison rules"]
    RULES --> LLM["Ollama: draft explanation/reply only"]
    LLM --> CASEDB[("ai.AiHRCases")]
    CASEDB --> HR["HR Case Inbox / Detail"]
    HR --> REVIEW["HR approve / edit / reject"]
    REVIEW --> AUDIT[("ai.AiHRCaseReviews + rowversion")]
~~~

| Thành phần | Trách nhiệm |
|---|---|
| FVN_REGISTER API | JWT, danh tính, quyền chức năng, employee managed scope |
| IReportDispatcher | Lấy dữ liệu OT, attendance, leave đã có trong FVN_REGISTER |
| FVN_REGISTER.AI.Api | Ingestion, case analysis, case inbox/review API; chỉ nhận signed context từ gateway |
| AI Worker | Scan, trích xuất tài liệu, tạo candidate |
| FVN_AI SQL | Document metadata, queue, extraction, case và review audit |
| Ollama | Soạn câu trả lời từ facts và kết quả so sánh đã xác định |
| Blazor HR workspace | Xem bằng chứng, so sánh, chỉnh sửa và duyệt câu trả lời |

## 3. Nguồn dữ liệu nghiệp vụ và cách đối chiếu

FVN API không nhận số liệu chính thức từ người dùng. Nó tự truy vấn report service sau khi gọi CanAccessAsync(user, functionCode, employeeCode, null).

### 3.1. Làm thêm giờ (OT)

Các report được gọi cho đúng nhân viên và ngày:

| Report | Dữ liệu lấy | Nguồn report service |
|---|---|---|
| OTDetail | Phiếu OT, mã phiếu, trạng thái, tổng giờ đăng ký | VF03OTRequest |
| OTSummaryByEmployee | Giờ kế hoạch, giờ thực tế, số dòng có actual | VF03OTRequestDetail |
| AttendanceDetail | Giờ OT tính từ dữ liệu chấm công, ca, ngày lễ | F03HrmAttendanceCalculated |

Các so sánh:

- OT-PLAN-001: số giờ trên phản hồi/bằng chứng so với giờ đăng ký.
- OT-ACTUAL-001: số giờ phản hồi so với actual đã ghi nhận.
- OT-ATTENDANCE-USER-001: số giờ phản hồi so với OT tính từ chấm công.
- OT-ATTENDANCE-001: actual đã xác nhận so với OT chấm công.
- OT-STATUS-001: trạng thái phiếu cần HR diễn giải theo quy trình phê duyệt.

ActualRecordCount được thêm vào report OT theo nhân viên để phân biệt actual chưa có với actual thực sự bằng 0. Không dùng tổng 0 do SUM(ActualHours ?? 0) để kết luận khi chưa có dòng actual.

### 3.2. Chấm công

Dùng ReportType.AttendanceDetail, lấy dữ liệu qua report service từ F03HrmAttendanceCalculated: ngày làm việc, mã nhân viên, ca, check-in/check-out, tổng giờ, OT giờ được công nhận và ngày lễ.

Các giá trị thời gian trong bằng chứng chỉ được tự điền khi trích xuất đúng hai mốc giờ duy nhất và hợp lệ. Nếu có nhiều mốc giờ, hệ thống không đoán.

### 3.3. Nghỉ phép

Dùng ReportType.LeaveDetail, lấy đơn từ VF03LeaveRequest: mã đơn, mã nhân viên, StartDate, EndDate, TotalDay, LeaveTypeCode và RequestStatus.

Nếu không tìm thấy đơn bao phủ ngày được chọn, hệ thống vẫn tạo facts với trạng thái “No leave request found” để HR xem xét; không biến tình huống không có đơn thành lỗi kỹ thuật.

Lưu ý: TotalDay hiện là tổng ngày của đơn. Với đơn nhiều ngày, đây là số ngày của toàn đơn chứ không phải phần ngày phân bổ riêng cho một ngày. HR cần xem khoảng ngày và quy tắc tính nửa ngày/ca trước khi kết luận. Nếu cần đối soát theo từng ngày, cần bổ sung nguồn detail theo ngày và ánh xạ DayValue theo ngày làm việc.

### 3.4. Quyền truy cập

- Đọc OT: SecurityFunctionCodes.OTView (2101).
- Đọc chấm công: AttendanceView (2901).
- Đọc nghỉ phép: LeaveView (2001).
- Duyệt case OT: OTReconcile (2106).
- Duyệt case attendance: AttendanceFeedback (2912).
- Duyệt case leave: LeaveApprove (2005).

FVN API kiểm tra quyền trên đúng EmployeeCode trước khi trả case detail hoặc chuyển review command. Hàng chờ AI API được lấy trong private gateway, sau đó FVN API lọc từng hồ sơ theo managed scope.

## 4. Ingestion và đọc bằng chứng

### 4.1. Định dạng hiện hỗ trợ

| Định dạng | Xử lý |
|---|---|
| PDF có text layer | PdfPig trích text và giữ page marker |
| XLSX | Đọc OpenXML shared strings và worksheet cells |
| CSV | Đọc UTF-8 |
| TXT | Đọc UTF-8 |

Scanned/image-only PDF hiện chưa có OCR. DOC/DOCX chưa nằm trong allowlist. Không giả vờ OCR thành công nếu PDF không có text layer.

### 4.2. Quy trình bảo mật file

1. FVN API nhận file từ người dùng đăng nhập và chuyển file tới AI API.
2. AI API lưu file trong private storage, tính SHA-256 và tạo queue job.
3. Worker lấy lease, chạy ClamAV INSTREAM.
4. Chỉ verdict Clean mới được chuyển sang extractor.
5. Kết quả được lưu thành AiExtractionRecord ở trạng thái AwaitingHumanReview.
6. HR case chỉ được phân tích khi file thuộc đúng tenant, do đúng actor tải lên, đã scan và đã có extraction candidate.

Nếu scanner chưa cấu hình hoặc không kết nối được, verdict là Unknown, file không được trích xuất. Đây là fail-closed có chủ ý.

### 4.3. Candidate fields

Extractor nhận diện có giới hạn: employeeCode, dateCandidates, timeCandidates, hourCandidates, dayCandidates, leaveTypeCandidates và evidence[] gồm lineNumber, pageNumber, text.

Các trường chỉ được tự động đưa vào so sánh khi có một giá trị duy nhất, hợp lệ. Khi nhiều giá trị khác nhau, AI không chọn ngẫu nhiên. EvidenceCandidateJson được lưu cùng hồ sơ để HR kiểm tra nội dung trích xuất; PDF giữ số trang, các dòng giữ số dòng candidate.

### 4.4. Phát hiện sai nhân viên/ngày

Nếu bằng chứng có đúng một mã nhân viên hoặc ngày làm việc, hệ thống so với employee/date của facts chính thức:

- EVIDENCE-EMPLOYEE-001
- EVIDENCE-WORKDATE-001

Khác nhau sẽ tạo Discrepancy và hiển thị trong HR workspace. Candidate không bao giờ thay thế mã nhân viên do FVN gateway đã xác thực.

## 5. Phân tích và soạn câu trả lời

HRReconciliationService thực hiện:

1. Kiểm tra input và provenance của official facts.
2. Kiểm tra evidence ownership/status/extraction.
3. Ánh xạ các candidate đơn trị vào reported values.
4. Chạy rule comparisons theo loại case.
5. Tổng hợp outcome: Matched, Discrepancy, InsufficientEvidence, NeedsHumanDecision.
6. Tạo internal note từ từng comparison/rule code.
7. Gọi IAIModelProvider để diễn đạt câu trả lời tiếng Việt, chỉ sử dụng JSON facts/comparisons được cung cấp.
8. Lưu case với trạng thái PendingHRReview.

LLM không tính lại giờ công, không thay đổi outcome, không đưa ra quy định hoặc cam kết không có trong facts. Nếu Ollama lỗi hoặc chưa cấu hình, hệ thống dùng câu trả lời fallback xác định và vẫn yêu cầu HR duyệt.

### Cấu hình Ollama

Mặc định DefaultProvider=Unconfigured. Khi đã cài Ollama trên máy chủ AI riêng, cấu hình bằng environment variables hoặc secret store, không commit secret:

~~~text
AIPlatform__DefaultProvider=Ollama
AIPlatform__Endpoint=http://127.0.0.1:11434
AIPlatform__Model=qwen2.5:7b
~~~

Model cần được tải trước trên máy chủ Ollama. Không mở Ollama trực tiếp ra Internet; chỉ cho phép AI API truy cập endpoint nội bộ. Có thể thay model bằng model tiếng Việt đã được FCC kiểm thử.

## 6. HR case workspace và quyết định

Trang Blazor route /ai/hr-assistant:

- Tạo case OT/attendance/leave; nhập số giờ OT, khoảng giờ vào/ra hoặc số ngày/mã loại nghỉ mà nhân viên phản hồi.
- Tải file evidence và kiểm tra trạng thái scan/extraction.
- Chặn phân tích nếu file chưa ở AwaitingHumanReview hoặc Approved.
- Hàng chờ HR lọc theo managed scope qua FVN API.
- Xem phản hồi nhân viên, official source/reference, internal note, từng comparison và candidate evidence theo trang/dòng.
- Chỉnh sửa câu trả lời đề xuất, ghi chú nội bộ, approve/edit/reject.

Approved ở đây là HR đã duyệt câu trả lời case, không phải tự động phê duyệt đơn OT/nghỉ phép. Chưa có chức năng gửi email/thông báo câu trả lời cho nhân viên.

## 7. API

### FVN_REGISTER API – dùng từ Blazor, có JWT

| Method | Route | Mục đích |
|---|---|---|
| POST | /api/ai/hr-cases/analyze | Lấy facts có quyền, gọi AI phân tích |
| GET | /api/ai/hr-cases/pending | Hàng chờ đã lọc theo managed scope |
| GET | /api/ai/hr-cases/{caseId} | Chi tiết hồ sơ có kiểm tra quyền |
| POST | /api/ai/hr-cases/{caseId}/review | Duyệt/chỉnh sửa/từ chối |
| POST | /api/ai/hr-cases/evidence | Upload bằng chứng |
| GET | /api/ai/hr-cases/evidence/{documentId} | Trạng thái scan/extraction |
| GET | /api/ai/hr-cases/evidence/{documentId}/extraction | Candidate của người tải lên |

### AI API – private service-to-service

Các endpoint /api/ai/v1/hr-cases/* được bảo vệ bằng API key và HMAC signed tenant/actor context. Không gọi trực tiếp từ trình duyệt.

AIPlatform:TrustFvnRegisterGatewayForHRReview=true chỉ bật trên AI API khi AI API chỉ truy cập được từ FVN API gateway/firewall nội bộ, API key và ContextSigningKey chỉ nằm trong secret store của hai backend, và FVN API thực hiện authorization trước khi gọi AI API. Nếu không đáp ứng điều kiện này, giữ false.

## 8. SQL và triển khai

Database AI phải tách riêng khỏi database nghiệp vụ FVN_REGISTER. Chạy theo thứ tự:

1. SQL/AI/001_CreateAiIngestion.sql
2. SQL/AI/002_ReliableIngestionQueue.sql
3. SQL/AI/003_Review_Knowledge_Feedback.sql
4. SQL/AI/004_HR_Reconciliation_Cases.sql

Migration 004 tạo:
- ai.AiHRCases: facts provenance, analysis, evidence candidate, draft reply, review result.
- ai.AiHRCaseReviews: lịch sử approve/edit/reject.
- RowVersion: optimistic concurrency để không duyệt hai lần đồng thời.
- Index tenant/workdate/type và tenant/status/created.

### Cấu hình FVN API

Dùng secret/environment variables:

~~~text
AIPlatform__BaseUrl=https://<internal-ai-api-host>/
AIPlatform__ApiKey=<same-secret-as-ai-api>
AIPlatform__ContextSigningKey=<same-secret-as-ai-api>
AIPlatform__TenantCode=FCCVN
~~~

### Cấu hình AI API và Worker

~~~text
ConnectionStrings__FvnAi=<dedicated-FVN_AI-SQL-connection>
AIPlatform__ApiKey=<same-secret-as-fvn-api>
AIPlatform__ContextSigningKey=<same-secret-as-fvn-api>
AIPlatform__TrustFvnRegisterGatewayForHRReview=true
AIPlatform__MalwareScanner__Provider=ClamAV
AIPlatform__MalwareScanner__Host=127.0.0.1
AIPlatform__MalwareScanner__Port=3310
~~~

Cả AI API và AI Worker phải dùng cùng AI SQL, StorageRoot và MalwareScanner config. Cấu hình ClamAV daemon để chỉ nhận kết nối từ AI service host. StorageRoot phải nằm trên volume riêng, backup và ACL chỉ cho service account.

## 9. Kiểm thử

### Lệnh build/test

~~~powershell
dotnet restore FVN_REGISTER.sln
dotnet build FVN_REGISTER.sln --configuration Release --no-restore
dotnet test Tests/FVN_REGISTER.AI.Tests/FVN_REGISTER.AI.Tests.csproj --configuration Release
~~~

### Kịch bản kiểm thử nghiệp vụ

1. OT: evidence có E0001, ngày 01/10/2026, 2 giờ; FVN reports có planned 2, actual 1.5, attendance OT 1.5. Kết quả phải có Discrepancy, chênh 0.5 giờ, không tự sửa dữ liệu.
2. Attendance: evidence có giờ vào/ra khác report. Phải có ATT-SOURCE-001.
3. Evidence có mã nhân viên hoặc ngày khác case: phải có EVIDENCE-EMPLOYEE-001 / EVIDENCE-WORKDATE-001.
4. Leave: kiểm tra đúng LeaveTypeCode, RequestStatus và TotalDay; đơn nhiều ngày cần HR xác minh phân bổ ngày.
5. Evidence chưa scan hoặc scan Unknown: phân tích phải bị chặn.
6. Người dùng không có managed scope tới nhân viên: FVN API phải trả 403.
7. HR không có function review tương ứng: detail/review phải bị chặn.
8. Hai HR user duyệt đồng thời: một request thành công, request còn lại nhận conflict.
9. Ollama ngừng hoạt động: case vẫn lưu với fallback, không tự gửi.

## 10. Giới hạn đã biết và phần không được tuyên bố hoàn thành

- OCR cho scanned PDF, DOCX, trích xuất bảng nâng cao và xác minh layout chưa được triển khai.
- LLM chỉ soạn câu trả lời; không quyết định nguyên nhân gốc khi dữ liệu không chứng minh được.
- Leave report hiện dùng tổng ngày của đơn; đối soát phân bổ từng ngày cần nguồn detail theo ngày.
- HR Policy RAG (quản trị phiên bản quy định, chunking, embeddings, ACL retrieval và trả lời có trích dẫn điều khoản) là bounded context riêng, chưa được triển khai trong luồng case này. Không dùng case evidence cá nhân làm kho quy định chung.
- Chưa có gửi câu trả lời đã duyệt đến nhân viên; trạng thái và final reply được lưu để tích hợp kênh thông báo sau.

Các giới hạn này được giữ rõ ràng để HR không nhận một kết luận do AI tự suy diễn.
