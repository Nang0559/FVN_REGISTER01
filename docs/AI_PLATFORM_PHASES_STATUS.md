# FVN_REGISTER AI Center — Implementation Status

Branch: `feature/ai-manufacturing-data-platform`  
Target: .NET 9 / SQL Server  
Roadmap: **5 AI Core phases + Phase 6 FVN_REGISTER applications**.

This document is the single status reference for the agreed roadmap. A phase is not complete merely because its schema, interface, or UI exists. Completion requires an executable end-to-end flow, authorization, tests, deployment instructions, and operational verification.

## Executive status

| Phase | Scope | Current status | Remaining exit criteria |
|---|---|---|---|
| 1 | Universal Data Ingestion | In progress | OCR inputs, broader formats/connectors, end-to-end security and failure tests |
| 2 | AI Document Understanding | In progress | OCR/layout, table understanding, robust classification, schema extraction pipeline and evaluation |
| 3 | Data Extraction & Validation | In progress | Schema administration/API, pipeline orchestration, persisted validation results, review workflow and module contracts |
| 4 | RAG & Knowledge Base | In progress | Publication governance API, chunking/evaluation, RAG answer generation, citation validation, ACL integration and UI |
| 5 | Active Learning | In progress | Golden sets, field-level evaluation, feedback-to-dataset pipeline, controlled model/prompt rollout and rollback |
| 6 | FVN_REGISTER AI Applications | Pilot only | Re-integrate HR and build reusable registration/data-entry/reporting applications after Core gates pass |

## Phase 1 — Universal Data Ingestion

### Implemented foundations
- Separate AI Core projects, dedicated FVN_AI database and original document storage.
- PDF, XLSX, DOCX, PNG, JPEG, CSV and TXT upload validation; Office ZIP structures are inspected with expanded-size limits and image/PDF signatures are checked.
- File signature checks, expanded ZIP limits, upload size limits, SHA-256 and tenant duplicate detection.
- Quarantine-first lifecycle, malware scanning adapter and fail-closed unknown verdict.
- SQL queue, leased workers, retry/dead-letter and expired lease recovery.
- Tenant/actor signed gateway context and replay protection.

### Remaining
- Tesseract/Poppler OCR adapter is implemented for PNG/JPEG and scanned PDF; production still requires installing/configuring Tesseract language packs and Poppler on API/Worker hosts. Additional office formats and connector-based sources remain open.
- Configurable content limits by media type, retention/deletion policies and storage encryption/backup verification.
- Cross-replica nonce persistence and shared storage/queue operations.
- End-to-end tests for corrupt files, ZIP bombs, malicious files, duplicate races, interrupted uploads and lease recovery.

## Phase 2 — AI Document Understanding

### Implemented foundations
- Text extraction for TXT/CSV, XLSX, text-layer PDF and DOCX.
- Provider-neutral document classifier with model-based classification and deterministic fallback.
- Ollama chat provider and independent Ollama embedding provider configuration.
- Schema-driven structured extraction contract and model adapter; candidates preserve source references and confidence.

### Remaining
- OCR adapter for PNG/JPEG and scanned PDF using Tesseract + Poppler; verify binaries, language packs, timeout and quality on the FCC hosts.
- Layout-aware extraction for tables, forms, merged cells, headers/footers and multi-column documents.
- Classification taxonomy management and confidence calibration.
- Robust JSON/schema-constrained model output, prompt versioning and language-specific golden corpus.
- Extraction pipeline must load a published schema and invoke structured extraction + validation end to end.

## Phase 3 — Data Extraction & Validation

### Implemented foundations
- Versioned schema contracts, tenant/module-scoped schema registry and SQL migrations 005 and 006.
- Draft/publish schema version lifecycle.
- Deterministic validation for required fields, primitive types, length/range, regex, allowed values, confidence and cross-field rules.
- Human review and correction persistence; evidence and tenant ownership checks.

### Remaining
- Authorized schema administration endpoints and UI.
- Bind the ingestion worker to a published module schema, structured extraction and validation.
- Persist validation runs/issues with schema version, model/prompt version and evidence provenance.
- Mapping profiles for source columns/cells to schema paths; support multiple rows and repeated records.
- Review queue and approved output contract for downstream module adapters.
- Schema migration/compatibility policy and test coverage for nested objects, arrays, localization and ambiguous values.

## Phase 4 — RAG & Knowledge Base

