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
| 4 — RAG & Knowledge Base | Foundation / partial | Human-approved publication, ACL/tenant-filtered cosine retrieval, citation validation, retention cleanup and assistant chat UI | Policy effective/expiry/replacement lifecycle, real authorization adapter, knowledge administration/review UI, retrieval-quality benchmark and red-team tests |
| 5 — Active Learning & Controlled Release | Foundation / partial | Release-bound extraction/feedback, release-scoped golden evaluations, candidate/active/retired/rollback lifecycle, human-decision gate | Real FVN_REGISTER authorization adapter, field-level quality/calibration, drift/cost/PII governance, full pipeline-to-release evaluation workflow |
| 6 — FVN_REGISTER AI Applications | Pilot foundation / integration pending | HR Assistant adapter boundary, deterministic reconciliation and human-review workspace | Real FVN_REGISTER gateway/authorization, production HR data integration and UI/API pilot verification |

## P1–P8 product roadmap status

The P1–P8 sequence is a separate product architecture roadmap, not a renumbering of the Core phases above.

| Product phase | Status on this branch | Notes |
|---|---|---|
| P1 — AI Foundation | Existing foundation / partial | Existing provider, model, ingestion and safety components; orchestrator/prompt/context governance still requires a complete exit gate |
| P2 — FVN Business Context | Authenticated context projection + fail-closed AI ports / integration partial | `GET /api/ai/business-context/me` derives actor from the server principal, loads effective permissions/scopes from `IAuthorizationService`, returns module/function metadata plus deterministic managed-scope selectors and disables caching. `POST /api/ai/business-context/ai` obtains that projection from the authenticated FVN request and forwards the exact JSON snapshot to `/api/ai/v1/business-context/snapshot` with a body-bound HMAC and replay nonce. The standalone snapshot endpoint validates signed tenant/actor and returns an acceptance response without persisting a session snapshot. Separately, the conversation API stores the validated allowlisted context projection in each user message ContextJson, linked to the conversation. A distinct session-level snapshot record and request-scoped orchestrator consumption remain. Live business tool catalog/executor adapters, request-scoped context consumption and end-to-end authorization tests remain |
| P3 — FVN Knowledge | Existing foundation / partial | Existing RAG uses ACL-filtered cosine retrieval; hybrid search and full policy lifecycle remain |
| P4 — Business Tools | Contracts + fail-closed gateway only | Context/catalog/executor ports and default-deny adapter are registered; no business tool is exposed or executable yet. Implement allow-listed typed handlers, fresh host authorization, managed-scope enforcement and provenance before enabling any tool |
| P5 — Task Assistant | Not implemented | Must use workflow-owned task APIs |
| P6 — Controlled Learning | Existing foundation / partial | Existing feedback/release pipeline; broader approved knowledge lifecycle and evaluation gates remain |
| P7 — Document Intelligence | Existing foundation / partial | Existing OCR/extraction foundation; handwriting/vision and cross-module validation remain |
| P8 — Transaction Agent / Production & Quality | Disabled foundation / not executable | Typed preview/confirmation contracts and a default-deny command gateway are registered; no write endpoint or trusted host executor is enabled. Requires fresh host authorization, confirmation UI, durable idempotency/audit and recovery tests before any write capability |

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

`001 → 002 → 003 → 004 → 005 → 006 → 007 → 008 → 009 → 010 → 011 → 012 → 013 → 014 → 015 → 016 → 017 → 018 → 019 → 020 → 021`

Migration `014_Phase5_ReleaseRetentionTenantHardening.sql` is a forward migration; do not edit already-applied migrations. It adds release provenance, tenant composite constraints, retention/dedup hardening, replay-window enforcement and queue/document state propagation.

## CI and test boundary

`.github/workflows/ai-platform-sql-validation.yml` now:

- applies migrations 001–021 twice;
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

- The signed host forwarding route uses the configured API key, tenant, actor, timestamp, nonce and body-bound HMAC signature. `POST /api/ai/business-context/ai` now forwards the host-generated permission/scope snapshot to the AI snapshot endpoint; the AI service checks the snapshot tenant/actor against the signed headers. This validates the signed transport and identity binding, but the snapshot endpoint currently only acknowledges the payload; it does not store or consume the context in an assistant conversation. It does not configure business tool discovery or execution.

