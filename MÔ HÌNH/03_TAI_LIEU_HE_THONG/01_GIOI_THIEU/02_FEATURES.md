# FVN REGISTER — TỔNG QUAN TÍNH NĂNG

## 1. Bản đồ tính năng

```mermaid
mindmap
  root((FVN REGISTER))
    Employee
      Leave
      Overtime
      Trip
      Equipment
      Track status
    Approval
      Multi-level
      Snapshot
      Approve
      Reject
      Escalation
      Notification
    Execution
      Planned
      Actual
      Reconciliation
      Confirmation
      Evidence
      HR Review
    Workspace
      Dashboard
      Action Center
      Notification
      Work Calendar
    HR
      Attendance Calculation
      Execution Resolution
      Payroll Input
    Management
      Reports
      Statistics
      Export
    Security
      Authentication
      Capability
      Data Scope
      Audit
```

## 2. Request & Approval

```mermaid
sequenceDiagram
    participant U as Employee
    participant M as Business Module
    participant W as Approval Workflow
    participant S as Approval Snapshot
    participant A as Approver
    participant N as Notification

    U->>M: Create / Edit
    M->>M: Validate
    U->>M: Submit
    M->>W: Initialize
    W->>S: Capture immutable hierarchy
    W->>N: Notify first approver
    A->>W: Approve / Reject
    W->>W: Move to next required step
    W->>N: Notify next actor
```

Approval timeout escalation là system decision `Escalated`, không đồng nghĩa `Rejected`.

## 3. Leave

- Request và approval.
- Balance.
- Lịch nghỉ đã duyệt.
- Calendar.
- Reports.

## 4. OT

Hạn mức phải lấy từ policy/rule hiện hành; Weekly chỉ áp dụng khi có rule cấu hình. Không hard-code hạn mức trong UI.

```mermaid
flowchart TD
    OT[OT Request] --> V[Server Validator]
    V --> D[Daily]
    V --> W[Weekly if configured]
    V --> M[Monthly]
    V --> Y[Yearly]
    D --> OK{Valid?}
    W --> OK
    M --> OK
    Y --> OK
    OK -->|No| ERR[Reject with validation message]
    OK -->|Yes| SUB[Submit / Approval]
```

## 5. Trip

```mermaid
flowchart LR
    D[Draft Trip] --> P[Pending Approval]
    P --> I[InProgress]
    I --> A[Approved]
    A --> X[Trip Actual]
    X --> R[Execution Reconciliation]
```

Trip sử dụng approval engine chung.

## 6. Equipment + QR

```mermaid
flowchart TD
    U[Authorized User] --> R[Create Equipment Request]
    R --> QR[Generate QR Token]
    QR --> INACTIVE[QR not active]
    R --> AP[Approval]
    AP -->|Approved| ASSET[Equipment Asset]
    ASSET --> ACTIVE[QR Active]
    ACTIVE --> SCAN[Scan QR]
    SCAN --> REP[Repair Request]
    REP --> RAP[Repair Approval]
    RAP -->|Approved| HIST[Official Repair History]
```

Module access và approval authority là hai lớp khác nhau.

## 7. Execution Reconciliation

```mermaid
flowchart LR
    PLAN[Planned / Approved] --> REC[Reconciliation]
    ACT[Actual] --> REC
    REC -->|Matched| DONE[Resolved]
    REC -->|Mismatch| CONF[Confirmation]
    CONF --> EVI[Evidence if policy requires]
    EVI --> REVIEW[Review]
    REVIEW --> HR[HR Resolution]
    HR --> DONE
```

Reconciliation là framework dùng chung cho OT, Leave, Trip và module tương lai.

## 8. Action / Calendar / Notification

```mermaid
flowchart TB
    B[Business Module] --> REC[Reconciliation / Business Event]
    REC --> ACT[Action]
    ACT --> CAL[Calendar Projection]
    ACT --> NOTI[Notification]
    ACT --> DASH[Dashboard]
    B --> DASH
    B --> CAL
    ACT -. same ActionId .-> CAL
    ACT -. same ActionId .-> NOTI
    ACT -. same ActionId .-> DASH
```

- Action = công việc.
- Calendar = projection/navigation.
- Notification = delivery/read state.
- Dashboard = aggregation.
- Business module = source of truth.

## 9. HR Review & Attendance

```mermaid
flowchart TD
    MISMATCH[Mismatch / User Feedback] --> QUEUE[HR Review Queue]
    QUEUE --> DEC{HR Decision}
    DEC -->|OK| CORR[Execution Correction]
    DEC -->|NG| REJ[Rejected with Reason]
    CORR --> CALC[HRM-compatible Calculation]
    CALC --> SNAP[FVN Attendance Snapshot]
    SNAP --> PAY[Payroll Input]
```

HR không sửa trực tiếp kết quả attendance snapshot. Correction phải đi qua calculation pipeline.

## 10. Reports

Danh mục chính: Leave, OT, Trip, Equipment và Attendance. Export là capability riêng và luôn bị giới hạn bởi Data Scope.

## 11. Security

```mermaid
flowchart LR
    AUTH[Authenticated User] --> CAP[Function / Action Capability]
    CAP --> SCOPE[Effective Data Scope]
    SCOPE --> QUERY[Validated Query]
    QUERY --> RESULT[DTO / Excel]
    CLIENT[UI filter] -. never grants access .-> QUERY
```

Scope chuẩn: Own, Employee, Department, All. Quyền Approve không tự động có Scope All.
