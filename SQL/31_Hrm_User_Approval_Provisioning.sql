/*
FVN_REGISTER - HRM User / Approval Provisioning
Canonical model:

F03Employee -> F03User
F03Employee.DeptCode + PositionCode -> F03ApprovalPolicies (requester scope)
F03ApprovalPolicies.ApprovalPositionCode -> F03Positions -> F03Approvers (candidate pool)

There is NO ApprovalGroup / PositionGroup layer.
PositionCode is the HRM source-of-truth for employee identity and approval capability.

F03ApprovalPolicies defines required levels for the REQUESTER scope and the
approver position through ApprovalPositionCode.
F03Approvers is the candidate pool. Therefore approver provisioning must NOT
join an approver employee directly to the requester's policy PositionCode.
Candidate Level is derived from the approver employee's HRM approval position.
*/
USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

/*
  USER provisioning
  - creates missing users from F03Employees
  - synchronizes HRM identity/scope fields
  - preserves existing PermissionCode
  - deactivates users whose employee is inactive
  - never writes back to HRM
*/
CREATE OR ALTER PROCEDURE dbo.usp_ReconcileEmployeeUsers
    @EmployeeCode nvarchar(50)=NULL,
    @CreatedBy int=0,
    @DefaultPasswordHash nvarchar(255)=N'edbf6b4c784a9d55a68f115834be9d51' -- MD5("Fcc@123"), khớp C# EmployeeHrmSyncJob
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    INSERT dbo.F03Users
    (
        IsActive,CreatedBy,LastModifiedSource,Password,EmployeeCode,FullName,
        PermissionCode,LockoutEnable,NumLoginFailed,LevelApprove,DeptCode,Cvcode
    )
    SELECT
        CASE WHEN e.IsActive=1 THEN 1 ELSE 0 END,
        @CreatedBy,N'HRM',
        @DefaultPasswordHash,
        e.EmployeeCode,e.EmployeeName,
        ISNULL(
            (
                SELECT TOP (1) r.PermissionCode
                FROM dbo.F03HrmUserRoleRules r
                WHERE r.IsActive=1
                  AND (r.DeptCode=e.DeptCode OR r.DeptCode IS NULL)
                  AND (r.PositionCode=e.PositionCode OR r.PositionCode IS NULL)
                ORDER BY
                    CASE
                        WHEN r.DeptCode=e.DeptCode AND r.PositionCode=e.PositionCode THEN 0
                        WHEN r.DeptCode=e.DeptCode THEN 1
                        WHEN r.PositionCode=e.PositionCode THEN 2
                        ELSE 3
                    END,
                    r.Priority,r.Id
            ),5),
        1,0,ISNULL(e.LevelApprove,0),e.DeptCode,e.PositionCode
    FROM dbo.F03Employees e
    WHERE (@EmployeeCode IS NULL OR e.EmployeeCode=@EmployeeCode)
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.F03Users u
          WHERE u.EmployeeCode=e.EmployeeCode
      );

    /*
      OWNERSHIP GUARD: chỉ cập nhật tài khoản do HRM sở hữu (LastModifiedSource='HRM').
      Tài khoản thủ công / break-glass / SuperAdmin không bị khóa và không bị đóng dấu lại.
      Chỉ UPDATE các dòng thực sự thay đổi để không ghi/khóa toàn bộ F03Users mỗi lần chạy.
    */
    UPDATE u
       SET u.IsActive=CASE WHEN e.IsActive=1 THEN 1 ELSE 0 END,
           u.FullName=e.EmployeeName,
           u.LevelApprove=ISNULL(e.LevelApprove,0),
           u.DeptCode=e.DeptCode,
           u.Cvcode=e.PositionCode,
           u.ModifiedBy=@CreatedBy,
           u.ModifiedAt=GETDATE()
    FROM dbo.F03Users u
    INNER JOIN dbo.F03Employees e
        ON e.EmployeeCode=u.EmployeeCode
    WHERE u.LastModifiedSource=N'HRM'
      AND (@EmployeeCode IS NULL OR e.EmployeeCode=@EmployeeCode)
      AND
      (
             ISNULL(u.IsActive,0)<>CASE WHEN e.IsActive=1 THEN 1 ELSE 0 END
          OR ISNULL(u.FullName,N'')<>ISNULL(e.EmployeeName,N'')
          OR ISNULL(u.LevelApprove,0)<>ISNULL(e.LevelApprove,0)
          OR ISNULL(u.DeptCode,-1)<>ISNULL(e.DeptCode,-1)
          OR ISNULL(u.Cvcode,N'')<>ISNULL(e.PositionCode,N'')
      );

    /*
      Canonical RBAC: PermissionCode is the primary/default role code,
      but authorization is evaluated from F03UserRoles -> F03RoleFunctions.
      Therefore every HRM-provisioned user must also receive the matching
      primary role. Existing role assignments are preserved; only the
      default role is repaired when missing.
    */
    INSERT dbo.F03UserRoles
        (IdUser,IdRole,IsPrimary,CreatedBy,LastModifiedSource)
    SELECT
        u.Id,
        r.Id,
        1,
        @CreatedBy,
        N'HRM'
    FROM dbo.F03Users u
    INNER JOIN dbo.F03Roles r
        ON r.RoleCode=ISNULL(u.PermissionCode,5)
       AND r.IsActive=1
    WHERE (@EmployeeCode IS NULL OR u.EmployeeCode=@EmployeeCode)
      AND u.IsActive=1
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.F03UserRoles ur
          WHERE ur.IdUser=u.Id
            AND ur.IdRole=r.Id
      );

    SELECT
        AffectedEmployee=@EmployeeCode,
        ActiveUsers=(SELECT COUNT(*) FROM dbo.F03Users WHERE IsActive=1);
