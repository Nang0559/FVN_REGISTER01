# FVN_REGISTER AI Platform — Operations

## Scope

This is the current operational guide for `feature/ai-platform-phase1-6-integration`. The canonical phase status is the root `AI_PLATFORM_PHASES_STATUS.md`; do not use an older phase-status copy under `docs/`.

## 1. Topology

```text
FVN_REGISTER -> signed AI API -> SQL metadata/queue + quarantine storage
                                      |
                                      v
                                  AI Worker
                                      |
                    AV -> extraction -> validation -> human review
                                      |
                          approved/corrected knowledge
                                      |
                           ACL + tenant filtered RAG
```

## 2. Configuration and secrets

Required production settings:

- `ConnectionStrings:FvnAi`
- `AIPlatform:ApiKey`
- `AIPlatform:ContextSigningKey` (minimum 32 bytes/characters for HMAC validation)
- `AIPlatform:RequireContentBoundSignatures=true` — startup fails if disabled.
- Provider endpoint/model and embedding model according to the approved provider.
- ClamAV/Tesseract/Poppler runtime dependencies when enabled.

Secrets must come from the deployment secret store/environment, never from committed `appsettings*.json`.

## 3. Request integrity and replay

Requests use HMAC identity signatures. With content-bound signatures enabled, method/path/query/body are included in the signed material, protecting multipart `moduleCode`/`schemaCode` and review `CorrectedPayloadJson` from tampering.

The timestamp acceptance window is ±5 minutes. Migration `014` installs a SQL trigger that guarantees a nonce expiry of at least 10 minutes, closing the future-timestamp replay gap.

## 4. Migration order

For an empty database use exactly this sequence:

`001 → 002 → 003 → 004 → 005 → 006 → 007 → 008 → 009 → 010 → 011 → 012 → 013 → 014 → 015 → 016 → 017 → 018 → 019 → 020 → 021`

Migration `014_Phase5_ReleaseRetentionTenantHardening.sql` is the forward migration for:

- release-bound extraction/feedback provenance;
- model-aware extraction uniqueness, including a guard for `SchemaVersion IS NULL`;
- tenant columns and composite FKs for validation/golden evaluations;
- purge/re-upload dedup lifecycle;
- ten-minute replay retention;
- document state propagation for lease/dead-letter;
- nullable purgeable knowledge/extraction payloads.

Never edit an already-applied migration to change its historical behavior. The migration runner records filename and SHA-256 where verification is supported; legacy 001–009 records can remain NULL when their historical checksum cannot be reconstructed.

## 5. Apply and rollback

Use `scripts/apply-ai-migrations.sh`. It applies all 21 migrations and verifies recorded checksums before executing them.

Rollback is forward-only at the application/schema level: restore from a tested SQL backup for destructive rollback, or create a new compensating migration. Do not delete rows from `AiSchemaMigrations` to bypass the ledger.

Before any production migration:

1. take a full SQL backup;
2. verify backup restore on a non-production instance;
3. stop or drain ingestion workers where the migration changes queue semantics;
4. apply migrations;
5. run the SQL validation workflow and smoke checks;
6. resume workers only after the database is healthy.

## 6. Retention

`ArchiveExpiredAsync` now coordinates original storage, document state, extraction payloads and knowledge chunks. Expired `Rejected` documents are included. Knowledge chunks are marked inactive and their `Content`/`EmbeddingJson` cleared. `AiExtractions.CandidateJson` and `RepresentationJson` are cleared. Archived documents set `DedupActive=false`, allowing an identical file to be uploaded again.

## 7. Queue and failure handling

A claimed job moves its document to `Processing`. Retryable failures return the document to `Quarantined`; terminal dead-letter moves it to `Failed`. Lease recovery follows the same rule. Re-uploading an identical fingerprint requeues an existing `DeadLetter`/`Failed` job when its retained original still exists.

## 8. RAG and scale boundary

