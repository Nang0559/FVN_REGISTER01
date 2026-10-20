# FVN_REGISTER AI Center — Documentation Index

This index lists the reusable AI Core design, product roadmap and operations documents. The **root** `AI_PLATFORM_PHASES_STATUS.md` is the single source of truth for implementation readiness.

| Document | Purpose |
|---|---|
| [AI Platform Architecture](AI_PLATFORM_ARCHITECTURE.md) | Current module-neutral architecture, boundaries, security and deployment |
| [P1–P8 Product Roadmap](AI_PLATFORM_P1_P8_ROADMAP.md) | Target architecture from AI foundation through business context, tools, learning, documents and transactions |
| [AI Platform Phase Status](../AI_PLATFORM_PHASES_STATUS.md) | Canonical status of legacy AI Core phases and mapping to P1–P8 |
| [AI Platform Operations](AI_PLATFORM_OPERATIONS.md) | Configuration/secrets, migrations, backup/restore, monitoring and cost controls |
| [AI Platform Implementation Plan](AI_PLATFORM_IMPLEMENTATION_PLAN.md) | Work packages, dependencies and exit gates |
| [HR Policy Assistant Design](AI_HR_POLICY_ASSISTANT.md) | Policy knowledge lifecycle and RAG application design |
| [HR Evidence Reconciliation Pilot](AI_HR_RECONCILIATION_PILOT.md) | HR pilot scope and integration boundary |
| [HR Case Assistant Implementation](AI_HR_CASE_ASSISTANT_IMPLEMENTATION.md) | HR facts gateway, evidence reconciliation, review workflow and deployment details |

## Existing AI Core implementation track

1. Universal Data Ingestion.
2. AI Document Understanding.
3. Data Extraction & Validation.
4. RAG & Knowledge Base.
5. Active Learning.
6. FVN_REGISTER AI applications.

## Product architecture track

P1–P8 is the product/platform sequence defined in [AI Platform P1–P8 Roadmap](AI_PLATFORM_P1_P8_ROADMAP.md). It does not renumber or replace the existing Core implementation phases.

## Architectural rule

AI Core remains reusable and module-neutral. OT, leave, attendance, trip, equipment, execution, production and quality semantics are exposed through typed FVN_REGISTER metadata and authorized adapters. HR Case Assistant is a pilot, not the architecture template for every module.

A design, contract, SQL table or isolated code path does not mean a capability is production-complete.
