# FVN_REGISTER AI Platform — Operations

## Scope

This is the current operational guide for `feature/ai-platform-phase1-6`. The canonical phase status is the root `AI_PLATFORM_PHASES_STATUS.md`; do not use an older phase-status copy under `docs/`.

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

`001 → 002 → 003 → 004 → 005 → 006 → 007 → 008 → 009 → 010 → 011 → 012 → 013 → 014 → 015 → 016 → 017`

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

Use `scripts/apply-ai-migrations.sh`. It applies all 17 migrations and verifies recorded checksums before executing them.

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

The current store uses tenant/ACL-filtered SQL candidate loading and application cosine scoring. It is intentionally a PoC/medium-corpus baseline. Candidate scan limit and minimum score are configuration points, but benchmark evidence is still required before production scale.

SQL Server 2025 provides native `VECTOR`, `VECTOR_DISTANCE` and `VECTOR_SEARCH`; an eventual migration can use server-side exact kNN or an approximate vector index after compatibility and recall benchmarks. urlSQL Server vector search documentationhttps://learn.microsoft.com/en-us/sql/sql-server/ai/vectors?view=sql-server-ver17

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

`.github/workflows/ai-platform-sql-validation.yml` runs SQL Server 2022 integration checks, applies migrations 001–017 twice, verifies checksums and constraints, tests duplicate legacy ordinal failure, concurrent lease claims, nonce replay, dead-letter document state, tenant-scoped deduplication, and the AI unit-test project.
