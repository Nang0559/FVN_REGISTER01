# FVN_REGISTER AI Platform — P1–P8 Architecture Roadmap

**Status:** Architecture target and implementation sequence  
**Baseline branch:** `feature/ai-platform-phase1-6-integration`  
**Runtime:** .NET 9 / SQL Server / hybrid on-premises-first

This roadmap is the product/platform sequence. It is deliberately distinct from the earlier AI Core phase labels used for ingestion, document understanding, extraction, RAG and learning. Existing implementation status remains in the root `AI_PLATFORM_PHASES_STATUS.md`.

## Governing principle

> AI capability may improve over time; AI authority must not increase merely because the model becomes more capable.

Qwen (or another provider) interprets language, reasons over bounded context and proposes tool calls. FVN_REGISTER remains authoritative for identity, permissions, managed scope, policy, business facts, workflow transitions and writes. The model never receives database credentials, arbitrary SQL access or unrestricted HTTP execution.

## Target sequence

| Product phase | Capability | Required boundary / exit gate |
|---|---|---|
| P1 — AI Foundation | Orchestrator, provider/model registry, prompt versions, conversation context, structured responses, safety | Provider-neutral contracts; bounded context; traceable model/prompt versions; fail-closed configuration |
| P2 — FVN Business Context | Module/function/action catalog, user/employee/org context, roles, permissions, scopes, policies and business vocabulary | Metadata discoverable without bulk database exposure; trusted short-lived context; host re-authorizes every invocation |
| P3 — FVN Knowledge | Ingestion, versioned documents, chunking, embeddings, hybrid retrieval, effective dates, provenance and citations | Tenant/ACL filtering before retrieval; source/version/page/section citations; supersession and expiry |
| P4 — Business Tools | Read-only employee/org/calendar/OT/leave/equipment/execution tools | Allow-listed typed tools; host-side authorization and managed-scope checks on every call; source provenance |
| P5 — Task Assistant | My Tasks, deadline/priority explanation, missing evidence and recommendations | Tasks from workflow APIs; AI recommends only; workflow owns status and assignment |
| P6 — Controlled Learning | Feedback, candidate knowledge, human review, release/evaluation, supersession and rollback | Feedback is not automatically policy; approved/versioned release only; regression evaluation and rollback |
| P7 — Document Intelligence | OCR, vision, handwriting, classification, extraction, validation, confidence and human correction | No direct business posting; extraction remains a candidate until human/domain validation |
| P8 — Transaction Agent and Production/Quality | Preview, explicit confirmation, authorized commands, audit; production/QC/inventory knowledge and analysis | Fresh authorization + domain policy + confirmation + idempotent command + audit; enable incrementally |

P7 and P8 are capability tracks that reuse P1–P6. Transaction execution must not be enabled merely because document extraction exists.

### P8 transaction command boundary (implemented as a disabled foundation)

The AI application now defines typed `FvnBusinessCommandPreviewRequest`, `FvnBusinessCommandPreview`, `FvnBusinessCommandConfirmation` and `FvnBusinessCommandExecutionResult` contracts, plus `IAuthorizedFvnBusinessCommandGateway`. The infrastructure registers `UnconfiguredFvnBusinessCommandGateway` as the default-deny implementation. Both preview and confirmed execution throw `FVN_BUSINESS_COMMAND_GATEWAY_NOT_CONFIGURED` until a trusted FVN host adapter is explicitly registered. Unit tests cover both fail-closed paths.

This is deliberately **not** an executable transaction agent. No AI endpoint exposes a write command, no model can produce a valid human confirmation, and no transaction is executed by this default implementation. A production host adapter must:
- resolve tenant/actor from the authenticated FVN principal, never from model arguments;
- perform fresh permission, managed-scope and domain-policy checks on preview and again at commit;
- bind an opaque, short-lived preview token to a canonical command hash, tenant, actor and exact change set;
- require confirmation from the trusted FVN UI, not a model-generated flag;
- enforce unique idempotency keys and single-use preview tokens transactionally;
- write immutable audit records with before/after references, actor, command version, correlation and outcome;
- provide recovery/reconciliation for uncertain commit outcomes and tests for replay, expiry, scope change and concurrent retries.

The current state-changing boundary therefore remains disabled. Do not advertise P8 as complete until the host adapter, durable idempotency/audit store, confirmation UI and end-to-end tests are deployed.


## P2 — FVN Business Context design

```text
Authenticated FVN_REGISTER session
       |
       v
FVN Business Gateway
       +--> User / Employee / Department / Position
       +--> Role / Permission / Managed Scope
       +--> Module / Function / Action catalog
       +--> Approval Policy / Work Calendar metadata
       |
       v
Short-lived authorized context + allow-listed tool catalog
       |
       v
AI Orchestrator --> Qwen
```

