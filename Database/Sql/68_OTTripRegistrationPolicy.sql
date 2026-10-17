/*
    OT / Trip registrar policy
    1 = everyone in the department
    2 = explicitly assigned users
    3 = assigned HRM positions
    DepartmentCode = 0 is the fallback rule.
    RegisterType: OT / TRIP
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.F03OTRegistrationRules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03OTRegistrationRules
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03OTRegistrationRules PRIMARY KEY,
        DepartmentCode INT NOT NULL,
        RegisterType NVARCHAR(20) NOT NULL CONSTRAINT DF_F03OTRegistrationRules_RegisterType DEFAULT N'OT',
        Mode INT NOT NULL CONSTRAINT DF_F03OTRegistrationRules_Mode DEFAULT 3,
        IsActive BIT NOT NULL CONSTRAINT DF_F03OTRegistrationRules_IsActive DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03OTRegistrationRules_CreatedAt DEFAULT SYSDATETIME(),
        CreatedBy INT NULL,
        ModifiedAt DATETIME2 NULL,
        ModifiedBy INT NULL,
        CONSTRAINT CK_F03OTRegistrationRules_Mode CHECK (Mode IN (1,2,3)),
        CONSTRAINT CK_F03OTRegistrationRules_RegisterType CHECK (RegisterType IN (N'OT',N'TRIP'))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_F03OTRegistrationRules_DeptType' AND object_id = OBJECT_ID(N'dbo.F03OTRegistrationRules'))
    CREATE UNIQUE INDEX UX_F03OTRegistrationRules_DeptType ON dbo.F03OTRegistrationRules(DepartmentCode, RegisterType);

IF OBJECT_ID(N'dbo.F03OTRegistrars', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03OTRegistrars
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03OTRegistrars PRIMARY KEY,
        RegistrationRuleId INT NOT NULL,
        DepartmentCode INT NOT NULL,
        UserId INT NOT NULL,
        RegisterType NVARCHAR(20) NOT NULL CONSTRAINT DF_F03OTRegistrars_RegisterType DEFAULT N'OT',
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03OTRegistrars_CreatedAt DEFAULT SYSDATETIME(),
        CreatedBy INT NULL,
        ModifiedAt DATETIME2 NULL,
        ModifiedBy INT NULL,
        CONSTRAINT FK_F03OTRegistrars_Rule FOREIGN KEY (RegistrationRuleId) REFERENCES dbo.F03OTRegistrationRules(Id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_F03OTRegistrars_RuleUserType' AND object_id = OBJECT_ID(N'dbo.F03OTRegistrars'))
    CREATE UNIQUE INDEX UX_F03OTRegistrars_RuleUserType ON dbo.F03OTRegistrars(RegistrationRuleId, UserId, RegisterType);

IF OBJECT_ID(N'dbo.F03OTRegistrarPositions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03OTRegistrarPositions
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03OTRegistrarPositions PRIMARY KEY,
        RegistrationRuleId INT NOT NULL,
        DepartmentCode INT NOT NULL,
        PositionCode NVARCHAR(20) NOT NULL,
        RegisterType NVARCHAR(20) NOT NULL CONSTRAINT DF_F03OTRegistrarPositions_RegisterType DEFAULT N'OT',
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03OTRegistrarPositions_CreatedAt DEFAULT SYSDATETIME(),
        CreatedBy INT NULL,
        ModifiedAt DATETIME2 NULL,
        ModifiedBy INT NULL,
        CONSTRAINT FK_F03OTRegistrarPositions_Rule FOREIGN KEY (RegistrationRuleId) REFERENCES dbo.F03OTRegistrationRules(Id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_F03OTRegistrarPositions_RulePositionType' AND object_id = OBJECT_ID(N'dbo.F03OTRegistrarPositions'))
    CREATE UNIQUE INDEX UX_F03OTRegistrarPositions_RulePositionType ON dbo.F03OTRegistrarPositions(RegistrationRuleId, PositionCode, RegisterType);

IF OBJECT_ID(N'dbo.F03SupervisorFeedbacks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03SupervisorFeedbacks
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03SupervisorFeedbacks PRIMARY KEY,
        EmployeeId INT NOT NULL,
        EmployeeCode NVARCHAR(50) NOT NULL,
        WorkDate DATE NOT NULL,
        ActualIn DATETIME2 NULL,
        ActualOut DATETIME2 NULL,
        DepartmentCode INT NULL,
        Status INT NOT NULL CONSTRAINT DF_F03SupervisorFeedbacks_Status DEFAULT 1,
        ApproverId INT NULL,
        Note NVARCHAR(1000) NULL,
        OTRequestId INT NULL,
        ResolvedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03SupervisorFeedbacks_CreatedAt DEFAULT SYSDATETIME(),
        CreatedBy INT NULL,
        ModifiedAt DATETIME2 NULL,
        ModifiedBy INT NULL,
        CONSTRAINT CK_F03SupervisorFeedbacks_Status CHECK (Status IN (0,1,2,3,4))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03SupervisorFeedbacks_ApproverStatusDate' AND object_id = OBJECT_ID(N'dbo.F03SupervisorFeedbacks'))
    CREATE INDEX IX_F03SupervisorFeedbacks_ApproverStatusDate ON dbo.F03SupervisorFeedbacks(ApproverId, Status, WorkDate);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03SupervisorFeedbacks_EmployeeDateStatus' AND object_id = OBJECT_ID(N'dbo.F03SupervisorFeedbacks'))
    CREATE INDEX IX_F03SupervisorFeedbacks_EmployeeDateStatus ON dbo.F03SupervisorFeedbacks(EmployeeId, WorkDate, Status);

/* Default fallback: use the positions already configured as approver positions. */
DECLARE @RuleId INT;
SELECT @RuleId = Id FROM dbo.F03OTRegistrationRules WHERE DepartmentCode = 0 AND RegisterType = N'OT';
IF @RuleId IS NULL
BEGIN
    INSERT dbo.F03OTRegistrationRules(DepartmentCode, RegisterType, Mode, IsActive)
    VALUES (0, N'OT', 3, 1);
    SET @RuleId = SCOPE_IDENTITY();
