/*
  107 - Seed default LateEarly approval conditions.
  F03BusinessRules is the existing JSON-backed business-rule store; no duplicate policy table.
  Department-specific rows use LATE_EARLY_POLICY_{DeptCode}; code 0 is company fallback.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.F03BusinessRules', N'U') IS NULL
    THROW 51701, N'F03BusinessRules is required before seeding LateEarly approval conditions.', 1;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.F03BusinessRules
    WHERE Module = N'LateEarly' AND Code = N'LATE_EARLY_POLICY_0'
)
BEGIN
    INSERT dbo.F03BusinessRules
    (
        IsActive, CreatedBy, LastModifiedSource, CreatedAt,
        Module, Code, Name, ConfigJson
    )
    VALUES
    (
        1, 0, N'107_LATE_EARLY_APPROVAL_POLICY', GETDATE(),
        N'LateEarly', N'LATE_EARLY_POLICY_0',
        N'Chính sách Đi muộn / Về sớm mặc định',
        N'{"DeptCode":0,"MonthlyLimit":0,"RequireLevel2ForRetroactive":true,"EarlyLeaveLevel2BeforeMinutes":0,"IsActive":true,"IsFallback":true}'
    );
END;

PRINT N'107_LateEarlyApprovalPolicy: default company policy seeded.';
