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
| Software Catalog | DONE | Manual + Excel, versioned draft, submit and publish; SQL installs a deny-by-default baseline so the system never relies on an absent policy. |
| Windows Service Catalog | DONE | Same canonical policy model; deny-by-default baseline. |
| Catalog aliases / wildcard / regex | DONE | `AliasNames` supports `;`/newline-separated aliases; names and publishers support exact, `*`/`?` wildcard and `REGEX:` patterns. |
| Excel preview/validation | DONE | Excel remains an adapter into canonical DTOs. |
| Common Approval | DONE | Uses existing approval provider/route/policy infrastructure. |
| Endpoint inventory | DONE | Agent ingestion and inventory summary. Same `LastInventoryHash` skips unchanged inventory writes. |
| Equipment linkage | DONE | `F03EndpointDevice.EquipmentAssetId` reference; UI shows EquipmentCode + EquipmentName. Agent cannot self-assign Equipment. |
| Agent credential provisioning | DONE | Admin provision/rotate/revoke plus agent self-rotation while the current credential is still valid. |
| Agent DPAPI provisioning | DONE | `FVN_REGISTER.EndpointAgent.exe --protect-secret-stdin` creates `ApiKeyProtected` with DPAPI `LocalMachine`; install script can provision it on the target machine. |
| Agent credential expiry / 401 | DONE | Agent rotates proactively before expiry and attempts one self-rotation/retry after HTTP 401. If the credential is already expired/revoked, Security Center provisioning is required. |
| Hardware identity | DONE | Serial from `Win32_BIOS.SerialNumber`; HardwareUuid from `Win32_ComputerSystemProduct.UUID`. No Manufacturer/Model pseudo-UUID. |
| Software inventory | DONE | Machine-wide + loaded user hives; Windows update/system-component noise is filtered. Software versions/architectures are preserved. |
| Service binary hash | DONE | SHA-256 is calculated from the service executable file contents, not the path string. |
| Antivirus inventory | DONE | Defender plus Windows Security Center antivirus providers. Non-Defender providers are detected without falsely marking them as disabled when their detailed protection flags are unavailable. |
| Automatic compliance evaluation | DONE | Runs after changed trusted inventory and can also be triggered manually. |
| Finding deduplication | DONE | Compliant items do not create persistent findings. Active non-compliant/unknown findings are updated instead of duplicated every ingest. |
| Finding retention | DONE | SQL cleanup procedure retains resolved findings for 180 days and deletes in batches. |
| Version-pinned findings | DONE | Finding records retain PolicyId/PolicyVersion/PolicyItemId. |
| Installation Request | DONE | Uses Endpoint request + Security Review + Common Approval. |
| Compliance Exception request | DONE | Uses existing Endpoint request workflow; approval remains common. |
| Endpoint Governance UI | DONE | Catalog, manual entry, Excel, version lifecycle, requests, Security Review and compliance. |
| Endpoint Inventory UI | DONE | Inventory, compliance, Equipment linkage and installation request. |
| Credential UI | DONE | Provision/rotate/revoke with one-time secret display. |
| Navigation | DONE | Existing Shared NavMenu; capability-gated. |
| Legacy duplicate SQL catalog | REMOVED | Old `56_Endpoint_Governance.sql` removed; canonical schema is 54/55 + verification. |
| Duplicate legacy policy/compliance tables | REMOVED FROM NEW DEPLOYMENT | Inventory foundation no longer creates legacy policy/result/exception tables. |
| Compliance alert model | INTENTIONAL | Compliance warnings are canonical Findings; existing identity/agent alerts remain for technical endpoint events. |

## Agent runbook

1. IT/SuperAdmin provisions the endpoint credential from Security Center.
2. On the target machine, install with the plaintext API key through stdin:

```powershell
$ApiKey | .\FVN_REGISTER.EndpointAgent.exe --protect-secret-stdin
```

or let `install-agent.ps1 -ApiKey $ApiKey` perform the LocalMachine DPAPI protection locally. `-ApiKeyProtected` remains supported when the protected blob was already generated on that same machine.
3. The Windows service stores only the DPAPI-protected blob in `appsettings.json`.
4. The agent sends inventory every 30 minutes, rotates its credential 30 days before expiry, and retries once after a 401.
5. If the key has already expired/revoked, the agent reports that Security Center must provision a new credential; it never guesses or creates a server credential by itself.

## Architectural rules retained

- Equipment remains Asset/Master Data; Endpoint only references `EquipmentAssetId`.
- Software/Service policy is Endpoint Governance, not Equipment.
- Common Approval is reused; no `EndpointApprovalEngine`/`EquipmentSoftwareApproval` is introduced.
- Security Center remains the only permission system.
- `BaseAuditEntity.IsActive` remains `bool?`; query semantics must use explicit nullable logic.
- Excel is an adapter only; persistence goes through the canonical Application contract.
- Published policy versions are immutable in business meaning; a new change creates a new version.
- Endpoint Agent cannot assign itself to an employee or Equipment Asset; ownership/linkage is an FVN-side administrative fact.
- `DeptCode` remains textual; `HrmDeptId` is reserved for a numeric HRM identifier when HRM actually provides one.
