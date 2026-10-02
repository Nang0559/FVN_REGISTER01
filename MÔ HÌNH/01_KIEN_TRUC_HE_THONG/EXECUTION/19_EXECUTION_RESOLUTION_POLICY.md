# Execution Reconciliation — Resolution Policy & Appeal

> Canonical design document for the bounded employee/HR resolution workflow. This document supersedes the earlier assumption that an HR resolution immediately finalizes a reconciliation.

## 1. Scope

The workflow applies to attendance/execution reconciliation cases where actual attendance and registered execution differ and the employee submits feedback/evidence.

The system separates reconciliation business state, actionable task/action state, evidence review, employee decision after HR resolution, bounded appeal rounds, final decision, calendar projection, and payroll readiness.

## 2. Lifecycle

```text
Mismatch
  ↓
Employee feedback
  ↓
HR claim / lock
  ↓
HR evidence review
  ↓
HR resolution
  ↓
AwaitingEmployeeDecision
  ├─ ACCEPT → Resolved → correction (when applicable) → calendar complete
  └─ APPEAL → AppealRound + 1
                ↓
           AppealReviewing
                ↓
           HR resolution
             ├─ rounds remain → AwaitingEmployeeDecision
             └─ max reached → FinalDecisionPending
                                      ↓
                                 final HR decision
                                      ↓
                                    Resolved
```

## 3. State responsibilities

| State | Meaning | Current actionable party |
|---|---|---|
| Mismatch | Reconciliation requires employee feedback/processing | Depends on action |
| AwaitingConfirmation | Employee feedback/confirmation is pending | Employee |
| AwaitingEmployeeDecision | HR has issued a non-final result | Employee |
| AppealReviewing | Employee appealed and the case is back with HR | HR/operator |
| FinalDecisionPending | Policy requires a final authority decision | Final HR authority |
| Resolved | Workflow is closed | None |

The reconciliation ActionId points to the current actionable task. Completed actions remain in action history.

## 4. HR claim / lock

Multiple authorized HR operators may have access to the same execution-review function, but only one operator may actively process a case at a time.

- Claim uses an application lock keyed by reconciliation ID.
- The claim expires after the configured claim timeout.
- Only the current claim owner can continue evidence review or resolution.
- Another operator can view the case but cannot mutate it while it is owned by someone else.
- The server enforces the rule; UI locking is only a usability aid.

## 5. Resolution policy

Policy is managed at /admin/execution-policies and is protected by Execution.PolicyManage (function code 3073).

Supported controls:
- EmployeeResponseHours: employee response SLA.
- EmployeeTimeoutMode: timeout escalates to final decision or auto-accepts the HR result.
- HrReviewHours: HR processing SLA.
- AllowEmployeeAppeal: whether appeal is permitted.
- MaxAppealRounds: hard upper bound for employee appeal rounds.
- AppealReviewHours: HR SLA for appeal rounds.
- RequireEvidenceOnAppeal: requires evidence submitted after the latest HR resolution.
- RequireFinalDecision: whether the final-decision stage is required.
- FinalDecisionPositionCode: HRM position code used for the final authority rule.
- PayrollCutoffMode: reserved policy control for payroll-cutoff behavior.
- AllowReopenAfterPayroll: controls reopening policy after payroll processing.
- AdjustmentPeriodMode: controls the intended correction period behavior.
- EffectiveFrom, EffectiveTo, PolicyVersion, IsActive: version/effective-date management.

### Policy versioning

A reconciliation case snapshots the effective policy into ResolutionPolicySnapshotJson when the case is created. Later policy edits therefore do not silently change the rules of an existing case.

Future-dated policy versions may be scheduled without changing the currently effective policy. Effective windows must not overlap.

## 6. Employee decision

After a non-final HR resolution, the employee receives a result-confirmation action.

### Accept

- marks the employee decision as accepted;
- closes the current employee action;
- moves the reconciliation to Resolved;
- updates the calendar projection;
- applies attendance correction only when the final HR decision is an applicable/OK result.

### Appeal

- validates AllowEmployeeAppeal;
- validates AppealRound < MaxAppealRounds;
- when required, validates new evidence after the latest HR resolution;
- increments AppealRound;
- closes the employee decision action;
- creates the next HR appeal action;
- sets ActionId to that current HR action;
- moves to AppealReviewing, or FinalDecisionPending when the maximum round has been reached.

## 7. Timeout

Expired employee result actions are processed by the execution background worker.

Two policy modes exist:
1. Escalate: employee timeout closes the employee decision action and creates a final-decision HR action.
2. Auto-accept: the system records acceptance on behalf of the employee according to the stored policy snapshot.

Employee result actions must not be prematurely expired by the generic action lifecycle worker before this timeout processor runs.

## 8. Final decision

When the maximum appeal boundary is reached, the previous HR resolver cannot perform the final decision for the same case.

The final decision is a separate stage. The final authority is represented using HRM PositionCode, consistent with the system's approval model.

## 9. Calendar and Action Center

Calendar is a projection of workflow state:
- AwaitingEmployeeDecision → action required;
- AppealReviewing → action required;
- FinalDecisionPending → action required;
- Resolved → completed/resolved;
- active workflow states are sticky and must not be overwritten by ordinary attendance reconciliation runs.

Calendar and Action Center consume the same ActionId chain so the user opens the current task rather than an obsolete completed task.

## 10. Payroll boundary

Unresolved reconciliation remains a payroll-readiness blocker.

PayrollCutoffMode is stored as policy configuration, but automatic finalization at payroll cutoff is not implied by this document. Any future automatic cutoff behavior must be explicitly integrated with payroll prepare/lock processing and audited separately.

## 11. Audit

Every important transition is recorded in F03ExecutionReconciliationHistory, including state transition, actor, reason, employee/HR decision, appeal round, and final decision.

The detail view exposes this history so employees and HR can understand the complete case timeline.

## 12. Security boundary

UI visibility is not authorization. The API/service layer must enforce function/capability authorization, claim ownership, valid state transitions, policy limits, employee ownership for employee decisions, and final-decision separation.
