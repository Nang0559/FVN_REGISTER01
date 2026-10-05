/*
  Execution resolution policy
  - Extends existing F03ExecutionPolicies; no second policy table is introduced.
  - Version is bumped when an active policy is edited.
  - Existing rows receive safe defaults so current reconciliation keeps working.

  IMPORTANT:
  SQL Server compiles a whole batch before executing conditional ALTER TABLE
  statements. Therefore statements that reference columns introduced above are
  executed through sp_executesql so they compile only after the columns exist.
*/
IF OBJECT_ID(N'dbo.F03ExecutionPolicies', N'U') IS NULL
    THROW 51001, 'F03ExecutionPolicies does not exist. Deploy the base execution policy script first.', 1;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'PolicyName') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD PolicyName nvarchar(200) NOT NULL CONSTRAINT DF_F03ExecutionPolicies_PolicyName DEFAULT N''Execution Reconciliation'';');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'PolicyVersion') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD PolicyVersion int NOT NULL CONSTRAINT DF_F03ExecutionPolicies_PolicyVersion DEFAULT 1;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'EmployeeResponseHours') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD EmployeeResponseHours int NOT NULL CONSTRAINT DF_F03ExecutionPolicies_EmployeeResponseHours DEFAULT 48;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'EmployeeTimeoutMode') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD EmployeeTimeoutMode tinyint NOT NULL CONSTRAINT DF_F03ExecutionPolicies_EmployeeTimeoutMode DEFAULT 0;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'HrReviewHours') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD HrReviewHours int NOT NULL CONSTRAINT DF_F03ExecutionPolicies_HrReviewHours DEFAULT 48;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'AllowEmployeeAppeal') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD AllowEmployeeAppeal bit NOT NULL CONSTRAINT DF_F03ExecutionPolicies_AllowEmployeeAppeal DEFAULT 1;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'MaxAppealRounds') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD MaxAppealRounds tinyint NOT NULL CONSTRAINT DF_F03ExecutionPolicies_MaxAppealRounds DEFAULT 1;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'AppealReviewHours') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD AppealReviewHours int NOT NULL CONSTRAINT DF_F03ExecutionPolicies_AppealReviewHours DEFAULT 48;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'RequireEvidenceOnAppeal') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD RequireEvidenceOnAppeal bit NOT NULL CONSTRAINT DF_F03ExecutionPolicies_RequireEvidenceOnAppeal DEFAULT 1;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'RequireFinalDecision') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD RequireFinalDecision bit NOT NULL CONSTRAINT DF_F03ExecutionPolicies_RequireFinalDecision DEFAULT 1;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'FinalDecisionPositionCode') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD FinalDecisionPositionCode nvarchar(20) NULL;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'PayrollCutoffMode') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD PayrollCutoffMode tinyint NOT NULL CONSTRAINT DF_F03ExecutionPolicies_PayrollCutoffMode DEFAULT 0;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'AllowReopenAfterPayroll') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD AllowReopenAfterPayroll bit NOT NULL CONSTRAINT DF_F03ExecutionPolicies_AllowReopenAfterPayroll DEFAULT 0;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'AdjustmentPeriodMode') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD AdjustmentPeriodMode tinyint NOT NULL CONSTRAINT DF_F03ExecutionPolicies_AdjustmentPeriodMode DEFAULT 1;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'EffectiveFrom') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD EffectiveFrom datetime2 NULL;');

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'EffectiveTo') IS NULL
    EXEC(N'ALTER TABLE dbo.F03ExecutionPolicies ADD EffectiveTo datetime2 NULL;');

