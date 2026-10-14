# FVN_REGISTER AI Platform — Phase 1 to 5 delivery status

Branch: `feature/ai-manufacturing-data-platform`  
Runtime target: .NET 9 / SQL Server  
Status is conservative: contracts and schemas are not marked as running capabilities until their provider and end-to-end path are verified.

## Phase 1 — Ingestion foundation

Implemented in branch:
- Dedicated AI projects and provider-neutral ports.
- PDF/XLSX/CSV/TXT upload allowlist and basic signature/content validation.
- Original file storage adapter, SHA-256 and tenant-scoped duplicate detection.
- SQL document/job metadata and upload/status endpoints.
- API key plus signed tenant/actor HMAC context, timestamp tolerance and in-process nonce replay rejection.
- SQL queue claim, lease recovery, complete/fail procedures with retry and dead-letter states.
- Worker claims jobs, runs the document pipeline with bounded concurrency and records completion/failure.
- ClamAV `clamd INSTREAM` adapter; scanner remains fail-closed and returns Unknown when unconfigured/unavailable.

Still required before production:
- Configure and deploy ClamAV; configure identical scanner settings in Worker and API.
- Lease renewal/heartbeat for long-running OCR/LLM tasks (current worker lease is finite).
- Shared nonce cache for multiple API replicas.
- Shared production object storage accessible to API and Worker; encryption, retention, backup/restore and audit.
- FVN_REGISTER backend signing implementation and secret rotation.
- SQL/API integration, concurrency and failure-recovery tests.
- Verify Worker/API connection strings, storage root and migration deployment.

## Phase 2 — Document understanding

Implemented:
- Local deterministic extraction for UTF-8 TXT/CSV and basic XLSX OpenXML cell content.
- Provider-neutral text extraction/classification/structured extraction ports.
- Persistence schema for extraction candidates and validation issues.
- Candidate data remains separate from official FVN_REGISTER business records.

Still required:
- PDF text extraction and scanned PDF/image OCR (Tesseract + PDF rasterizer).
- Wire Ollama provider into a versioned structured extraction pipeline; provider adapter alone is not an end-to-end AI extraction feature.
- Schema registry, table/layout-aware parsing and validation rules.
- Page/region/row-level provenance, confidence calibration and golden-set evaluation.
- Vietnamese/Japanese document evaluation corpus.

## Phase 3 — Human review

Implemented:
- Tenant-scoped extraction query endpoint.
- Review persistence with original payload snapshot, optional corrected JSON, reviewer, decision and timestamp.
- Corrected JSON becomes latest candidate while original is retained in review audit.
- Document status update in AI database only.
- Feedback endpoint records reviewer feedback.

Still required:
- Blazor review UI showing original evidence beside candidate fields and source locations.
- Fine-grained review permission integration through trusted FVN_REGISTER backend.
- Validation-rule authoring, master-data validation and approved business command workflow.
- FVN_REGISTER endpoint that re-authorizes and validates any approved command before writing.
- Integration tests for review idempotency, correction audit and tenant isolation.

## Phase 4 — Knowledge and RAG

Implemented:
- SQL schema for knowledge chunks, source document/review linkage, tenant/access-scope metadata and embedding metadata.
- Provider-neutral embedding and vector-store ports.

Design completed; implementation still required:
- HR policy metadata, version/effective-date and publication workflow.
- HR policy upload/verification/approval UI; upload must not imply publication.
- Clause-aware chunk creation only from approved/effective policy revisions.
- Embedding provider and Qdrant vector-store adapter.
- Hybrid keyword/vector retrieval, reranking and ACL filtering before model context.
- Policy Q&A API, citation validation, answer abstention and Blazor chat UI.
- Retrieval/citation/ACL and Vietnamese/Japanese evaluation.

## Phase 5 — Feedback and model governance

Implemented:
- Tenant-scoped feedback capture with accept/reject/correct/answer-rating types.
- Feedback data model and model deployment registry schema.
- Feedback does not trigger automatic training.

Still required:
- Feedback analytics and evaluation jobs/dashboard.
- Model deployment management API/UI, staged rollout, rollback and drift/cost monitoring.
- Golden-set regression pipeline and approved model release process.
- Conversation/retrieval trace governance and retention configuration.

## Applied HR use cases — design status

### HR Policy Assistant
Design: [AI_HR_POLICY_ASSISTANT.md](AI_HR_POLICY_ASSISTANT.md)

Purpose: employee asks “How do I register OT?” and receives a clear step-by-step answer from the current approved HR procedure, with policy code/version/effective date/article/page citations.

Not yet implemented: policy administration, HR publication approval, clause-aware indexing, Qdrant retrieval, policy chat API/UI.

### HR feedback and reconciliation
Design: [AI_HR_RECONCILIATION_PILOT.md](AI_HR_RECONCILIATION_PILOT.md)

Pilot: analyze OT/attendance/leave evidence against authorized FVN_REGISTER facts, show deterministic differences and prepare a suggested HR reply. Human HR review remains mandatory.

Implemented baseline: typed HR case contracts, deterministic OT/attendance/leave comparison service, signed-context API endpoint `POST /api/ai/v1/hr-cases/analyze`, safe unconfigured facts-provider default and unit tests for OT discrepancy / employee mismatch.\n\nStill required: trusted FVN_REGISTER authorized facts adapter, evidence-candidate-to-case field mapping, durable case/review/reply persistence, HR case UI and reply approval workflow. The API deliberately returns 503 until the trusted facts adapter is configured.

### HR reporting chatbot
Design direction: natural-language intent/parameter extraction mapped to existing allowlisted report operations. It must not generate arbitrary SQL or bypass ReportController/IReportDispatcher authorization. Chat UI and report-query contract remain unimplemented.

## SQL migration order

Run on the dedicated FVN_AI database only:

1. `SQL/AI/001_CreateAiIngestion.sql`
2. `SQL/AI/002_ReliableIngestionQueue.sql`
3. `SQL/AI/003_Review_Knowledge_Feedback.sql`

## Validation

A GitHub Actions workflow exists to restore/build the solution and run AI tests on Windows with .NET 9. AI projects were explicitly enabled for solution builds. A successful workflow result and a local build/test have not yet been confirmed. Do not treat this branch as production-ready until the workflow passes and integration/security gates are closed.

## Architectural invariants

- AI is a separate bounded context and has its own database/schema.
- AI does not reference the FVN_REGISTER business DbContext.
- AI never writes directly to FVN_REGISTER business tables.
- Tenant/actor identity must be signed by trusted FVN_REGISTER backend after RBAC checks.
- Policy retrieval must filter status, effective dates, entity/site/audience and user ACL before model invocation.
- Only HR-approved policy versions are eligible for current policy answers.
- Employee evidence/case attachments are not general HR knowledge.
- Unknown antivirus verdict, unconfigured provider or unsupported document type fails closed.
- Model answers cannot approve policy, OT, leave or payroll decisions.
