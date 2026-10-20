# FVN_REGISTER AI Platform — Phase Status

> **Status convention:** `Foundation / partial` means contracts and infrastructure exist but the phase exit gate is not complete. Phase 1–5 are not production-complete.

## Current branch

- Branch: `feature/ai-platform-phase1-6-integration`
- The root file is the **single source of truth** for legacy AI Core phase readiness. Product roadmap P1–P8 is defined separately in [docs/AI_PLATFORM_P1_P8_ROADMAP.md](docs/AI_PLATFORM_P1_P8_ROADMAP.md).

## Phase status

| Phase | Status | Current implementation | Exit-gate / production work remaining |
|---|---|---|---|
| 1 — Universal Data Ingestion | Foundation / partial | Tenant-scoped ingestion, SHA-256 dedup, quarantine, source metadata, folder connector, leased queue, retention | Multi-replica lease/restore verification, archive/file safety tests and operational SQL verification |
| 2 — Document Understanding | Foundation / partial | Provider-neutral representation, text/table/OCR adapters, classification and provenance contracts | Real provider/OCR end-to-end verification and malformed-document coverage |
| 3 — Extraction & Validation | Foundation / partial | Versioned schema registry, mappings, deterministic validation and provenance | Persist validation runs/issues with full schema/model/prompt/evidence provenance; bind worker to published schema; end-to-end review queue tests |
| 4 — RAG & Knowledge Base | Foundation / partial | Human-approved publication, ACL/tenant-filtered cosine retrieval, citation validation, retention cleanup | Policy effective/expiry/replacement lifecycle, real authorization adapter, UI, retrieval-quality benchmark and red-team tests |
| 5 — Active Learning & Controlled Release | Foundation / partial | Release-bound extraction/feedback, release-scoped golden evaluations, candidate/active/retired/rollback lifecycle, human-decision gate | Real FVN_REGISTER authorization adapter, field-level quality/calibration, drift/cost/PII governance, full pipeline-to-release evaluation workflow |
| 6 — FVN_REGISTER AI Applications | Pilot foundation / integration pending | HR Assistant adapter boundary, deterministic reconciliation and human-review workspace | Real FVN_REGISTER gateway/authorization, production HR data integration and UI/API pilot verification |

## P1–P8 product roadmap status

The P1–P8 sequence is a separate product architecture roadmap, not a renumbering of the Core phases above.

| Product phase | Status on this branch | Notes |
|---|---|---|
| P1 — AI Foundation | Existing foundation / partial | Existing provider, model, ingestion and safety components; orchestrator/prompt/context governance still requires a complete exit gate |
| P2 — FVN Business Context | Authenticated context projection + fail-closed AI ports / integration partial | `GET /api/ai/business-context/me` derives actor from the server principal, loads effective permissions/scopes from `IAuthorizationService`, returns module/function metadata plus deterministic managed-scope selectors and disables caching. `POST /api/ai/business-context/ai` now obtains that projection from the authenticated FVN request and forwards the exact JSON snapshot to `/api/ai/v1/business-context/snapshot` with a body-bound HMAC and replay nonce. The AI endpoint validates signed tenant/actor against the snapshot body and returns the accepted short-lived context. Live business tool catalog/executor adapters and end-to-end authorization tests remain |
| P3 — FVN Knowledge | Existing foundation / partial | Existing RAG uses ACL-filtered cosine retrieval; hybrid search and full policy lifecycle remain |
| P4 — Business Tools | Contracts + fail-closed gateway only | Context/catalog/executor ports and default-deny adapter are registered; no business tool is exposed or executable yet. Implement allow-listed typed handlers, fresh host authorization, managed-scope enforcement and provenance before enabling any tool |
| P5 — Task Assistant | Not implemented | Must use workflow-owned task APIs |
| P6 — Controlled Learning | Existing foundation / partial | Existing feedback/release pipeline; broader approved knowledge lifecycle and evaluation gates remain |
| P7 — Document Intelligence | Existing foundation / partial | Existing OCR/extraction foundation; handwriting/vision and cross-module validation remain |
| P8 — Transaction Agent / Production & Quality | Not implemented | Requires explicit preview/confirmation, idempotent commands and audit before any write capability |

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


## P1–P8 integration hardening update

- The FVN business-context projection groups the caller's effective function metadata by module and sends `Cache-Control: no-store` to prevent cross-account context replay by browser/proxy caches.
- The AI service registers `IAuthorizedFvnBusinessContextProvider`, `IAuthorizedFvnBusinessToolCatalog` and `IAuthorizedFvnBusinessToolExecutor` through a single fail-closed default. Its `/api/ai/v1/business-context/me` endpoint is inside the HMAC/replay-protected gateway group and resolves tenant/actor only from signed headers. Missing host integration returns `FVN_BUSINESS_GATEWAY_NOT_CONFIGURED`; it does not synthesize an identity or expose tools.
- `/api/ai/capabilities` reports whether the trusted context provider, catalog and executor are actually configured. Contracts/default-deny wiring are not counted as a production business-tool capability.
- Added unit tests proving context lookup, tool discovery and tool execution fail closed when the trusted host adapter is absent.

- The signed host forwarding route uses the configured API key, tenant, actor, timestamp, nonce and body-bound HMAC signature. `POST /api/ai/business-context/ai` now forwards the host-generated permission/scope snapshot to the AI snapshot endpoint; the AI service checks the snapshot tenant/actor against the signed headers. This enables trusted context handoff, but does not configure business tool discovery or execution.

- Managed-scope selectors are now normalized from the real `ManagedScopeDto` fields (`NodeType`, `NodeCode`, `FactoryCode`, `DeptCode`, `SubDepartmentCode`, `IncludeChildren`), sorted deterministically, and included in `contextVersion`. Free-text remarks and employee lists are deliberately excluded.
