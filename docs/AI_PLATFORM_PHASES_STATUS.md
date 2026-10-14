# FVN_REGISTER AI Platform — trạng thái triển khai

Branch: feature/ai-manufacturing-data-platform  
Runtime target: .NET 9 / SQL Server  
Quy ước: phân biệt rõ mã nguồn đã bổ sung với tích hợp cần cấu hình và kiểm thử trên môi trường FCC.

## Tóm tắt trạng thái

| Năng lực | Mã nguồn | Cần cấu hình / xác minh |
|---|---|---|
| Upload, lưu file, SHA-256, queue | Đã có | SQL, storage, antivirus |
| TXT/CSV/XLSX/PDF text extraction | Đã có | Kiểm thử file thực tế; OCR scanned PDF chưa có |
| Ollama chat provider | Đã có | Ollama endpoint/model, prompt evaluation |
| Extraction candidate/review/feedback | API và persistence đã có | HR review UI cho extraction độc lập |
| HR Case Assistant OT/attendance/leave | Gateway, facts mapping, case persistence và UI đã có | RBAC mapping, SQL integration, end-to-end build/test |
| HR Policy RAG | Thiết kế | Publication, chunking, embedding, retrieval, citations, chat UI |
| HR reporting chatbot | Thiết kế | Intent mapping, report UI |

## Phase 1 — Ingestion foundation

Đã có:
- Các project AI riêng và provider-neutral ports.
- Allowlist PDF/XLSX/CSV/TXT, validation chữ ký file và giới hạn dung lượng.
- Private original-document store, SHA-256, duplicate detection theo tenant.
- SQL metadata và reliable ingestion queue.
- Claim, retry, dead-letter, expired lease recovery, lease owner fencing.
- Worker xử lý song song có bounded concurrency; lease được gia hạn trong lúc xử lý job dài.
- ClamAV clamd INSTREAM adapter. Verdict không rõ hoặc scanner chưa cấu hình luôn fail-closed.
- API key, HMAC signed tenant/actor, timestamp tolerance và nonce replay protection trong tiến trình.

Cần cấu hình trước production:
- ClamAV daemon và scanner config giống nhau ở AI API/Worker.
- Shared nonce store nếu chạy nhiều AI API replicas.
- Shared durable storage nếu API/Worker chạy nhiều máy; encryption, ACL, backup/restore, retention.
- Secret rotation, TLS, firewall và network isolation.
- SQL/API integration, concurrency, lease recovery và failure-injection tests.

## Phase 2 — Document understanding

Đã có:
- UTF-8 TXT/CSV và basic XLSX OpenXML extraction.
- PDF text-layer extraction bằng PdfPig; page marker được giữ trong evidence rows.
- Deterministic candidate extraction: employee code, date/time/hour/day, leave type candidate.
- Page/line evidence candidate được lưu trong extraction JSON.
- Ollama chat model provider có cấu hình qua AIPlatform Endpoint/Model; chỉ đăng ký khi DefaultProvider=Ollama.
- Nếu Ollama lỗi hoặc chưa cấu hình, HR reply drafting dùng safe deterministic fallback.

Còn thiếu:
- OCR scanned/image-only PDF, PDF rasterizer và Tesseract.
- DOC/DOCX, bảng/layout-aware extraction nâng cao.
- Structured LLM extraction có schema validation và confidence calibration.
- Golden corpus tiếng Việt/Nhật và evaluation pipeline.

## Phase 3 — Human review và HR Case Assistant

Đã có:
- Tenant/uploader-scoped document status và extraction lookup.
- Extraction review/feedback persistence.
- FVN API gateway AIHRCaseController dùng JWT, IAuthorizationService và employee managed scope.
- Facts lấy qua IReportDispatcher; AI không đọc FVN business DbContext và không ghi dữ liệu nghiệp vụ.
- OT facts: OTDetail, OTSummaryByEmployee, AttendanceDetail. Report OT bổ sung ActualRecordCount để phân biệt actual chưa có với actual bằng 0.
- Attendance facts: check-in/out, shift, total hours, OT hours, holiday từ AttendanceDetail.
- Leave facts: StartDate/EndDate/TotalDay/LeaveTypeCode/RequestStatus từ LeaveDetail.
- Missing report rows được biểu diễn thành evidence gap, không bị hiểu nhầm là lỗi server hoặc số 0.
- HRReconciliationService so sánh OT/attendance/leave và evidence identity/date.
- Evidence ownership, malware scan state và extraction readiness được kiểm tra trước phân tích.
- HR case lưu facts provenance, comparison JSON, evidence candidate JSON và suggested reply.
- HR Case inbox/detail/review API, FVN API lọc theo function permission và employee managed scope.
- Review decision lưu cả snapshot hiện tại và bảng audit; rowversion chống hai người duyệt cùng lúc.
- Blazor workspace tại /ai/hr-assistant: tạo case, upload/kiểm tra evidence, xem queue, evidence page/line, comparisons, sửa reply và approve/edit/reject.
- Ollama được dùng để soạn reply, không được quyền thay đổi comparison outcome hoặc dữ liệu nghiệp vụ.