END;

INSERT dbo.F03OTRegistrarPositions(RegistrationRuleId, DepartmentCode, PositionCode, RegisterType)
SELECT DISTINCT @RuleId, 0, p.ApprovalPositionCode, N'OT'
FROM dbo.F03ApprovalPolicies p
WHERE p.RequestType = 1
  AND NULLIF(LTRIM(RTRIM(p.ApprovalPositionCode)), N'') IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.F03OTRegistrarPositions rp
      WHERE rp.RegistrationRuleId = @RuleId
        AND rp.PositionCode = p.ApprovalPositionCode
        AND rp.RegisterType = N'OT'
  );

IF NOT EXISTS (SELECT 1 FROM dbo.F03OTRegistrationRules WHERE DepartmentCode = 0 AND RegisterType = N'TRIP')
    INSERT dbo.F03OTRegistrationRules(DepartmentCode, RegisterType, Mode, IsActive)
    VALUES (0, N'TRIP', 3, 1);

DECLARE @TripRuleId INT = (SELECT Id FROM dbo.F03OTRegistrationRules WHERE DepartmentCode = 0 AND RegisterType = N'TRIP');
INSERT dbo.F03OTRegistrarPositions(RegistrationRuleId, DepartmentCode, PositionCode, RegisterType)
SELECT DISTINCT @TripRuleId, 0, p.ApprovalPositionCode, N'TRIP'
FROM dbo.F03ApprovalPolicies p
WHERE p.RequestType = 2
  AND NULLIF(LTRIM(RTRIM(p.ApprovalPositionCode)), N'') IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.F03OTRegistrarPositions rp
      WHERE rp.RegistrationRuleId = @TripRuleId
        AND rp.PositionCode = p.ApprovalPositionCode
        AND rp.RegisterType = N'TRIP'
  );

COMMIT TRANSACTION;
GO