-- These columns are added by earlier lifecycle migrations. Keep this script
-- idempotent when it is executed against an older database by only backfilling
-- when the required snapshot columns already exist.
IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'PolicyName') IS NOT NULL
AND COL_LENGTH(N'dbo.F03ExecutionPolicies', N'PolicyVersion') IS NOT NULL
AND COL_LENGTH(N'dbo.F03ExecutionPolicies', N'EmployeeResponseHours') IS NOT NULL
AND COL_LENGTH(N'dbo.F03ExecutionPolicies', N'HrReviewHours') IS NOT NULL
AND COL_LENGTH(N'dbo.F03ExecutionPolicies', N'AppealReviewHours') IS NOT NULL
AND COL_LENGTH(N'dbo.F03ExecutionPolicies', N'MaxAppealRounds') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE dbo.F03ExecutionPolicies
        SET PolicyName = CASE WHEN NULLIF(LTRIM(RTRIM(PolicyName)), N'''''''') IS NULL THEN N''Execution Reconciliation'' ELSE PolicyName END,
            PolicyVersion = CASE WHEN PolicyVersion < 1 THEN 1 ELSE PolicyVersion END,
            EmployeeResponseHours = CASE WHEN EmployeeResponseHours <= 0 THEN 48 ELSE EmployeeResponseHours END,
            HrReviewHours = CASE WHEN HrReviewHours <= 0 THEN 48 ELSE HrReviewHours END,
            AppealReviewHours = CASE WHEN AppealReviewHours <= 0 THEN 48 ELSE AppealReviewHours END,
            MaxAppealRounds = CASE WHEN AllowEmployeeAppeal = 1 AND MaxAppealRounds = 0 THEN 1 ELSE MaxAppealRounds END;';
END;

IF OBJECT_ID(N'dbo.F03ExecutionReconciliations', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'EmployeeDecisionStatus') IS NULL
        EXEC(N'ALTER TABLE dbo.F03ExecutionReconciliations ADD EmployeeDecisionStatus nvarchar(40) NOT NULL CONSTRAINT DF_F03ExecutionReconciliations_EmployeeDecisionStatus DEFAULT N''Pending'';');

    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'EmployeeDecisionAt') IS NULL
        EXEC(N'ALTER TABLE dbo.F03ExecutionReconciliations ADD EmployeeDecisionAt datetime2 NULL;');

    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'EmployeeDecisionBy') IS NULL
        EXEC(N'ALTER TABLE dbo.F03ExecutionReconciliations ADD EmployeeDecisionBy int NULL;');

    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'EmployeeDecisionComment') IS NULL
        EXEC(N'ALTER TABLE dbo.F03ExecutionReconciliations ADD EmployeeDecisionComment nvarchar(2000) NULL;');

    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'AppealRound') IS NULL
        EXEC(N'ALTER TABLE dbo.F03ExecutionReconciliations ADD AppealRound tinyint NOT NULL CONSTRAINT DF_F03ExecutionReconciliations_AppealRound DEFAULT 0;');

    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'FinalizedAt') IS NULL
        EXEC(N'ALTER TABLE dbo.F03ExecutionReconciliations ADD FinalizedAt datetime2 NULL;');

    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'FinalizedBy') IS NULL
        EXEC(N'ALTER TABLE dbo.F03ExecutionReconciliations ADD FinalizedBy int NULL;');
END;

