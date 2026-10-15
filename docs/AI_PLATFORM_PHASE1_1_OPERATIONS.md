# FVN AI Platform – Phase 1.1 Operations & Security

## Scope

Phase 1.1 hardens the Phase 1 upload boundary and SQL queue primitives. It adds:
- signed tenant/actor request context with HMAC-SHA256, five-minute clock tolerance and in-process nonce replay rejection;
- atomic SQL job claim procedure using `UPDLOCK`, `READPAST`, `ROWLOCK`;
- worker lease expiry recovery, bounded attempts, exponential retry delay and dead-letter transition procedures.

This is a security/reliability foundation, not a declaration that the complete malware-scanning pipeline is production-ready. The worker must not claim a document for parsing until an approved malware scanner adapter is configured and wired into the worker. Documents remain `Quarantined` until that gate passes.

## 1. Database migration

Run the following against the dedicated `FVN_AI` database, after Phase 1 migration:

```sql
:r .\SQL\AI\002_ReliableIngestionQueue.sql
```

If SQLCMD mode is unavailable, open and execute the migration file directly in SSMS. Do not run it against the FVN_REGISTER business database.

New job fields:
- `LeaseOwner`, `LeaseUntilUtc`: ownership and expiry of an in-flight job.
- `NextAttemptAtUtc`, `LastAttemptAtUtc`: scheduling and diagnostics.
- `MaxAttempts`, `LastErrorAtUtc`: retry budget and failure timestamp.

Stored procedures:
- `ai.usp_RecoverExpiredAiIngestionLeases`: recover expired `Running` jobs; exhausted jobs become `DeadLetter`.
- `ai.usp_ClaimAiIngestionJob`: atomically claims one eligible job and increments attempt count.
- `ai.usp_RenewAiIngestionLease`: extends a still-valid lease owned by the calling worker.
- `ai.usp_CompleteAiIngestionJob`: completes only when the supplied worker owns the active lease.
- `ai.usp_FailAiIngestionJob`: retry with exponential delay or move to `DeadLetter`.

The claim procedure is safe for multiple workers: competing workers skip locked rows and cannot claim the same job. A lease must be renewed before expiry for long-running work; a worker that loses its lease must stop and must not commit results.

## 2. Signed FVN_REGISTER service identity

Every request to `/api/ai/v1/**` must provide:
- `X-AI-API-Key`: service API key.
- `X-Tenant-Code`: trusted tenant code.
- `X-Actor-Id`: authenticated FVN_REGISTER actor identifier.
- `X-AI-Timestamp`: Unix UTC seconds.
- `X-AI-Nonce`: cryptographically random value, at least 16 characters.
- `X-AI-Signature`: lowercase or uppercase hex HMAC-SHA256.

Canonical message (UTF-8, newline-separated, no trailing newline):

```text
{HTTP_METHOD_UPPER}
{PATH}
{TENANT_CODE}
{ACTOR_ID}
{UNIX_TIMESTAMP}
{NONCE}
```

Signature:

```text
hex(HMACSHA256(UTF8(ContextSigningKey), UTF8(canonical_message)))
```

The timestamp must be within five minutes of the AI API clock. A nonce cannot be reused within the in-memory replay window. The API key and context signing key are separate secrets. Configure both through environment variables or a secret manager; do not commit real secrets to appsettings or source control.

Example environment variables for ASP.NET Core configuration:

```powershell
$env:AIPlatform__ApiKey = "<random-service-api-key>"
$env:AIPlatform__ContextSigningKey = "<at-least-32-random-characters>"
```

**Important:** the API endpoint is for trusted backend-to-backend calls only. Do not call it directly from Blazor/browser/mobile clients. FVN_REGISTER must authenticate the user, authorize the action using its existing RBAC/tenant scope, then sign the resulting tenant and actor context. The AI API signature proves the caller supplied an untampered context; it does not independently replace FVN_REGISTER authorization.

The current replay cache is process-local. For a multi-instance API deployment, replace it with a shared atomic nonce store (for example Redis or a SQL table with a unique key and expiry) before exposing multiple API replicas.

## 3. Worker and queue operation

The Worker runs lease recovery, claims jobs, renews leases, invokes the document pipeline, and completes/fails jobs using the stored procedures. The pipeline verifies the lease, scans the original, extracts text/OCR, classifies the document and optionally runs schema-driven extraction/validation. Do not mark jobs completed merely because a file was uploaded or claimed.

Before enabling job execution, implement and register:
1. An antivirus adapter for the organization's approved engine (for example, a managed ICAP/ClamAV gateway or endpoint protection scanning service).
2. A fail-closed policy: scanner unavailable, timeout, malformed response or unknown verdict must never release a file.
3. The registered document pipeline, which opens the tenant-scoped original, runs the configured malware scanner, extracts text/OCR and persists candidates only after a clean verdict.
4. Lease renewal for long-running scans/extractions; use worker identity for complete/fail calls.
5. Dead-letter review/replay operations restricted to AI platform administrators.

Never move a document from `Quarantined` to `ReadyForProcessing` or `AwaitingHumanReview` without a positive clean verdict and a successful processing result.

## 4. Retry policy

The SQL failure procedure applies exponential backoff (15 seconds × 2^attempt, capped at one hour) and moves non-retryable or exhausted jobs to `DeadLetter`. Use stable machine-readable failure codes, not raw exception messages. Avoid storing file contents, secrets, or personal data in logs.

## 5. Production gates still required

- Shared durable storage (or object-storage adapter) with encryption at rest, backup, retention and access audit.
- Shared nonce replay store for multiple API replicas.
- Organization-approved antivirus adapter and worker handler; quarantine release must fail closed.
- FVN_REGISTER signing implementation and secret rotation procedure.
- SQL integration tests covering concurrent claim, lease expiry, retry budget, dead-letter and ownership checks.
- API integration tests covering missing/invalid signatures, expired timestamp, nonce replay, tenant isolation and file-size limits.
- Build and test on the target .NET 9 / SQL Server environment.

Until these gates pass, deploy only to a controlled test environment. The Phase 1.1 SQL procedures and API signature check are implemented, but production readiness has not been established by a build, security test or deployment.


## 6. Phase 2–5 implementation now present on this branch

- Local extraction supports TXT/CSV, XLSX, DOCX and text-layer PDF. PNG/JPEG and scanned PDF use the Tesseract + Poppler OCR adapter; binaries and language packs must be installed and verified on each processing host.
- Schema registry, schema-driven extraction, deterministic validation, validation issue persistence, knowledge chunks, vector retrieval, feedback and review persistence are defined in AI SQL migrations 003–006.
- AI API exposes schema draft/publish/read, knowledge publish/search/unpublish, RAG answer and feedback evaluation routes behind the signed backend request filter. FVN_REGISTER gateway authorization and ACL-scope derivation remain mandatory before production exposure.
- The default malware scanner returns `Unknown`. It never reports a clean verdict when no approved scanning engine is configured.
- The Ollama adapter supports chat, JSON-mode structured extraction/classification/RAG and a separate embedding model.
- CI workflow validates the solution and AI test project on Windows/.NET 9 when GitHub Actions runs.

These are persistence and integration foundations. They do not yet constitute a running OCR/LLM extraction pipeline, an ACL-filtered vector retrieval service, a Blazor review UI, or a production model registry. Those require approved provider and deployment choices.
