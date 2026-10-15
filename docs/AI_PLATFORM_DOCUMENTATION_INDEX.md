# FVN_REGISTER AI Center — Documentation Index

This index describes the reusable AI Core roadmap on branch `feature/ai-manufacturing-data-platform`.

| Document | Purpose |
|---|---|
| [AI Platform Architecture](AI_PLATFORM_ARCHITECTURE.md) | Module-neutral architecture, boundaries, security, integration and deployment |
| [AI Platform Phase Status](AI_PLATFORM_PHASES_STATUS.md) | The authoritative status of Phase 1–5 Core and Phase 6 applications |
| [AI Platform Implementation Plan](AI_PLATFORM_IMPLEMENTATION_PLAN.md) | Work packages, dependencies and exit gates |
| [HR Policy Assistant Design](AI_HR_POLICY_ASSISTANT.md) | HR policy knowledge lifecycle and future RAG application |
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

Phase 1–5 must remain reusable and module-neutral. OT, leave, attendance, trip, equipment and other business semantics belong in published schemas, validation profiles and FVN_REGISTER adapters. HR Case Assistant is a Phase 6 pilot; it is not the architecture template for every module.

The [Phase Status](AI_PLATFORM_PHASES_STATUS.md) document is the single source of truth for implementation status. A design or SQL table alone does not mean a capability is complete.