The current store uses tenant/ACL-filtered SQL candidate loading and application cosine scoring. It is intentionally a PoC/medium-corpus baseline. Candidate materialization is capped at 5,000 ACL-filtered rows per request. `MinimumScore` is applied to raw cosine similarity before semantic/lexical reranking. This remains a bounded PoC baseline; benchmark evidence is still required before production scale.

SQL Server 2025 provides native `VECTOR`, `VECTOR_DISTANCE` and `VECTOR_SEARCH`; an eventual migration can use server-side exact kNN or an approximate vector index after compatibility and recall benchmarks. [SQL Server vector search documentation](https://learn.microsoft.com/en-us/sql/sql-server/ai/vectors?view=sql-server-ver17)

## 9. Monitoring

At minimum collect:

- queue depth, oldest queued job and dead-letter count;
- lease expiry/recovery count;
- extraction duration, validation error rate and human-review latency;
- provider/model/prompt release usage and failure rate;
- embedding call count/latency and chunk count;
- RAG candidate scan count, score distribution, latency and abstention rate;
- retention purge count and failed purge operations;
- release-gate decision count, acceptance rate and golden pass rate.

Never log document contents, embeddings, HMAC secrets, API keys or sensitive HR payloads.

## 10. Cost controls

Track embedding and LLM calls by tenant/module/release where allowed. Chunk overlap increases embedding volume; bounded concurrency reduces wall-clock time but does not reduce provider cost. Set provider timeouts, maximum input size, chunk limits and candidate scan limits. Any external provider requires explicit data-classification/privacy approval.

## 11. Backup/restore

Back up:

- AI SQL database;
- original/quarantine storage;
- deployment configuration/secret references;
- migration ledger.

A restore is not complete until SQL, original storage and their document SHA-256/storage keys are mutually consistent. Knowledge chunks must be rebuildable from retained approved sources when embeddings are not restored.

## 12. Tenant isolation release gate

CI verifies tenant predicates in the RAG retrieval query, tenant references in reviewed AI stored procedures, tenant-scoped duplicate behavior, and composite tenant FKs. These checks are not proof that every future EF/LINQ endpoint is tenant-safe. Endpoint-by-endpoint tenant/ACL review remains a release gate. SQL Server Row-Level Security is not enabled by this migration set.

## 13. CI

`.github/workflows/ai-platform-sql-validation.yml` runs SQL Server 2022 integration checks, applies migrations 001–021 twice, verifies checksums and constraints, tests duplicate legacy ordinal failure, 12 concurrent queue claims, nonce replay, dead-letter document state, conversation recovery/purge, tenant-scoped deduplication, and the AI unit-test project.

## 14. Conversation maintenance and queue claim hardening

- `ConversationMaintenanceWorker` is hosted by `FVN_REGISTER.AI.Worker` and runs `ai.usp_RecoverStaleConversationTurns` plus `ai.usp_PurgeExpiredConversations` every 5 minutes by default. Override with `AIPlatform:ConversationMaintenancePollMinutes` (bounded to 1–60 minutes). The AI Worker must be running and migrations 016/018 must be applied.
- Migration 020 serializes only the short queue-claim transaction with a transaction-owned SQL application lock. Document extraction/validation remains parallel after claim. SQL CI verifies 12 simultaneous callers claim 12 distinct jobs without deadlock.
- Migration 021 recreates `ai.usp_FailAiIngestionJob` with `ANSI_NULLS ON` and `QUOTED_IDENTIFIER ON`, and keeps queue/document dead-letter transitions in one transaction. SQL CI verifies both procedure SET options and that terminal failure marks the source document `Failed`.
- Conversation messages retain status-specific outcomes. Only successful/usable assistant statuses (`Supported`, `PartiallySupported`, `NotFound`) and their completed user turns are reused as prompt history; `Forbidden`, `InvalidModelResponse` and other abstentions are not reused.

## 15. FVN application permission seed

`SQL/110_AIDataEntryPermissions.sql` is a separate FVN_REGISTER database deployment script, not an `FVN_AI` migration. Include it through `SQL/00_Deploy_All.sql` or `SQL/00_Deploy_Run.sql` after the security registry schema is available. It seeds:

- 3501 View, 3502 TemplateManage, 3503 RunCreate, 3504 Confirm, 3505 Export;
- 3510 AIAssistant.Chat, which grants access to the chat surface but does **not** grant access to protected knowledge.

Roles 1–5 receive the chat and own-run capabilities; only roles 1–2 receive template management. Guest role 6 receives none. Data-entry run read/confirm/export also checks `CreatedBy` against the current actor. Review the role matrix before production rollout if the company role policy differs.

## Local development configuration

Do not commit machine-specific paths or secrets. `AIPlatform:StorageRoot` is intentionally empty in the committed appsettings and must be an absolute path shared by `FVN_REGISTER.AI.Api` and `FVN_REGISTER.AI.Worker`. Set it, and the SQL connection string, for **both** projects with user-secrets (or `AIPlatform__StorageRoot` / `ConnectionStrings__FvnAi` environment variables):

```powershell
cd FVN_REGISTER.AI.Api
dotnet user-secrets set "AIPlatform:StorageRoot" "D:\FvnAi\OriginalDocuments"
cd ..\FVN_REGISTER.AI.Worker
dotnet user-secrets set "AIPlatform:StorageRoot" "D:\FvnAi\OriginalDocuments"
dotnet user-secrets set "ConnectionStrings:FvnAi" "<same FVN_AI connection string as the API>"
```

Ingestion also requires `AIPlatform:MalwareScanner:Provider=ClamAV` with a reachable clamd (or, on Windows dev/pilot machines only, `Provider=WindowsDefender`, which scans a temp copy with `MpCmdRun.exe`; optional `DefenderExecutable` overrides auto-detection; failure codes `DEFENDER_UNAVAILABLE`, `DEFENDER_TIMEOUT`, `DEFENDER_SCAN_ERROR`), and Tesseract/pdftoppm on PATH for images and scanned PDFs. Worker failure codes: `ORIGINAL_BLOB_MISSING` (storage root differs between Api and Worker), `OCR_TOOL_UNAVAILABLE` (OCR executable missing), `MALWARE_SCANNER_NOT_CONFIGURED`.

## 16. Knowledge authorization (FVN RBAC adapter)

`FvnKnowledgeAccessProvider` replaces the fail-closed default whenever a SQL connection is available. It reads the FVN security tables (`dbo.F03Users`, `F03UserRoles`, `F03Roles`, `F03RoleFunctions`, `F03Functions`, `F03Employees`) with the same activity rules as `AuthorizationService`, using the actor from the HMAC-validated gateway header.

| Operation | Rule | Result |
|---|---|---|
| Search | Active user holding `AIAssistant.Chat` (3510); an `Own` grant counts only for an active HRM employee | `fvn:staff`, `fvn:role:{RoleCode}` per active role, `fvn:dept:{DeptCode}` |
| Publish | Active user holding the publish function with data scope `All` | `fvn:staff` |
| Unpublish | Same as publish | allow or HTTP 403 |

Configuration (`AIPlatform:KnowledgeAuthorization`): `ConnectionStringName` (default `FvnRegister`, falls back to `FvnAi`), `ChatFunctionCode` (3510), `PublishFunctionCode` (3502, `AIDocumentDataEntry.TemplateManage`), `StaffScope` (`fvn:staff`). If the AI schema is in a different database from FVN_REGISTER, set `ConnectionStrings:FvnRegister` for the AI API. SQL failures return HTTP 503 (fail closed).

## 17. Protected diagnostics endpoints

`/health` remains anonymous (liveness only). `/health/ready` and `/api/ai/capabilities` require the `X-AI-API-Key` header; FVN_REGISTER.API sends it server-side. `GET api/ai/assistant/availability` (authenticated, cached 30 s) returns one boolean that the UI uses to hide the "Trợ lý AI" menu when `ragOperational` is false.