END;
GO

/*
  APPROVER provisioning - POLICY DRIVEN

  F03ApprovalPolicies is the source of truth for which approval positions and
  request levels are available:
      Policy.ApprovalPositionCode -> F03Employee.PositionCode
      Policy.RequestType          -> F03Approvers.RequestType
      Policy.Level                -> F03Approvers.Level

  HRM security reconcile then:
      1. marks every ApprovalPositionCode referenced by an active policy as approval-capable;
      2. derives DefaultApproveLevel from the lowest configured policy level
         for that approval position;
      3. creates/synchronizes one HRM-owned F03Approver candidate for each
         active eligible employee and each active RequestType/Level supported
         by that employee's approval position IN THE EMPLOYEE'S OWN DEPARTMENT
         (ap.DeptCode = e.DeptCode). The default ApproveForDeptCode
         is ALWAYS the employee's actual F03Employees.DeptCode.
      4. deactivates stale HRM-owned rows when the employee/position/policy
         qualification is no longer valid or the employee changes department.

  IMPORTANT:
      F03Employees.LevelApprove > 0 is the HRM/F03Employee gate for appearing
      in the candidate pool.
      HRM sync NEVER copies F03ApprovalPolicies.DeptCode into ApproveForDeptCode.
      F03ApprovalPolicies.DeptCode describes the requester scope, not the
      approver's home department.
      Administrators may manually change ApproveForDeptCode (including another
      department or ALL). Manual rows are protected from later HRM reconcile.

  No ApprovalGroup / PositionGroup layer is used.
*/
CREATE OR ALTER PROCEDURE dbo.usp_ReconcileEmployeeApprovers
    @EmployeeCode nvarchar(50)=NULL,
    @CreatedBy int=0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    /*
      Policy -> Position metadata.
      IsApprove/IsAllowApprove are derived metadata, not a prerequisite
      maintained manually by Admin.
    */
    UPDATE p
       SET p.IsApprove=1,
           p.IsAllowApprove=1,
           p.DefaultApproveLevel=(
               SELECT MIN(ap.Level)
               FROM dbo.F03ApprovalPolicies ap
               WHERE ap.IsActive=1
                 AND ap.ApprovalPositionCode=p.PositionCode
           ),
           p.ModifiedBy=@CreatedBy,
           p.ModifiedAt=GETDATE(),
           p.LastModifiedSource=N'HRM'
    FROM dbo.F03Positions p
    WHERE p.IsActive=1
      AND EXISTS
      (
          SELECT 1
          FROM dbo.F03ApprovalPolicies ap
          WHERE ap.IsActive=1
            AND ap.ApprovalPositionCode=p.PositionCode
      );

    /*
      Keep F03Employees.LevelApprove as the effective FVN approval-capability
      marker. It is not read from the HRM source table; it is derived from the
      active approval-policy configuration through F03Positions.DefaultApproveLevel.

      This is intentionally done BEFORE building #HrmApproverSource so that
      an employee whose position becomes approval-capable in the same HRM
      security sync is immediately eligible for F03Approvers.
    */
    UPDATE e
       SET e.LevelApprove = ISNULL(p.DefaultApproveLevel,0),
           e.ModifiedAt = GETDATE(),
           e.LastModifiedSource = N'HRM'
    FROM dbo.F03Employees e
    INNER JOIN dbo.F03Positions p
        ON p.PositionCode = LTRIM(RTRIM(e.PositionCode))
    WHERE e.IsActive=1;

    /*
      F03Users mirrors the employee approval level for security/UI consumers.
      Existing roles/permissions are NOT changed here.
    */
    UPDATE u
       SET u.LevelApprove = ISNULL(e.LevelApprove,0),
           u.ModifiedAt = GETDATE()
    FROM dbo.F03Users u
    INNER JOIN dbo.F03Employees e
        ON e.EmployeeCode = u.EmployeeCode
    WHERE e.IsActive=1
      AND u.LastModifiedSource = N'HRM'
      AND ISNULL(u.LevelApprove,0) <> ISNULL(e.LevelApprove,0);

    /*
      Positions that are no longer referenced by any active policy are no
      longer approval-capable. Do not touch positions owned by another source.
    */
    UPDATE p
       SET p.IsApprove=0,
           p.IsAllowApprove=0,
           p.DefaultApproveLevel=NULL,
           p.ModifiedBy=@CreatedBy,
           p.ModifiedAt=GETDATE(),
           p.LastModifiedSource=N'HRM'
    FROM dbo.F03Positions p
    WHERE p.LastModifiedSource=N'HRM'
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.F03ApprovalPolicies ap
          WHERE ap.IsActive=1
            AND ap.ApprovalPositionCode=p.PositionCode
      );

    /*
      Candidate pool is now driven directly by active ApprovalPolicies.
      A position can have different levels for different RequestTypes.
    */
    SELECT DISTINCT
        e.EmployeeCode,
        e.PositionCode,
        e.EmployeeName,
        e.EmailAddress,
        e.DeptCode AS DeptCode,
        d.DeptName,
        ap.RequestType,
        ap.Level,
        ap.LevelName,
        ap.RoleName,

        -- HRM default: approver can approve for their own actual department.
        e.DeptCode AS ApproveForDeptCode,
        ISNULL(d.DeptName,CONVERT(nvarchar(20),e.DeptCode)) AS ApproveForDeptName
    INTO #HrmApproverSource
    FROM dbo.F03Employees e
    INNER JOIN dbo.F03ApprovalPolicies ap
        ON ap.ApprovalPositionCode=LTRIM(RTRIM(e.PositionCode))
       AND ap.IsActive=1
       -- Policy-driven per department: only the policies configured for the employee's own
       -- department decide which RequestType/Level that employee receives.
       AND ap.DeptCode=e.DeptCode
    LEFT JOIN dbo.F03Departments d
        ON d.DeptCode=e.DeptCode
    WHERE e.IsActive=1
      AND ISNULL(e.LevelApprove,0) > 0
      AND (@EmployeeCode IS NULL OR e.EmployeeCode=@EmployeeCode);

    MERGE dbo.F03Approvers AS target
    USING #HrmApproverSource AS src
      ON target.ApproverCode=src.EmployeeCode
     AND target.RequestType=
         CASE src.RequestType
             WHEN 0 THEN N'Leave'
             WHEN 1 THEN N'Overtime'
             WHEN 2 THEN N'Trip'
             WHEN 3 THEN N'Equipment'
         END
     AND target.Level=src.Level
     AND target.ApproveForDeptCode=src.ApproveForDeptCode
    WHEN MATCHED
         AND ISNULL(target.LastModifiedSource,N'')=N'HRM'
    THEN
        UPDATE SET
            target.IsActive=1,
            target.UserId=(SELECT TOP(1) u.Id FROM dbo.F03Users u WHERE u.EmployeeCode=src.EmployeeCode),
            target.PositionCode=src.PositionCode,
            target.ApproverName=src.EmployeeName,
            target.ApproverEmail=ISNULL(src.EmailAddress,N''),
            target.ApproverDeptCode=ISNULL(src.DeptCode,0),
            target.ApproverDeptName=ISNULL(src.DeptName,CONVERT(nvarchar(20),src.DeptCode)),
            target.ApproveForDeptName=src.ApproveForDeptName,
            target.RoleName=ISNULL(src.RoleName,src.LevelName),
            target.ModifiedBy=@CreatedBy,
            target.ModifiedAt=GETDATE(),
            target.LastModifiedSource=N'HRM'
    WHEN NOT MATCHED THEN
        INSERT
        (
            IsActive,CreatedBy,LastModifiedSource,UserId,
            RequestType,ApproverCode,PositionCode,ApproverName,ApproverEmail,
            ApproverDeptCode,ApproverDeptName,ApproveForDeptCode,ApproveForDeptName,
            Level,RoleName
        )
        VALUES
        (
            1,@CreatedBy,N'HRM',
            (SELECT TOP(1) u.Id
             FROM dbo.F03Users u
             WHERE u.EmployeeCode=src.EmployeeCode),
            CASE src.RequestType
                WHEN 0 THEN N'Leave'
                WHEN 1 THEN N'Overtime'
                WHEN 2 THEN N'Trip'
                WHEN 3 THEN N'Equipment'
            END,
            src.EmployeeCode,src.PositionCode,src.EmployeeName,
            ISNULL(src.EmailAddress,N''),ISNULL(src.DeptCode,0),
            ISNULL(src.DeptName,CONVERT(nvarchar(20),src.DeptCode)),src.ApproveForDeptCode,
            src.ApproveForDeptName,src.Level,ISNULL(src.RoleName,src.LevelName)
        );

    /*
      Deactivate only HRM-owned rows that are no longer represented by
      active employee + active ApprovalPolicy configuration.
    */
    UPDATE a
       SET a.IsActive=0,
           a.ModifiedBy=@CreatedBy,
           a.ModifiedAt=GETDATE(),
           a.LastModifiedSource=N'HRM'
    FROM dbo.F03Approvers a
    INNER JOIN dbo.F03Employees e
        ON e.EmployeeCode=a.ApproverCode
    WHERE a.LastModifiedSource=N'HRM'
      AND (@EmployeeCode IS NULL OR e.EmployeeCode=@EmployeeCode)
      AND
      (
          e.IsActive=0
          OR ISNULL(e.LevelApprove,0) <= 0
          OR (
              a.ApproveForDeptCode <> e.DeptCode
          )
          OR NOT EXISTS
          (
              SELECT 1
              FROM dbo.F03ApprovalPolicies ap
              WHERE ap.IsActive=1
                AND ap.ApprovalPositionCode=LTRIM(RTRIM(e.PositionCode))
                AND ap.DeptCode=e.DeptCode
                AND a.RequestType=
                    CASE ap.RequestType
                        WHEN 0 THEN N'Leave'
                        WHEN 1 THEN N'Overtime'
                        WHEN 2 THEN N'Trip'
                        WHEN 3 THEN N'Equipment'
                    END
                AND a.Level=ap.Level
          )
      );

    SELECT
        AffectedEmployee=@EmployeeCode,
        ActiveApprovers=(SELECT COUNT(*) FROM dbo.F03Approvers WHERE IsActive=1),
        PolicyPositions=(
            SELECT COUNT(DISTINCT ap.ApprovalPositionCode)
            FROM dbo.F03ApprovalPolicies ap
            WHERE ap.IsActive=1
        );
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ReconcileHrmSecurity
    @EmployeeCode nvarchar(50)=NULL,
    @CreatedBy int=0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    EXEC dbo.usp_ReconcileEmployeeUsers
        @EmployeeCode=@EmployeeCode,
        @CreatedBy=@CreatedBy;

    EXEC dbo.usp_ReconcileEmployeeApprovers
        @EmployeeCode=@EmployeeCode,
        @CreatedBy=@CreatedBy;
END;
GO

/*
  Verification: these are read-only and intentionally return zero when
  current HRM-derived security is fully provisioned.
*/
SELECT MissingUsers=COUNT(*)
FROM dbo.F03Employees e
LEFT JOIN dbo.F03Users u ON u.EmployeeCode=e.EmployeeCode
WHERE e.IsActive=1 AND u.Id IS NULL;

SELECT MissingApprovers=COUNT(*)
FROM dbo.F03Employees e
INNER JOIN dbo.F03ApprovalPolicies ap
    ON ap.ApprovalPositionCode=e.PositionCode
   AND ap.IsActive=1
LEFT JOIN dbo.F03Approvers a
    ON a.ApproverCode=e.EmployeeCode
   AND a.IsActive=1
   AND a.RequestType=CASE ap.RequestType
       WHEN 0 THEN N'Leave'
       WHEN 1 THEN N'Overtime'
       WHEN 2 THEN N'Trip'
       WHEN 3 THEN N'Equipment'
   END
   AND a.Level=ap.Level
   AND a.ApproveForDeptCode=e.DeptCode
WHERE e.IsActive=1
  AND ISNULL(e.LevelApprove,0) > 0
  AND a.Id IS NULL;
GO
