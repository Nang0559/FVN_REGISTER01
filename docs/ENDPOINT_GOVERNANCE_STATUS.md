# Endpoint Governance – implementation status

Branch: `docs/cleanup-and-help-safe`

## Current flow

```text
Manual / Excel
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
| Security capability family 3101–3124 | DONE | Registered in existing Security Center; SuperAdmin receives the initial baseline only. |
| Software Catalog | DONE | Manual + Excel, versioned draft, submit and publish. |
| Windows Service Catalog | DONE | Same canonical policy model; no duplicate approval engine. |
| Excel preview/validation | DONE | Excel remains an adapter into canonical DTOs. |
| Common Approval | DONE | Uses existing approval provider/route/policy infrastructure. |
| Endpoint inventory | DONE | Agent ingestion and inventory summary. |
| Equipment linkage | DONE | `F03EndpointDevice.EquipmentAssetId` reference; UI shows EquipmentCode + EquipmentName. |
| Agent credential provisioning | DONE | Existing credential service; corrected to Endpoint capability codes; UI supports provision/rotate/revoke. |
| Automatic compliance evaluation | DONE | Runs after trusted agent ingestion and can also be triggered manually. |
| Version-pinned findings | DONE | Finding records retain PolicyId/PolicyVersion/PolicyItemId. |
| Installation Request | DONE | Uses Endpoint request + Security Review + Common Approval. |
| Compliance Exception request | DONE | Uses existing Endpoint request workflow; approval remains common. |
| Endpoint Governance UI | DONE | Catalog, manual entry, Excel, version lifecycle, requests, Security Review and compliance. |
| Endpoint Inventory UI | DONE | Inventory, compliance, Equipment linkage and installation request. |
| Credential UI | DONE | Provision/rotate/revoke with one-time secret display. |
| Navigation | DONE | Existing Shared NavMenu; capability-gated. |
| Legacy duplicate SQL catalog | REMOVED | Old `56_Endpoint_Governance.sql` removed; canonical schema is 54/55 + verification. |
| Duplicate legacy policy/compliance tables | REMOVED FROM NEW DEPLOYMENT | Inventory foundation no longer creates `F03SoftwarePolicies`, `F03WindowsServicePolicies`, `F03EndpointComplianceResults` or `F03EndpointComplianceExceptions`; those responsibilities belong to canonical Governance/Request/Findings. |
| Compliance alert model | INTENTIONAL | Compliance warnings are canonical Findings; existing identity/agent alerts remain for technical endpoint events. No second alert architecture is introduced. |
| Agent deployment/installation | EXISTING | Agent project remains separate; this module consumes its trusted inventory contract. |

## Architectural rules retained

- Equipment remains Asset/Master Data; Endpoint only references `EquipmentAssetId`.
- Software/Service policy is Endpoint Governance, not Equipment.
- Common Approval is reused; no `EndpointApprovalEngine`/`EquipmentSoftwareApproval` is introduced.
- Security Center remains the only permission system.
- `BaseAuditEntity.IsActive` remains `bool?`; query semantics must use explicit nullable logic.
- Excel is an adapter only; persistence goes through the canonical Application contract.
- Published policy versions are immutable in business meaning; a new change creates a new version.
- Endpoint Agent cannot assign itself to an employee or Equipment Asset; ownership/linkage is an FVN-side administrative fact.
