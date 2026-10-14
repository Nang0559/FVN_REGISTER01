# FVN_REGISTER AI Platform — Implementation plan

## First end-to-end slice

1. Authenticated PDF/XLSX/CSV/TXT upload to quarantine.
2. SHA-256, MIME/signature validation and document metadata.
3. Durable queue/outbox with idempotency key.
4. Worker extraction through provider adapters.
5. Versioned document classification and extraction schema.
6. Candidate fields + provenance + confidence + validation issues persisted separately.
7. Blazor review screen: original beside extracted fields/source locations.
8. Accept/reject/correct with reviewer and model/parser/prompt versions.
9. Explicit approved command to FVN_REGISTER; FVN re-authorizes and validates before writing.

## Decisions before provider enablement

- Approved local runtime, model family and compute capacity.
- Approved hosted provider, data terms, region, egress allowlist and allowed data classes.
- SQL Server version and vector capability; otherwise choose an approved vector service.
- Object storage and immutable key/backup/retention policy.
- Queue choice: SQL outbox + SQL-backed queue initially, or approved RabbitMQ/Azure Service Bus.
- Service-to-service authentication and user-delegation/audit claims.
- Golden evaluation corpus covering Vietnamese/Japanese forms, scans, spreadsheets and expected outputs.

## Non-functional requirements

- Idempotent ingestion, retry/backoff and dead-letter handling.
- Per-tenant/department authorization on every operation.
- No raw document text or secrets in normal logs.
- Configurable upload limits, quotas and retention.
- Metrics for queue depth, processing duration, provider latency/cost, failures and human-review rate.
- Parser/model/prompt/schema versions attached to results.
- Golden-set evaluation before rollout.
- Independent deploy, monitor, disable and rollback without interrupting FVN_REGISTER.

## Production definition of done

Threat model/data classification approved; authentication tests pass; malware and file-signature checks enabled; queue retry/dead-letter verified; low-confidence and business-impacting outputs require review; RAG filters ACL before retrieval; answers cite evidence; provider outage does not break FVN_REGISTER; deletion and restore tested.
