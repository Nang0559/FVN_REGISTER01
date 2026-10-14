# FVN_REGISTER AI Platform — Phase 1 to 5 delivery status

Branch: feature/ai-manufacturing-data-platform  
Runtime target: .NET 9 / SQL Server  
Status is conservative: a contract/schema is not marked as a running capability until its provider and worker path exist.

## Phase 1 — Ingestion foundation

Implemented:
- Dedicated AI projects and provider-neutral ports.
- PDF/XLSX/CSV/TXT upload allowlist and basic signature/content validation.
- Original file storage adapter, SHA-256, tenant-scoped duplicate detection.
- SQL document/job metadata and upload/status endpoints.
- API key plus signed tenant/actor HMAC context, timestamp tolerance and in-process nonce replay rejection.
- SQL queue claim, lease recovery, complete/fail procedures with retry and dead-letter states.
- Worker now claims jobs, runs the document pipeline with bounded concurrency, completes jobs or records retry/dead-letter failures.
- ClamAV clamd INSTREAM adapter is implemented; scanner remains fail-closed and returns Unknown when not configured or unavailable.

Not yet production-complete:
- Configure and deploy ClamAV service; set the same scanner settings for Worker and API.
- Lease renewal/heartbeat for long-running OCR/LLM tasks (current worker lease is 600 seconds).
- Shared nonce cache for multiple API replicas.
- Production object storage, encryption, retention, backup/restore and audit.
- FVN_REGISTER backend signing implementation and secret rotation.
- SQL/API integration and concurrency tests.

## Phase 2 — Document understanding

Implemented:
- Local deterministic text extraction for UTF-8 TXT/CSV and basic XLSX OpenXML cell content.
- Provider-neutral text extraction, classification and structured extraction ports.
- Persistence schema for extraction candidates and validation issues.
- Candidate data is separate from official FVN_REGISTER business records.

Not yet implemented:
- PDF text extraction and scanned PDF/image OCR.
- Production classifier/LLM structured extractor and schema registry.
- PDF text extraction and scanned PDF/image OCR (Tesseract + PDF rasterizer adapter still required).
- Ollama LLM provider is not yet wired into the pipeline; current candidate generation is deterministic/local and intended as a runnable baseline.
- Page/region-level provenance, confidence calibration and golden-set evaluation.
- Vietnamese/Japanese document evaluation corpus.

## Phase 3 — Human review

Implemented:
- Tenant-scoped extraction query endpoint.
- Review persistence with original payload snapshot, optional corrected JSON, reviewer, decision and timestamp.
- Corrected JSON becomes the latest candidate while the original is retained in the review audit record.
- Document status update in the AI database only.
- Feedback endpoint records reviewer feedback.

Not yet implemented:
- Blazor review UI showing original document beside candidate fields and source locations.
- Fine-grained review permission integration through the FVN_REGISTER backend.
- Validation-rule authoring, master-data validation and approved business command workflow.
- FVN_REGISTER endpoint that re-authorizes and validates any approved command before writing.

## Phase 4 — Knowledge / RAG

Implemented:
- SQL schema for knowledge chunks, source document/review linkage, tenant and access-scope metadata, embedding metadata.
- Provider-neutral embedding and vector-store ports already defined.

Not yet implemented:
- Embedding provider and vector-store adapter.
- Chunk creation only from approved/reviewed documents.
- ACL filtering before retrieval/model context, including signed department/role scope propagation.
- Search API, citation assembly, prompt-injection defenses and retrieval evaluation.

## Phase 5 — Feedback and model governance

Implemented:
- Tenant-scoped feedback capture with accept/reject/correct/answer-rating types.
- Feedback data model and model deployment registry schema.
- Feedback does not trigger automatic training.

Not yet implemented:
- Feedback analytics dashboard and evaluation jobs.
- Model deployment management API/UI, staged rollout, rollback and drift/cost monitoring.
- Golden-set regression pipeline and approved model release process.

## SQL migration order

Run on the dedicated FVN_AI database only:

1. SQL/AI/001_CreateAiIngestion.sql
2. SQL/AI/002_ReliableIngestionQueue.sql
3. SQL/AI/003_Review_Knowledge_Feedback.sql

## Validation

A GitHub Actions workflow was added to restore/build the solution and run the AI test project on Windows with .NET 9. AI projects were explicitly enabled for solution builds. A successful workflow result has not yet been confirmed. Do not treat this branch as production-ready until the workflow passes and the integration/security gates above are closed.

## Architectural invariants

- AI is a separate bounded context and has its own database/schema.
- AI does not reference the FVN_REGISTER business DbContext.
- AI never writes directly to FVN_REGISTER business tables.
- Tenant/actor identity must be signed by the trusted FVN_REGISTER backend after its RBAC checks.
- Retrieved knowledge must be filtered by tenant and authorized scope before it reaches a model.
- Unknown antivirus verdict, unconfigured provider or unsupported document type fails closed; no fabricated result is allowed.
