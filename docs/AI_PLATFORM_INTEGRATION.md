# FVN_REGISTER AI Platform integration

This branch integrates `Nang0559/FVN_REGISTER_AI` feature `ai-platform-phase1-6` additively into the canonical FVN_REGISTER host.

## Host boundary

- `FVN_REGISTER.API` remains the authenticated browser gateway.
- AI service credentials and context-signing secrets remain server-side.
- Existing FVN_REGISTER authorization and managed-scope checks remain the first gate for HR facts and review operations.
- AI business writes remain human-approved.

## Integrated components

- AI Core / Application / Infrastructure / Api / Worker projects.
- AI unit tests.
- SQL/AI migrations 001-021.
- HR case gateway controller and workspace.
- AI operational documentation and migration script.
- AI validation workflows.

## Configuration

`FVN_REGISTER.API/appsettings.json` contains empty placeholders for `AIPlatform:BaseUrl`, `ApiKey`, and `ContextSigningKey`. Production secrets must be supplied through the deployment secret/configuration mechanism; they are intentionally not committed here.

The AI service `ContextSigningKey` must be at least 32 characters when configured.
