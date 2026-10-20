# FVN_REGISTER AI Center — Documentation Index

This index lists the current reusable AI Core design and operations documents. The **root** `AI_PLATFORM_PHASES_STATUS.md` is the single source of truth for readiness.

| Document | Purpose |
|---|---|
| [AI Platform Architecture](AI_PLATFORM_ARCHITECTURE.md) | Current module-neutral architecture, boundaries, security and deployment |
| [AI Platform Phase Status](../AI_PLATFORM_PHASES_STATUS.md) | Canonical foundation/partial status of Phase 1–5 Core and Phase 6 applications |
| [AI Platform Operations](AI_PLATFORM_OPERATIONS.md) | Configuration/secrets, migrations 001–014, backup/restore, monitoring and cost controls |
| [AI Platform Implementation Plan](AI_PLATFORM_IMPLEMENTATION_PLAN.md) | Work packages, dependencies and exit gates |
| [HR Policy Assistant Design](AI_HR_POLICY_ASSISTANT.md) | Policy knowledge lifecycle and RAG application design; implementation status remains subject to the canonical phase status |
| [HR Evidence Reconciliation Pilot](AI_HR_RECONCILIATION_PILOT.md) | HR pilot scope and integration boundary |
| [HR Case Assistant Implementation](AI_HR_CASE_ASSISTANT_IMPLEMENTATION.md) | HR facts gateway, evidence reconciliation, review workflow and deployment details |

## Agreed roadmap

### AI Core
1. Universal Data Ingestion.
2. AI Document Understanding.
3. Data Extraction & Validation.
4. RAG & Knowledge Base.
5. Active Learning.

### Applications
6. FVN_REGISTER AI applications: registration assistant, data entry/import, policy assistant, HR assistant, reporting assistant and future modules.

## Architectural rule

Phase 1–5 remain reusable and module-neutral. OT, leave, attendance, trip, equipment and other business semantics belong in published schemas, validation profiles and FVN_REGISTER adapters. HR Case Assistant is a Phase 6 pilot; it is not the architecture template for every module.

The [root Phase Status](../AI_PLATFORM_PHASES_STATUS.md) document is the only implementation-status source. A design, SQL table or isolated code path does not mean a capability is complete.