- Managed-scope selectors are now normalized from the real `ManagedScopeDto` fields (`NodeType`, `NodeCode`, `FactoryCode`, `DeptCode`, `SubDepartmentCode`, `IncludeChildren`), sorted deterministically, and included in `contextVersion`. Free-text remarks and employee lists are deliberately excluded.


- Security hardening: signed request body SHA-256 is captured by middleware before Minimal API parameter binding, then validated by the HMAC endpoint filter. This avoids validating an empty/consumed body stream after JSON binding.

- Snapshot ingress is capped at 1 MiB while streaming before JSON binding (including requests without Content-Length); oversized context payloads receive HTTP 413. Other signed API bodies are hashed incrementally before binding. Document upload endpoints retain their separately configured upload limit.

## Contextual assistant request integration

- Added the authenticated FVN gateway route `POST /api/ai/business-context/ask`. It derives actor and permissions from the active FVN session; the client supplies only a question and retrieval limit.
- The gateway sends the question and current context together to `POST /api/ai/v1/assistant/ask` using the existing body-bound HMAC and replay protection.
- The AI API verifies tenant and actor against signed headers and obtains knowledge scopes freshly for every request. The context is passed separately to RAG as identity/navigation metadata, not as evidence and not as an authorization grant.
- RAG retains ACL-filtered evidence and citation validation. Tests cover context metadata separation and malformed context rejection.
- The standalone assistant request body is limited to 64 KiB while streaming and does not itself persist memory. The separate conversation API stores per-turn ContextJson and bounded history.
- This is a request/response integration slice only. Production authorization adapter configuration, signed host-to-AI integration tests, end-to-end verification and build/CI validation remain outstanding; P2 and P4 are not marked complete.
- Privacy hardening for the assistant handoff: the FVN gateway now projects only tenant/actor identifiers, department/position, role and permission/scope codes, and context version. It excludes employee code and does not forward the complete `/me` response or other personal fields to the model.
- Assistant context validation hardening: extracted `TrustedBusinessContextValidator` in the AI API. It checks signed tenant/actor equality, required context version, bounded JSON size, required code arrays, string-only bounded entries, duplicate codes, and optional department/position field types. Unit tests cover matching identity, cross-tenant/cross-actor rejection, malformed permission entries, duplicate scopes, and missing arrays. These tests are committed but have not yet been executed in CI or locally.
- Replay-protection hardening: signed AI API requests now return HTTP 503 without executing when SQL nonce persistence fails; duplicate nonce remains HTTP 401. Expired-nonce cleanup is best-effort maintenance and cannot bypass the unique `(TenantCode, Nonce)` constraint. Assistant context identity mismatches now return HTTP 403 instead of a generic validation error.
- Context privacy schema is now allowlist-only at the AI API boundary: only tenant/actor, department/position, role/permission/function/managed-scope codes, and contextVersion are accepted. Unknown fields (including employeeCode) are rejected rather than silently passed to the model. Added a regression test for employeeCode injection.
- Knowledge authorization remains deliberately fail-closed. The AI repository does not yet define a canonical mapping from FVN function/scope grants and managed organizational scopes to the `AccessScopeJson` values assigned when knowledge documents are published. Do not map `managedScopeCodes` directly or assume every function permission grants document access. The production adapter requires an explicit, reviewed FVN permission-to-knowledge-scope policy and a fresh host-side authorization check for search, publication, and unpublication.


- P8 safety foundation: added typed preview/confirmation/execution contracts and `IAuthorizedFvnBusinessCommandGateway`; DI registers a default-deny implementation. Tests assert preview and execution both fail closed when the trusted FVN adapter is absent. This does not enable transaction writes and does not make P8 production-ready.


### Runtime capability reporting

