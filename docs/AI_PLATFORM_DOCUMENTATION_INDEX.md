# FVN_REGISTER AI — Documentation Index

This index is the entry point for the AI platform design on branch `feature/ai-manufacturing-data-platform`.

| Document | Purpose |
|---|---|
| [AI Platform Architecture](AI_PLATFORM_ARCHITECTURE.md) | Overall architecture, boundaries, ingestion pipeline, security and roadmap |
| [AI Platform Phase Status](AI_PLATFORM_PHASES_STATUS.md) | Implemented vs pending code, migration order and verification state |
| [HR Policy Assistant Design](AI_HR_POLICY_ASSISTANT.md) | Upload, approve, version and ask questions about HR policies with citations |
| [HR Evidence Reconciliation Pilot](AI_HR_RECONCILIATION_PILOT.md) | Analyze user evidence and prepare HR reconciliation replies |
 
## Product capabilities

1. Document intelligence — ingest files and create reviewable extraction candidates.
2. HR Policy Assistant — retrieve approved, current HR rules and explain procedures such as OT registration.
3. HR reconciliation assistant — compare user evidence with authorized attendance/leave/OT facts and draft an HR response.
4. HR reporting chatbot — natural-language access to existing authorized reports.

## Important status note

The documents distinguish target design from implemented code. Policy administration, approval/publication, Qdrant RAG, policy chat UI, OCR and full HR integration are design/backlog items until explicitly marked implemented in the phase status document.