-- Backfill legacy reconciliation rows once. From this point onward runtime requires
-- a snapshot so editing a policy can never change an existing case.
-- Dynamic SQL is intentional: ResolutionPolicyId / ResolutionPolicyVersion /
-- ResolutionPolicySnapshotJson may have been introduced by a prior migration in
-- the same deployment sequence.
IF OBJECT_ID(N'dbo.F03ExecutionReconciliations', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'ResolutionPolicyId') IS NOT NULL
AND COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'ResolutionPolicyVersion') IS NOT NULL
AND COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'ResolutionPolicySnapshotJson') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        UPDATE r
           SET ResolutionPolicyId = p.Id,
               ResolutionPolicyVersion = p.PolicyVersion,
               ResolutionPolicySnapshotJson = (
                   SELECT
                       p.ModuleCode,
                       p.PolicyName,
                       p.PolicyVersion,
                       p.EmployeeResponseHours,
                       p.EmployeeTimeoutMode,
                       p.HrReviewHours,
                       p.AllowEmployeeAppeal,
                       p.MaxAppealRounds,
                       p.AppealReviewHours,
                       p.RequireEvidenceOnAppeal,
                       p.RequireFinalDecision,
                       p.FinalDecisionPositionCode,
                       p.PayrollCutoffMode,
                       p.AllowReopenAfterPayroll,
                       p.AdjustmentPeriodMode,
                       p.EffectiveFrom,
                       p.EffectiveTo,
                       p.CorrectionMode
                   FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
               )
        FROM dbo.F03ExecutionReconciliations r
        CROSS APPLY
        (
            SELECT TOP (1) p.*
            FROM dbo.F03ExecutionPolicies p
            WHERE p.ModuleCode = r.ModuleCode
              AND p.IsActive <> 0
            ORDER BY p.PolicyVersion DESC, p.Id DESC
        ) p
        WHERE r.ResolutionPolicySnapshotJson IS NULL;';
END;

-- Register the management capability in the legacy function catalog.
IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode = 3073)
    BEGIN
        IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
            INSERT INTO dbo.F03Functions
                (FunctionCode, FunctionKey, FunctionName, ModuleCode, ActionCode, ScopeCode, LifecycleStatus, IsActive)
            VALUES
                (3073, N'Execution.PolicyManage', N'Execution.PolicyManage', N'Execution', N'PolicyManage', N'All', N'Active', 1);
        ELSE
            INSERT INTO dbo.F03Functions
                (FunctionCode, FunctionName, ModuleCode, ActionCode, ScopeCode, LifecycleStatus, IsActive)
            VALUES
                (3073, N'Execution.PolicyManage', N'Execution', N'PolicyManage', N'All', N'Active', 1);
    END
END;

/* Execution policy management follows the canonical SuperAdmin/Admin capability matrix. */
INSERT INTO dbo.F03RoleFunctions
(
    IdRole, IdFunction, IsActive, CreatedBy, CreatedAt
)
SELECT r.Id, f.Id, 1, 0, GETDATE()
FROM dbo.F03Roles AS r
CROSS JOIN dbo.F03Functions AS f
WHERE r.RoleCode IN (1, 2)
  AND r.IsActive = 1
  AND f.FunctionCode = 3073
  AND f.IsActive = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.F03RoleFunctions AS rf
      WHERE rf.IdRole = r.Id
        AND rf.IdFunction = f.Id
  );

UPDATE rf
SET rf.IsActive = 1
FROM dbo.F03RoleFunctions AS rf
INNER JOIN dbo.F03Roles AS r ON r.IdRole = rf.IdRole
INNER JOIN dbo.F03Functions AS f ON f.Id = rf.IdFunction
WHERE r.RoleCode IN (1, 2)
  AND r.IsActive = 1
  AND f.FunctionCode = 3073
  AND f.IsActive = 1;

IF OBJECT_ID(N'dbo.F03SecurityFunctionRegistry', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM dbo.F03SecurityFunctionRegistry WHERE FunctionKey = N'Execution.PolicyManage')
BEGIN
    INSERT INTO dbo.F03SecurityFunctionRegistry
    (
        FunctionKey, FunctionCode, DefinitionName, ModuleCode, ActionCode, ScopeCode,
        LifecycleStatus, SourceType, DefinitionHash, FirstDiscoveredAt, LastSeenAt, IsIgnored
    )
    SELECT
        N'Execution.PolicyManage', 3073, N'Execution.PolicyManage',
        N'Execution', N'PolicyManage', N'All', N'Active', N'Code',
        CONVERT(varchar(128), HASHBYTES('SHA2_256', N'Execution.PolicyManage|3073|Execution.PolicyManage'), 2),
        GETDATE(), GETDATE(), 0;
END;