`GET /api/ai/capabilities` now reports `knowledgeAuthorizationConfigured`, `ragServiceConfigured`, `ragOperational`, `businessCommandGatewayConfigured`, and `transactionExecutionEnabled`. `ragOperational` is true only when the model provider is configured, the RAG service exists, and a non-default knowledge authorization provider is registered. `transactionExecutionEnabled` is explicitly false while the default-deny command gateway is active. The capability list describes implemented platform features; the separate readiness flags describe whether dependent authorization/execution adapters are actually configured. This endpoint is diagnostic metadata, not an authorization decision.


### Signed context snapshot semantics

`POST /api/ai/v1/business-context/snapshot` now reuses the strict business-context allowlist validator used by assistant requests, rejects unknown top-level fields, and bounds module/function metadata arrays. Its response explicitly reports `validated=true`, `persisted=false`, and `usableForAuthorization=false`: the endpoint validates a signed projection but does not store it or turn it into an authorization grant. Authorization for each RAG request continues to come from a fresh `IAuthorizedKnowledgeAccessProvider` lookup.


### Knowledge publication approval revalidation

`KnowledgeIndexingService` now re-reads the document's approved status and the matching human review decision inside the serializable publication transaction, after extraction and embedding have completed. If approval/review changed during that work, publication aborts before deactivating existing chunks or upserting replacement vectors. This narrows the stale-approval window; host-side publication authorization still must be supplied by the trusted FVN authorization adapter and is not replaced by this database check.


### HR integration readiness reporting (P4)

`GET /api/ai/capabilities` now reports `hrFactsAuthorizationConfigured`, `hrReviewAuthorizationConfigured`, `hrReconciliationConfigured`, and `hrReadOnlyToolsOperational` independently. The operational flag requires both a trusted HR facts provider and a trusted reviewer authorization provider, in addition to the reconciliation service. The default unconfigured providers remain deny-by-default; the existence of HR reconciliation, inbox, or review code is not presented as proof that live FVN HR data access is enabled. `hr-evidence-reconciliation-foundation` is deliberately named as a foundation capability until the FVN-owned read-only facts adapter and per-actor reviewer policy are integrated and tested.


### Additional P3/P4 regression hardening

- Knowledge publication status now requires the document to remain `Approved` and at least one chunk's linked human review to remain `Approved` or `Corrected`. Stale active chunks are not reported as currently published after review revocation. Regression tests cover both revoked-review and valid-approved-review cases.
- The shared HR reconciliation core now rejects undefined `HRCaseType` enum values even when invoked through the trusted-facts path. This closes a validation gap where `AnalyzeWithAuthorizedFactsAsync` bypasses the public facts-provider method. A regression test asserts invalid values fail before any HR case is persisted.


- Vector retrieval now joins the source document and requires `AiDocuments.Status = 'Approved'` in addition to tenant match, active chunk, and current approved/corrected human review. This prevents a stale active chunk from being retrieved after its source document is rejected, archived, or otherwise leaves the approved state. Publication-status queries apply the same document/review checks.


### Controlled-learning runtime readiness (P6)

`GET /api/ai/capabilities` now separates the presence of feedback evaluation, golden dataset, and release-management services from `learningAdministrationAuthorized`. `controlledLearningOperational` requires all four conditions. The default administration authorization provider is deny-by-default, so evaluation code being registered does not imply that users can administer/promote model releases. The endpoint reports readiness only; it does not grant release permissions.


### Document intelligence runtime readiness (P7)

`GET /api/ai/capabilities` now reports document pipeline registration, text extraction registration, OCR configuration presence, malware scanner adapter readiness, and a combined `documentIntelligenceOperational` flag. The combined flag requires the pipeline, text extractor, and a non-default malware scanner. `ocrConfigurationPresent` deliberately means only that executable names and a bounded DPI are configured; it does not claim that Tesseract/Poppler binaries or language packs are installed and executable. Production rollout still requires an environment-level OCR smoke test and malware-scanner connectivity test.


### P2 conversation persistence and bounded turn context