/*
   DB guard: if an OT request contains employees other than the requester,
   the requester must match the resolved registrar rule.
*/
CREATE OR ALTER TRIGGER dbo.trg_F03OTEmployees_RegistrarPolicy
ON dbo.F03OTEmployees
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.F03OTRequests q ON q.Id = i.OTRequestId
        CROSS APPLY
        (
            SELECT TOP (1) rr.*
            FROM dbo.F03OTRegistrationRules rr
            WHERE rr.IsActive = 1
              AND rr.RegisterType = N'OT'
              AND rr.DepartmentCode IN (ISNULL(q.DeptCode, 0), 0)
            ORDER BY CASE WHEN rr.DepartmentCode = ISNULL(q.DeptCode, 0) THEN 0 ELSE 1 END
        ) policy_rule
        OUTER APPLY
        (
            SELECT TOP (1) u.EmployeeCode, e.PositionCode
            FROM dbo.F03Users u
            LEFT JOIN dbo.F03Employees e ON e.EmployeeCode = u.EmployeeCode
            WHERE u.Id = q.CreatedBy
        ) creator_info
        WHERE UPPER(LTRIM(RTRIM(i.EmployeeCode))) <> UPPER(LTRIM(RTRIM(ISNULL(creator_info.EmployeeCode, N''))))
          AND NOT
          (
              policy_rule.Mode = 1
              OR (policy_rule.Mode = 2 AND EXISTS (SELECT 1 FROM dbo.F03OTRegistrars x WHERE x.RegistrationRuleId = policy_rule.Id AND x.UserId = q.CreatedBy AND x.RegisterType = N'OT'))
              OR (policy_rule.Mode = 3 AND EXISTS (SELECT 1 FROM dbo.F03OTRegistrarPositions x WHERE x.RegistrationRuleId = policy_rule.Id AND x.PositionCode = creator_info.PositionCode AND x.RegisterType = N'OT'))
          )
    )
    BEGIN
        THROW 51531, N'Người đăng ký không có quyền đăng ký OT cho nhân viên khác theo policy của phòng ban.', 1;
    END;
END;
GO

/* DB guard for Trip multi-person registration through CompanionEmployeeCodes. */
CREATE OR ALTER TRIGGER dbo.trg_F03TripRequests_RegistrarPolicy
ON dbo.F03TripRequests
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted q
        CROSS APPLY
        (
            SELECT TOP (1) rr.*
            FROM dbo.F03OTRegistrationRules rr
            WHERE rr.IsActive = 1
              AND rr.RegisterType = N'TRIP'
              AND rr.DepartmentCode IN (ISNULL(q.DeptCode, 0), 0)
            ORDER BY CASE WHEN rr.DepartmentCode = ISNULL(q.DeptCode, 0) THEN 0 ELSE 1 END
        ) policy_rule
        OUTER APPLY
        (
            SELECT TOP (1) u.EmployeeCode, e.PositionCode
            FROM dbo.F03Users u
            LEFT JOIN dbo.F03Employees e ON e.EmployeeCode = u.EmployeeCode
            WHERE u.Id = q.CreatedBy
        ) creator_info
        WHERE NULLIF(LTRIM(RTRIM(ISNULL(q.CompanionEmployeeCodes, N''))), N'') IS NOT NULL
          AND NOT
          (
              policy_rule.Mode = 1
              OR (policy_rule.Mode = 2 AND EXISTS (SELECT 1 FROM dbo.F03OTRegistrars x WHERE x.RegistrationRuleId = policy_rule.Id AND x.UserId = q.CreatedBy AND x.RegisterType = N'TRIP'))
              OR (policy_rule.Mode = 3 AND EXISTS (SELECT 1 FROM dbo.F03OTRegistrarPositions x WHERE x.RegistrationRuleId = policy_rule.Id AND x.PositionCode = creator_info.PositionCode AND x.RegisterType = N'TRIP'))
          )
    )
    BEGIN
        THROW 51532, N'Người đăng ký không có quyền đăng ký công tác cho người khác theo policy của phòng ban.', 1;
    END;
END;
GO
