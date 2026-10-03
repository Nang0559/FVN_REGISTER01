# Endpoint Inventory Implementation Plan

## Phase 1 — data and security foundation
- Endpoint device identity and registration.
- Software/service inventory snapshots.
- Device credential lifecycle: issue, rotate, revoke.
- Server-side binding between DeviceKey and equipment asset.
- Idempotent inventory ingestion.

## Phase 2 — compliance
- Effective software policies.
- Effective Windows service policies.
- Scope resolution by company, department, position, schema and device.
- Exception resolution before NonCompliant result.
- Alert deduplication and resolution.

## Phase 3 — approval
- Policy draft → snapshot → existing Approval Engine → approved → active.
- Exception request → snapshot → existing Approval Engine → approved → active.
- Never activate an unapproved policy.
- Never create a second approval engine for endpoint compliance.

## Phase 4 — UI
- Endpoint dashboard.
- Device detail: hardware, software, services, compliance and alerts.
- Software catalog/policy.
- Windows service catalog/policy.
- Exceptions and approval history.
- Unmanaged/offline device views.

## Phase 5 — Windows Agent
- Windows Service.
- Collect installed applications from supported Windows uninstall registry locations.
- Collect Windows service name/display name/state/start mode.
- Send complete snapshots over HTTPS.
- Local retry queue with bounded size.
- No remote command execution.

## Phase 6 — network discovery
- Explicit network scopes, exclusions, rate limits, schedule and audit.
- Discovery identifies unmanaged endpoints only.
- No credentialed WMI/SMB scan in V1.

## Phase 7 — optional corporate integrations
- Microsoft Defender read-only connector when tenant permissions are available.
- Intune read-only connector when permissions are available.
- Reconcile sources without making either dependency mandatory.
