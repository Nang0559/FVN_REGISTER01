/*
  Execution resolution policy
  - Extends existing F03ExecutionPolicies; no second policy table is introduced.
  - Version is bumped when an active policy is edited.
  - Existing rows receive safe defaults so current reconciliation keeps working.
*/
IF OBJECT_ID(N'dbo.F03ExecutionPolicies', N'U') IS NULL
    THROW 51001, 'F03ExecutionPolicies does not exist. Deploy the base execution policy script first.', 1;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'PolicyName') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD PolicyName nvarchar(200) NOT NULL CONSTRAINT DF_F03ExecutionPolicies_PolicyName DEFAULT N'Execution Reconciliation';

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'PolicyVersion') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD PolicyVersion int NOT NULL CONSTRAINT DF_F03ExecutionPolicies_PolicyVersion DEFAULT 1;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'EmployeeResponseHours') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD EmployeeResponseHours int NOT NULL CONSTRAINT DF_F03ExecutionPolicies_EmployeeResponseHours DEFAULT 48;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'HrReviewHours') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD HrReviewHours int NOT NULL CONSTRAINT DF_F03ExecutionPolicies_HrReviewHours DEFAULT 48;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'AllowEmployeeAppeal') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD AllowEmployeeAppeal bit NOT NULL CONSTRAINT DF_F03ExecutionPolicies_AllowEmployeeAppeal DEFAULT 1;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'MaxAppealRounds') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD MaxAppealRounds tinyint NOT NULL CONSTRAINT DF_F03ExecutionPolicies_MaxAppealRounds DEFAULT 1;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'AppealReviewHours') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD AppealReviewHours int NOT NULL CONSTRAINT DF_F03ExecutionPolicies_AppealReviewHours DEFAULT 48;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'RequireEvidenceOnAppeal') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD RequireEvidenceOnAppeal bit NOT NULL CONSTRAINT DF_F03ExecutionPolicies_RequireEvidenceOnAppeal DEFAULT 1;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'RequireFinalDecision') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD RequireFinalDecision bit NOT NULL CONSTRAINT DF_F03ExecutionPolicies_RequireFinalDecision DEFAULT 1;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'FinalDecisionPositionCode') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD FinalDecisionPositionCode nvarchar(20) NULL;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'PayrollCutoffMode') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD PayrollCutoffMode tinyint NOT NULL CONSTRAINT DF_F03ExecutionPolicies_PayrollCutoffMode DEFAULT 0;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'AllowReopenAfterPayroll') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD AllowReopenAfterPayroll bit NOT NULL CONSTRAINT DF_F03ExecutionPolicies_AllowReopenAfterPayroll DEFAULT 0;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'AdjustmentPeriodMode') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD AdjustmentPeriodMode tinyint NOT NULL CONSTRAINT DF_F03ExecutionPolicies_AdjustmentPeriodMode DEFAULT 1;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'EffectiveFrom') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD EffectiveFrom datetime2 NULL;

IF COL_LENGTH(N'dbo.F03ExecutionPolicies', N'EffectiveTo') IS NULL
    ALTER TABLE dbo.F03ExecutionPolicies ADD EffectiveTo datetime2 NULL;

UPDATE dbo.F03ExecutionPolicies
SET PolicyName = CASE WHEN NULLIF(LTRIM(RTRIM(PolicyName)), N'') IS NULL THEN N'Execution Reconciliation' ELSE PolicyName END,
    PolicyVersion = CASE WHEN PolicyVersion < 1 THEN 1 ELSE PolicyVersion END,
    EmployeeResponseHours = CASE WHEN EmployeeResponseHours <= 0 THEN 48 ELSE EmployeeResponseHours END,
    HrReviewHours = CASE WHEN HrReviewHours <= 0 THEN 48 ELSE HrReviewHours END,
    AppealReviewHours = CASE WHEN AppealReviewHours <= 0 THEN 48 ELSE AppealReviewHours END,
    MaxAppealRounds = CASE WHEN AllowEmployeeAppeal = 1 AND MaxAppealRounds = 0 THEN 1 ELSE MaxAppealRounds END;

IF OBJECT_ID(N'dbo.F03ExecutionReconciliations', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'EmployeeDecisionStatus') IS NULL
        ALTER TABLE dbo.F03ExecutionReconciliations ADD EmployeeDecisionStatus nvarchar(40) NOT NULL CONSTRAINT DF_F03ExecutionReconciliations_EmployeeDecisionStatus DEFAULT N'Pending';

    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'EmployeeDecisionAt') IS NULL
        ALTER TABLE dbo.F03ExecutionReconciliations ADD EmployeeDecisionAt datetime2 NULL;

    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'EmployeeDecisionBy') IS NULL
        ALTER TABLE dbo.F03ExecutionReconciliations ADD EmployeeDecisionBy int NULL;

    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'EmployeeDecisionComment') IS NULL
        ALTER TABLE dbo.F03ExecutionReconciliations ADD EmployeeDecisionComment nvarchar(2000) NULL;

    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'AppealRound') IS NULL
        ALTER TABLE dbo.F03ExecutionReconciliations ADD AppealRound tinyint NOT NULL CONSTRAINT DF_F03ExecutionReconciliations_AppealRound DEFAULT 0;

    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'FinalizedAt') IS NULL
        ALTER TABLE dbo.F03ExecutionReconciliations ADD FinalizedAt datetime2 NULL;

    IF COL_LENGTH(N'dbo.F03ExecutionReconciliations', N'FinalizedBy') IS NULL
        ALTER TABLE dbo.F03ExecutionReconciliations ADD FinalizedBy int NULL;
END;
