# FVN_REGISTER AI — Documentation Index

This index is the entry point for the AI platform design on branch `feature/ai-manufacturing-data-platform`.

| Document | Purpose |
|---|---|
| [AI Platform Architecture](AI_PLATFORM_ARCHITECTURE.md) | Overall architecture, boundaries, ingestion pipeline, security and roadmap |
| [AI Platform Phase Status](AI_PLATFORM_PHASES_STATUS.md) | Implemented vs pending code, migration order and verification state |
| [HR Policy Assistant Design](AI_HR_POLICY_ASSISTANT.md) | Upload, approve, version and ask questions about HR policies with citations |
| [HR Evidence Reconciliation Pilot](AI_HR_RECONCILIATION_PILOT.md) | Initial pilot scope and integration boundary |
| [HR Case Assistant Implementation](AI_HR_CASE_ASSISTANT_IMPLEMENTATION.md) | Implemented evidence-to-facts reconciliation, HR workspace, review audit, SQL and deployment configuration |
 
## Product capabilities

1. Document intelligence — ingest files and create reviewable extraction candidates.
2. HR Policy Assistant — retrieve approved, current HR rules and explain procedures such as OT registration.
3. HR Case Assistant — implemented FVN-gateway facts retrieval, evidence extraction, deterministic comparisons, Ollama draft support, HR workspace and review audit. OCR and notification delivery remain pending.
4. HR reporting chatbot — natural-language access to existing authorized reports.

## Important status note

The documents distinguish target design from implemented code. HR Case Assistant is the first operational AI workflow. HR Policy RAG (policy administration, approval/publication, vector retrieval, ACL citations and policy chat UI) remains a separate backlog; scanned-PDF OCR and employee notification delivery are also pending.