- Added tenant- and actor-owned conversation/message records and SQL migration script `SQL/AI/016_ConversationPersistence.sql`.
- Added signed AI API operations to create a conversation, list only the current actor's active conversations, retrieve only an owned transcript, and submit a question to an owned conversation.
- Every user turn stores the strict, validated business-context projection used for that turn. EmployeeCode and unknown fields remain excluded by the shared validator.
- Prior turns are loaded only from the same tenant, actor, and conversation; at most eight completed turns are passed as a separate `conversationHistory` property. Current RAG evidence remains a distinct property and only current-question search results can be cited. Conversation history is explicitly treated as untrusted context, never as evidence or instructions.
- Message sequence allocation is serialized per append and protected by a unique tenant/conversation/sequence index. Conversations expire after 90 days; the migration includes a bounded purge procedure invoked by ConversationMaintenanceWorker every 5 minutes. The API hides expired conversations even before physical purge.
- This provides persistence and bounded context inside the signed AI API. The authenticated FVN API proxy now has matching conversation lifecycle routes; the Blazor chat, data-entry and HR workspaces are wired; a universal AI document-review queue and E2E verification remain. SQL migrations 001–021 must be applied before enabling these endpoints.


- Follow-up: the FVN authenticated API controller now exposes conversation create/list/transcript/ask proxy routes. Each upstream request is signed with the current session's actor ID; browser-supplied actor IDs are not accepted. The proxy constructs the strict business-context projection from the current FVN authorization snapshot. The conversation prompt is limited to the latest eight turns, each truncated to 2,500 characters for the AI request-size budget. UI pages are now wired for chat, data-entry and HR; applying SQL migrations 001–021 and completing E2E verification remain rollout tasks.

- User turns are recorded as `Pending` before model execution, marked `Completed` only when the assistant outcome is `Supported`, `PartiallySupported` or `NotFound`, and marked `Abstained` for `Forbidden`, `InvalidModelResponse` or other non-reusable outcomes. Model exceptions mark turns `Failed`. Only completed user turns and the allowed assistant statuses are reused as history. Conversation reads and lists use no-store cache headers.


### Conversation and migration review corrections (2026-10-20)

- Corrected `AnswerWithConversationContextAsync` argument order: retrieval limit and cancellation token now precede the optional serialized history argument passed to `AnswerCoreAsync`.
- Corrected the history validation error text to match the 24,000-character bound.
- Conversation history includes only completed user turns and assistant outcomes `Supported`, `PartiallySupported`, or `NotFound`. `Forbidden`, `InvalidModelResponse`, and other abstentions remain visible in the transcript with their status but are excluded from future model context.
- Migration runner now applies 001–021 and records SHA-256 checksums for 015–021. SQL integration workflow applies the full set twice and asserts data-entry tables, conversation tables, purge/recovery procedures, ownership FK, queue claim serialization, dead-letter procedure SET options, and migration checksums. Migration 017/019 correct dead-letter document status transactionally without leaving a trigger on the queue table (the claim procedure uses OUTPUT); migration 018 marks abandoned Pending user turns Failed after a bounded age.
- Documentation index now links only to files that exist. Operations, architecture and integration documentation were aligned to migration 019.
- Data-entry FVN proxy authorization was changed from the generic system-critical `SecurityView` permission to dedicated function codes: View (3501), TemplateManage (3502), RunCreate (3503), Confirm (3504), Export (3505). Each host route checks the matching permission before forwarding.
- Conversation message sequence allocation now uses a transaction-owned SQL application lock with a bounded 10-second wait and ReadCommitted transaction instead of Serializable range locking plus MAX+1 alone. The unique sequence index remains the final integrity guard.

**Verification update (2026-10-21):** Historical SQL CI run 39523741526 passed at commit `5127b3ec`: migrations 001–021 applied twice with checksum verification; duplicate-ordinal guard passed; 12 concurrent queue claimers obtained 12 distinct jobs; nonce replay produced exactly 1 accepted and 11 replay results; dead-letter propagated to document status; conversation recovery and expiry purge passed; tenant/dedup checks passed; AI unit tests passed **89/89**. This SQL workflow is green. The full solution build on the latest branch HEAD is still pending and must be checked separately; no production build claim is made yet.

