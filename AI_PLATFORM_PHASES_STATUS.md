# FVN_REGISTER AI Platform — Phase Status

> **Status convention:** `Foundation / partial` means contracts and infrastructure exist but the phase exit gate is not complete. Phase 1–5 are not production-complete.

## Current branch

- Branch: `feature/ai-platform-phase1-6`
- The root file is the **single source of truth** for phase readiness. Do not maintain a second status document under `docs/`.

## Phase status

| Phase | Status | Current implementation | Exit-gate / production work remaining |
|---|---|---|---|
| 1 — Universal Data Ingestion | Foundation / partial | Tenant-scoped ingestion, SHA-256 dedup, quarantine, source metadata, folder connector, leased queue, retention | Multi-replica lease/restore verification, archive/file safety tests and operational SQL verification |
| 2 — Document Understanding | Foundation / partial | Provider-neutral representation, text/table/OCR adapters, classification and provenance contracts | Real provider/OCR end-to-end verification and malformed-document coverage |
| 3 — Extraction & Validation | Foundation / partial | Versioned schema registry, mappings, deterministic validation and provenance | Persist validation runs/issues with full schema/model/prompt/evidence provenance; bind worker to published schema; end-to-end review queue tests |
| 4 — RAG & Knowledge Base | Foundation / partial | Human-approved publication, ACL/tenant-filtered cosine retrieval, citation validation, retention cleanup | Policy effective/expiry/replacement lifecycle, real authorization adapter, UI, retrieval-quality benchmark and red-team tests |
| 5 — Active Learning & Controlled Release | Foundation / partial | Release-bound extraction/feedback, release-scoped golden evaluations, candidate/active/retired/rollback lifecycle, human-decision gate | Real FVN_REGISTER authorization adapter, field-level quality/calibration, drift/cost/PII governance, full pipeline-to-release evaluation workflow |
| 6 — FVN_REGISTER AI Applications | Pilot foundation / integration pending | HR Assistant adapter boundary, deterministic reconciliation and human-review workspace | Real FVN_REGISTER gateway/authorization, production HR data integration and UI/API pilot verification |

## Phase 5 hardening completed in this branch

- Extraction provenance records the provider/model returned by the classifier rather than the placeholder `provider-routed` value.
- Extraction rows can be associated with the matching candidate/active model release.
- Feedback inherits `ReleaseId` from the extraction; release-gate feedback counts are release-scoped.
- Golden evaluations carry `ReleaseId`; summaries for a release cannot reuse another release's latest evaluation.
- Extraction uniqueness is release-aware: `(DocumentId, SchemaVersion, ModelProvider, ModelVersion, PromptVersion)`, plus a separate guard for `SchemaVersion IS NULL`.
- Retention deactivates and clears knowledge chunks, removes extraction candidate/representation payloads, and purges expired `Rejected` documents too.
- Dead-letter/lease-terminal states propagate to the document (`Failed`); failed/dead-letter uploads can be requeued, while archived/rejected fingerprints no longer block re-upload.
- Content-bound request signatures are mandatory by default and startup refuses an explicit `false` configuration.
- Nonce retention is enforced at least 10 minutes for the ±5-minute timestamp acceptance window.
- Corrected human-review payloads are used as the publication text for RAG.
- Knowledge chunks use 6,000 characters with 500-character overlap and bounded embedding concurrency.

## SQL migration order

For an empty dedicated `FVN_AI` database run exactly:

`001 → 002 → 003 → 004 → 005 → 006 → 007 → 008 → 009 → 010 → 011 → 012 → 013 → 014`

Migration `014_Phase5_ReleaseRetentionTenantHardening.sql` is a forward migration; do not edit already-applied migrations. It adds release provenance, tenant composite constraints, retention/dedup hardening, replay-window enforcement and queue/document state propagation.

## CI and test boundary

`.github/workflows/ai-platform-sql-validation.yml` now:

- applies migrations 001–014 twice;
- checks release-aware extraction indexes, tenant columns/FKs, dedup index and migration checksums;
- verifies duplicate legacy chunk ordinals fail clearly;
- exercises concurrent lease claims and nonce replay;
- verifies terminal dead-letter marks the document `Failed`;
- verifies tenant-scoped duplicate behavior;
- runs `Tests/FVN_REGISTER.AI.Tests` including release-scoped golden evaluation and RAG prompt-injection safety tests.

Unit/SQL tests still do not replace real provider/OCR, FVN_REGISTER authorization-adapter, production backup/restore, or end-to-end worker verification.

## Verification boundary

The platform should be described as:

**Implementation-complete foundation — exit gate pending; not production-complete.**

No Phase 3/4/5 row should be labelled implementation-complete until the remaining exit-gate items above are demonstrated in CI/integration environments.