Three data classes must remain separate:

1. **Business metadata:** module/function/action definitions, descriptions, state vocabulary and policy identifiers. Safe to index as bounded metadata.
2. **User context:** authenticated actor, employee, organization, roles, permissions and managed scopes. Resolve per request/session; never trust values supplied in a prompt.
3. **Business facts:** actual OT, leave, attendance, calendar, equipment and execution records. Retrieve only through purpose-built FVN_REGISTER tools filtered by the current caller.

The initial application contracts are in:
- `FVN_REGISTER.AI.Application/Contracts/FvnBusinessContextContracts.cs`
- `FVN_REGISTER.AI.Application/Abstractions/FvnBusinessPorts.cs`

These are integration ports, not evidence that a production host adapter is already wired. The AI service now registers a single fail-closed default for context lookup, tool catalog and execution; missing host integration returns `FVN_BUSINESS_GATEWAY_NOT_CONFIGURED`. The FVN host exposes an authenticated context projection grouped by module/function metadata, emits deterministic managed-scope selectors (excluding remarks and employee lists), disables response caching, and provides a signed `GET /api/ai/business-context/ai` forwarding route. The host exposes `POST /api/ai/business-context/ai`, which builds the projection from the authenticated FVN session and forwards the exact snapshot to the AI service using a content-bound HMAC signature and replay nonce. The AI `/api/ai/v1/business-context/snapshot` endpoint verifies that signed tenant/actor identity matches the body and returns an acceptance response. It currently does not persist the snapshot or attach it to a conversation/orchestrator, so this is transport/identity verification only—not completion of operational P2 context consumption. Request-scoped conversation integration, the production business tool catalog/executor adapter and end-to-end authorization tests remain pending.

## Tool execution contract

For a question such as “Can E0001 approve OT on 20/10?” the model must not infer authority from title or role labels. The host resolves current employee/department/position, permission, managed scope, approval policy, registration and calendar facts. The tool response returns authorized facts and provenance. The model may explain the result; it cannot override it.

Every invocation must bind tenant and actor from trusted gateway context; validate tool code and input against a registered schema; re-check permission and managed scope at execution time; apply current business policy and workflow preconditions; return structured results with source provenance; audit actor, tool/version, input hash, decision, result and correlation ID; and require explicit human confirmation for any command that changes business state.

No generic `ExecuteSql`, unrestricted `HttpRequest`, reflection-based method dispatch or model-selected service type is allowed.

## Relationship to existing AI Core phases

The existing Core implementation (ingestion, document understanding, extraction, RAG, learning and HR pilot) is reusable infrastructure. Its historical phase numbers are retained in the root status file. Do not renumber those implementation records to match P1–P8. P1–P8 describes the target product architecture and dependency order.

## Delivery gates

- P2 is not complete until the real host adapter is wired, integration tests prove caller isolation and scope filtering, and unauthorized tool access is denied.
- P3 is not complete until effective-date/supersession lifecycle, hybrid retrieval benchmark, citation correctness and authorization red-team tests pass.
- P4 is not complete until each tool has contract and host integration tests for role, permission, department and managed-scope boundaries.
- P7 is not complete until OCR/extraction confidence and human correction are persisted with provenance.
- P8 is not complete until preview/confirmation, idempotency, transaction audit, rollback/recovery and adversarial tool-call tests pass.


## Current implementation boundary (branch update)

- FVN host: `GET /api/ai/business-context/me` derives the actor from the authenticated server-side user, reads effective permission snapshots and managed scopes through `IAuthorizationService`, and returns module/function metadata. It is a read-only projection, not a permission grant to the model.
- AI service: context, catalog and executor ports are registered to a fail-closed implementation until a trusted adapter is supplied. `/api/ai/capabilities` exposes explicit configuration flags for all three ports.
- Tests: unit tests cover fail-closed context lookup, tool discovery and tool execution. These do not replace host/AI signed integration tests, tenant isolation tests or build/CI verification.
- P4 remains not implemented: no production business tool handler is registered, and no arbitrary SQL/HTTP execution path is introduced.

- The FVN gateway signs the AI context snapshot with the configured API key, tenant, authenticated actor, timestamp, nonce and SHA-256 body digest. The AI API verifies the body digest and signed tenant/actor before accepting the snapshot. This establishes a content-bound, authenticated transport for the context payload; the AI endpoint currently acknowledges rather than persists or consumes the snapshot. It does not authorize model-selected actions or replace fresh host-side authorization on every tool invocation.

- Unit tests cover the content-bound gateway signature: exact requests validate, while changing actor identity or body digest invalidates the signature. SQL nonce persistence remains the replay boundary in the AI API middleware.