**Outstanding risks:** conversation `Content` and per-turn `ContextJson` remain plaintext SQL columns and need a reviewed encryption-at-rest/application-key design before sensitive conversations are enabled in production. Conversation API endpoint-level integration/E2E tests are still outstanding. The trusted FVN knowledge authorization adapter and HR facts adapter remain fail-closed until the approved FVN permission-to-knowledge-scope mapping and authoritative HR source contracts are supplied. OCR and malware scanning still require environment smoke tests. A universal AI document-review queue is not yet implemented.
### Integration implementation update (2026-10-21)

- Added `/ai/assistant` Blazor conversation workspace and role-gated navigation. New permission `AIAssistant.Chat` (3510) is checked server-side by the FVN conversation proxy; browser requests never supply an actor ID.
- Added role-gated navigation for the existing AI data-entry (3501) and HR assistant workspaces. The data-entry UI checks 3501–3505 separately and hides template management unless 3502 is granted.
- Added FVN SQL deployment script `SQL/110_AIDataEntryPermissions.sql` to both main deployment entrypoints. Roles 1–5 receive assistant chat and own-run capabilities; template management (3502) is limited to roles 1–2; Guest receives none. Chat permission 3510 does not grant knowledge access.
- Data-entry run retrieval, confirmation and export now require `CreatedBy == currentActor` in addition to tenant ownership. This prevents one authorized user from reading or confirming another user's run.
- Conversation history eligibility is status-aware and remains scoped by tenant, actor and conversation. The status predicate is grouped inside the tenant/actor filter to prevent OR-precedence from bypassing those isolation predicates.
- Added regression tests for conversation abstention policy; `Forbidden` and `InvalidModelResponse` are not replayed into later prompts.
- Document processing tests exposed and fixed a phantom page 1 when a PDF starts with `[PAGE n]`, strict UTF-8 validation that previously accepted malformed input through BOM detection, and text extraction that skipped bytes when the caller stream position was non-zero. XLSX test ZIP entries are now disposed before the next entry is created.
- Queue claim concurrency is protected by migration 020; migration 021 fixes the stored procedure `ANSI_NULLS`/`QUOTED_IDENTIFIER` metadata required for filtered-index updates. SQL CI proves both behaviors.

## RAG / Conversation hardening update (2026-10-21)

- Conversation history JSON now uses UnsafeRelaxedJsonEscaping so Vietnamese/Japanese text is not expanded into six-character Unicode escape sequences before the 24,000-character history guard.
- RAG prompt JSON uses the same UTF-8-friendly encoder. Retrieved evidence is bounded to 9,000 characters before model invocation; the Ollama output allowance is 1,200 tokens with the existing 8,192-token context window. This is a conservative character budget, not an exact model-tokenizer measurement; a provider tokenizer-aware budget remains a follow-up.
- MinimumScore now filters the raw cosine similarity before the cosine-to-[0,1] normalization and lexical reranking.
- SQL vector candidate materialization is capped at 5,000 ACL-filtered rows per request before application cosine scoring.
- FVN permission seed 3510 now carries ModuleCode = AIAssistant; 3501–3505 remain in AIDocumentDataEntry.
- Added regression tests for long Vietnamese conversation history and aggregate evidence character bounds.
- Integration/architecture/operations documents now reference migrations 001–021; the operations citation artifact was corrected. Architecture wording now distinguishes lightweight lexical reranking from full BM25.
- Validation boundary: these changes are committed through fc1932be8e436b2d64409d09068ace7864b3eb24. CI has not yet reported a result for this HEAD. The earlier 89/89 unit-test result and SQL CI result at 5127b3ec are historical and must not be presented as validation of this updated HEAD.
- Still outstanding from the review: actor-level AI rate limiting, capability-aware chat navigation, authentication/network restriction for readiness and capabilities endpoints, conversation archive/delete APIs, proxy error normalization, chat UI localization, citation entailment checks, Japanese/Vietnamese retrieval benchmark, tokenizer-aware prompt budgeting, session-level context snapshot/hash design and encryption-at-rest.
