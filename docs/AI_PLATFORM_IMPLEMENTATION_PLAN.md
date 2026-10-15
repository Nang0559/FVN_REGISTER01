# FVN_REGISTER AI Center — Implementation Plan

## Objective

Complete a reusable AI Core (Phase 1–5) before expanding FVN_REGISTER-specific AI applications (Phase 6). Do not create a separate OCR, LLM, embedding, RAG or feedback implementation for every business module.

## Phase 1 — Universal Data Ingestion

- Define a common ingestion contract for uploads, folders and future connectors.
- Support safe document formats and structured data sources with content-signature validation.
- Preserve immutable original, hash, tenant, uploader, source and retention metadata.
- Use durable queue, idempotency, bounded workers, retry, dead-letter and recovery.
- Fail closed on malware scan uncertainty and unsupported formats.

**Exit gate:** file/source intake, duplicate race, malware, quarantine, retry, worker restart and retention tests pass.

## Phase 2 — AI Document Understanding

- Extract text, tables and layout with source locators.
- Integrate OCR for image-only documents and scanned PDFs.
- Classify content using a controlled taxonomy independent of business module.
- Support model-provider capabilities separately: classification, structured extraction, chat and embeddings.
- Preserve language, page/row/cell/region and confidence provenance.

**Exit gate:** Vietnamese/Japanese sample corpus produces measurable classification and extraction results; scanned and native files both reach the same normalized representation.

## Phase 3 — Data Extraction & Validation

- Create tenant/module-scoped versioned schema registry.
- Define field paths, types, required/optional, ranges, patterns, allowed values and cross-field rules.
- Map spreadsheet columns, table cells and document regions to schema fields.
- Run structured extraction and deterministic validation through one pipeline.
- Persist candidates, validation issues, schema/model/prompt versions and evidence locators.
- Human review accepts, corrects or rejects candidates; official writes remain with authorized FVN_REGISTER commands.

**Exit gate:** the same engine processes at least two different module schemas without module-specific extraction code; validation and review history are reproducible.

## Phase 4 — RAG & Knowledge Base

- Separate knowledge assets from personal evidence and HR case files.
- Add policy metadata, lifecycle, owner, effective dates, approval and supersession.
- Explicitly publish only reviewed/approved knowledge.
- Chunk by document structure, preserve citations, embed and index.
- Use tenant and ACL filters before content is returned to the model.
- Add hybrid retrieval, reranking, citation validation, abstention and prompt-injection defenses.

**Exit gate:** policy Q&A returns source-verifiable answers for authorized users, refuses unauthorized content and abstains when evidence is missing/conflicting.

## Phase 5 — Active Learning

- Capture accept/reject/correction and answer feedback with exact parser/model/prompt/schema versions.
- Build human-verified golden datasets with PII governance.
- Measure field-level accuracy, classification, retrieval and answer quality.
- Convert reviewed corrections into evaluation examples; do not auto-train on raw feedback.
- Gate model/prompt/schema releases with regression evaluation, staged rollout and rollback.

**Exit gate:** quality metrics are reproducible and a model/prompt release cannot be promoted without passing approved evaluation gates.

## Phase 6 — FVN_REGISTER applications

After Core gates pass, build thin application adapters:
- Registration: OT, leave, trip, equipment and generic requests.
- Data entry/import: Excel, PDF, scanned forms and structured files.
- Knowledge: policy/process Q&A.
- HR: reconciliation and employee support.
- Reporting: natural-language access to existing authorized reports.

Each application supplies schema, business rules, official data adapters and permission context. AI Core remains responsible for common AI capabilities.

## Cross-cutting requirements

- Separate FVN_AI database and deployment from FVN_REGISTER business database.
- FVN_REGISTER remains identity/RBAC/system of record; AI cannot write operational tables.
- Signed backend tenant/actor context; no trust in browser-supplied identity or ACL.
- Audit, privacy, retention, observability, cost limits and provider outage isolation.
- Tests for tenant isolation, ACL isolation, malicious files, prompt injection, provenance, concurrency and failure recovery.

## Deployment and verification

SQL migration order is maintained in [Phase Status](AI_PLATFORM_PHASES_STATUS.md). GitHub Actions validates restore/build/tests, but local FCC SQL, OCR runtime, storage, firewall, secret management and end-to-end authorization must also be verified before production.
