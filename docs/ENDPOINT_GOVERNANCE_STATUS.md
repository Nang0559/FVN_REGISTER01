# Endpoint Governance – implementation status

Branch: `fix/hrm-background-workers-and-deploy-patch`

## Current flow

```text
SuperAdmin / IT
    -> Draft Policy Version
    -> Submit Common Approval
    -> Approval Route (RequestModule.Endpoint)
    -> Approved
    -> Publish
    -> Endpoint Agent Inventory
    -> Automatic Compliance Evaluation
    -> Version-pinned Finding / Exception / Installation Request
```

## Status

| Area | Status | Notes |
|---|---|---|
| RequestModule.Endpoint | DONE | Reuses existing RequestModule/approval abstraction. |
| Security capability family 3101–3124 | DONE | Registered in existing Security Center; SuperAdmin receives the initial baseline only. IT must be granted the Endpoint capabilities explicitly. |
| Software Catalog | DONE | Manual + Excel, versioned draft, submit and publish; SQL installs a deny-by-default baseline. |
| Windows Service Catalog | DONE | Same canonical policy model; deny-by-default baseline. |
| Catalog aliases / wildcard / regex | DONE | Alias, wildcard and regex matching are supported. |
| Endpoint inventory | DONE | Agent ingestion and inventory summary. Same `LastInventoryHash` skips unchanged inventory writes. |
| Equipment linkage | DONE | Agent cannot self-assign Equipment. FVN can query serial-based suggestions; the suggestion never creates a link automatically. |
| Agent credential provisioning | DONE | Admin provision/rotate/revoke plus agent self-rotation while the current credential is still valid. Lifecycle actions are auditable. |
| Agent DPAPI provisioning | DONE | `FVN_REGISTER.EndpointAgent.exe --protect-secret-stdin` creates `ApiKeyProtected` with DPAPI `LocalMachine`; install script can provision it locally on the target machine. |
| Agent credential expiry / 401 | DONE | Agent rotates before expiry when the server advertises the expiry and attempts one self-rotation/retry after HTTP 401. Expired/revoked credentials require Security Center provisioning. |
| Hardware identity | DONE | Serial from `Win32_BIOS.SerialNumber`; HardwareUuid from `Win32_ComputerSystemProduct.UUID`. |
| Software inventory | DONE | Machine-wide + loaded user hives; update/system-component noise is filtered and versions/architectures are preserved. |
| Service binary hash | DONE | SHA-256 is calculated from executable file contents, not the path string. |
| Antivirus inventory | DONE | Defender plus Windows Security Center providers; unknown third-party protection state is not falsely reported as disabled. |
| Automatic compliance evaluation | DONE | Runs after changed trusted inventory and can also be triggered manually. |
| Finding deduplication | DONE | Compliant items do not create persistent findings; active findings are updated instead of duplicated every ingest. |
| Finding retention | DONE | SQL cleanup retains resolved findings for 180 days and runs in logged background work, outside the ingest request. |
| Credential audit | DONE | Provision/rotate/revoke have endpoint credential audit rows plus application audit entries; automatic rotation does not use a synthetic user id. |
| Inventory transaction | DONE | Identity history and endpoint alerts are written inside the same database transaction as the inventory upsert. |
| Agent configuration | DONE | Invalid/missing configuration stops the process with a non-zero exit code; `ApiBaseUrl` must use HTTPS. |

## Agent runbook

1. IT/SuperAdmin provisions the endpoint credential from Security Center.
2. On the target machine, install with the plaintext API key through stdin:

```powershell
$ApiKey | .\FVN_REGISTER.EndpointAgent.exe --protect-secret-stdin
```

or let `install-agent.ps1 -ApiKey $ApiKey` perform LocalMachine DPAPI protection locally. `-ApiKeyProtected` remains supported for a blob generated on that same machine.
3. The Windows service stores only the DPAPI-protected blob in `appsettings.json`.
4. The agent sends inventory every 30 minutes, uses the server-provided expiry to rotate before expiry, and retries once after a 401.
5. If the key has already expired/revoked, the agent reports that Security Center must provision a new credential.

## Architectural rules retained

- Equipment remains Asset/Master Data; Endpoint only references `EquipmentAssetId`.
- Software/Service policy is Endpoint Governance, not Equipment.
- Common Approval is reused; no `EndpointApprovalEngine`/`EquipmentSoftwareApproval` is introduced.
- Security Center remains the only permission system.
- Excel is an adapter only; persistence goes through the canonical Application contract.
- Published policy versions are immutable in business meaning; a new change creates a new version.
- Endpoint Agent cannot assign itself to an employee or Equipment Asset; ownership/linkage is an FVN-side administrative fact.
- `DeptCode` remains textual; `HrmDeptId` is reserved for a numeric HRM identifier when HRM actually provides one.