Cần cấu hình/xác minh:
- AI API TrustFvnRegisterGatewayForHRReview chỉ bật khi AI API nằm sau private gateway/firewall và secrets chỉ được giữ ở backend.
- Xác nhận các function code OTView, AttendanceView, LeaveView, OTReconcile, AttendanceFeedback, LeaveApprove với ma trận RBAC FCC hiện hành.
- Xác nhận report field names/semantics trên database thật; đặc biệt LeaveTypeCode và leave TotalDay.
- Chạy SQL migrations và test thực tế với FVN_AI + FVN_REGISTER.
- OCR, download original evidence preview và gửi final reply cho nhân viên chưa triển khai.

Chi tiết: [AI_HR_CASE_ASSISTANT_IMPLEMENTATION.md](AI_HR_CASE_ASSISTANT_IMPLEMENTATION.md).

## Phase 4 — HR Policy Knowledge / RAG

Đã có:
- Schema và provider-neutral ports cho knowledge chunks, metadata, embedding/vector store.
- Thiết kế lifecycle policy, version/effective date, HR approval/publication, clause-level citations và ACL retrieval.

Chưa triển khai:
- HR policy admin UI và upload/verify/approve/publish.
- Chunking từ policy versions đã publish.
- Embedding provider, Qdrant adapter, hybrid retrieval và reranking.
- ACL filtering trước khi đưa context vào model.
- Policy Q&A API/UI, citation validator, abstention và evaluation.

Không được dùng HR case evidence cá nhân làm kho quy định chung. HR Case Assistant là luồng evidence/facts; Policy RAG là bounded context khác.

Chi tiết: [AI_HR_POLICY_ASSISTANT.md](AI_HR_POLICY_ASSISTANT.md).

## Phase 5 — Feedback và model governance

Đã có:
- Feedback capture accept/reject/correct/answer-rating.
- Model deployment registry schema.
- Feedback không kích hoạt automatic training.

Chưa triển khai:
- Feedback analytics/evaluation dashboard.
- Model rollout/rollback, drift/cost monitoring.
- Golden-set regression và model release approval.
- Conversation/retrieval trace retention policy.

## SQL migration order

Chạy trên database FVN_AI riêng, theo đúng thứ tự:

1. SQL/AI/001_CreateAiIngestion.sql
2. SQL/AI/002_ReliableIngestionQueue.sql
3. SQL/AI/003_Review_Knowledge_Feedback.sql
4. SQL/AI/004_HR_Reconciliation_Cases.sql

Migration 004 tạo AiHRCases, AiHRCaseReviews, EvidenceCandidateJson và RowVersion. Không chạy các migration AI trên database vận hành FVN_REGISTER.

## Cấu hình quan trọng

FVN API:
- AIPlatform:BaseUrl
- AIPlatform:ApiKey
- AIPlatform:ContextSigningKey
- AIPlatform:TenantCode

AI API:
- ConnectionStrings:FvnAi
- AIPlatform:ApiKey
- AIPlatform:ContextSigningKey
- AIPlatform:TrustFvnRegisterGatewayForHRReview
- AIPlatform:DefaultProvider=Ollama, Endpoint và Model nếu dùng Ollama
- AIPlatform:MalwareScanner:Provider=ClamAV, Host, Port

Không commit secrets. Dùng environment variables, User Secrets hoặc secret manager.

## Validation

GitHub Actions workflow .github/workflows/ai-platform-validation.yml được cấu hình restore/build solution và chạy AI tests trên Windows/.NET 9. Tại thời điểm cập nhật tài liệu, chưa có kết quả build/test thành công được xác nhận. Người triển khai cần chạy:

~~~powershell
dotnet restore FVN_REGISTER.sln
dotnet build FVN_REGISTER.sln --configuration Release --no-restore
dotnet test Tests/FVN_REGISTER.AI.Tests/FVN_REGISTER.AI.Tests.csproj --configuration Release
~~~

Không đánh dấu production-ready cho tới khi build/test, migration, RBAC và end-to-end test đều pass.

## Architectural invariants

- AI là bounded context riêng, có database riêng.
- AI không tham chiếu FVN_REGISTER business DbContext.
- AI không ghi trực tiếp vào bảng nghiệp vụ FVN_REGISTER.
- Tenant/actor context được ký bởi backend đáng tin cậy.
- Facts chính thức phải lấy qua backend có RBAC và managed scope.
- Policy retrieval tương lai phải lọc publication status, effective dates, entity/site/audience và ACL trước model invocation.
- Chỉ policy versions được HR publish mới dùng cho policy answers.
- Employee evidence/case attachments không phải HR knowledge.
- Unknown antivirus verdict hoặc unsupported document type phải fail-closed.
- Model answer không được phê duyệt OT, leave, payroll hoặc quyết định nhân sự.