### Implemented foundations
- Knowledge chunk SQL table and EF mapping.
- Ollama embedding adapter.
- SQL-backed cosine vector search with tenant filtering and access-scope filtering before content is returned to the RAG caller.
- Explicit publication service: only human-approved extraction reviews can be indexed; upload alone does not publish knowledge.
- Chunk metadata includes document, review, page/section, ordinal and access scopes.
- Unpublish operation deactivates indexed chunks.

### Remaining
- Knowledge publication/search/unpublish and RAG answer API endpoints are present. Gateway-side authorization, admin UI, hybrid retrieval and end-to-end evaluation remain required.
- Policy lifecycle (draft, review, publish, effective dates, expiry, supersession and withdrawal).
- Better chunking for headings, clauses, tables, Japanese/Vietnamese mixed content and source page/line citations.
- Hybrid lexical/vector retrieval, reranking, deduplication and retrieval quality evaluation.
- Grounded answer orchestration, retrieved-chunk citation ID validation and abstention are implemented. Semantic claim-to-source verification, hybrid retrieval/reranking, prompt-injection red-team tests and RAG UI remain open.
- ACL derivation from trusted FVN_REGISTER permissions; never accept scopes from an untrusted browser.
- RAG conversation UI, trace/audit and retention controls.
- SQL cosine search is a moderate-scale baseline; benchmark before production scale and introduce a vector database adapter if needed.

## Phase 5 — Active Learning

### Implemented foundations
- Accept/Reject/Correct/AnswerRating feedback capture.
- Feedback stores parser/model/prompt version provenance when linked to an extraction.
- Tenant-scoped evaluation summary grouped by parser/model/prompt version.
- No automatic model training is triggered by user feedback.

### Remaining
- Golden datasets with human-verified labels and field-level metrics (precision, recall, exact match, confidence calibration).
- Feedback triage and conversion of corrections into reviewed training/evaluation examples.
- Regression evaluation before model, prompt, parser or schema releases.
- Model registry lifecycle, approval, staged rollout, rollback, drift and cost monitoring.
- Data governance: PII minimization, retention, anonymization and explicit training eligibility.

## Phase 6 — FVN_REGISTER Applications

HR Case Assistant remains a pilot application, not the AI platform itself. Its official facts must continue to come from authorized FVN_REGISTER services. AI must not write operational business tables or make approval/payroll decisions.

After Phase 1–5 exit gates pass, application adapters can consume shared ingestion, extraction, validation and RAG services for OT, leave, business trip, equipment, generic requests, Excel import, policy Q&A and reporting.

## SQL migration order

Run only against the dedicated FVN_AI database:

1. `SQL/AI/001_CreateAiIngestion.sql`
2. `SQL/AI/002_ReliableIngestionQueue.sql`
3. `SQL/AI/003_Review_Knowledge_Feedback.sql`
4. `SQL/AI/004_HR_Reconciliation_Cases.sql`
5. `SQL/AI/005_Universal_AI_Core.sql`
6. `SQL/AI/006_Document_Schema_Routing.sql`

Do not run AI migrations against the FVN_REGISTER operational database.

## Configuration

- API and Worker must share the same SQL connection, storage root (or shared durable storage), malware scanner and signing secrets.
- `AIPlatform:DefaultProvider=Ollama`, `Endpoint`, `Model` and `EmbeddingModel` configure Ollama chat and embedding capabilities in both API and Worker.
- `AIPlatform:Ocr:TesseractExecutable`, `PdfToPpmExecutable`, `Languages`, `Dpi` and `TimeoutSeconds` configure local OCR in both API and Worker.
- AI API key and HMAC signing key must be stored in a secret manager/environment variables, not committed.
- RAG scopes must be supplied by a trusted FVN_REGISTER authorization adapter, never directly from client input.

## Validation status

GitHub Actions workflow `.github/workflows/ai-platform-validation.yml` runs restore/build/tests. The latest run must be checked before claiming a passing build. Local FCC environment integration, SQL migrations, OCR runtime and security tests are not implied by a green compile.

## Architectural invariants

- AI Core is module-neutral; HR/OT/leave/equipment rules belong to application adapters or published schemas.
- AI owns its database and document/knowledge lifecycle; FVN_REGISTER remains source of truth for operational data and identity.
- AI does not access FVN_REGISTER business DbContext and does not write official operational records.
- Tenant, actor and permission context must be established by trusted backend services.
- Evidence, personal case files and policy knowledge are separate data classes.
- Only explicitly published and ACL-scoped knowledge can be retrieved by RAG.
- Extraction output is a candidate; human review and authorized business workflows remain authoritative.
- Feedback is evaluation data, not permission for automatic training.
- Unsupported formats, invalid signatures, unknown malware verdicts and unauthorized scopes fail closed.
