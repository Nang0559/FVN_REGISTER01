/*
===============================================================================
105 - Late arrival / early leave registration (Đăng ký đi muộn / về sớm)

Design:
  * Workers may not use mobile devices => a supervisor (registrar) registers ON BEHALF.
  * "Who is the registrar of each department" REUSES the existing policy tables
    F03OTRegistrationRules / F03OTRegistrars / F03OTRegistrarPositions with the new
    RegisterType = 'LATE_EARLY' (and the Feature Operator OT.RegistrarManage assignment).
  * Approval flow reuses F03ApprovalPolicies / F03Approvers with RequestType = 'LateEarly'.
Idempotent: safe to re-run.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

/* 1) Allow RegisterType = LATE_EARLY in the registrar policy rule table */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_F03OTRegistrationRules_RegisterType'
           AND parent_object_id = OBJECT_ID(N'dbo.F03OTRegistrationRules'))
    ALTER TABLE dbo.F03OTRegistrationRules DROP CONSTRAINT CK_F03OTRegistrationRules_RegisterType;

ALTER TABLE dbo.F03OTRegistrationRules
    ADD CONSTRAINT CK_F03OTRegistrationRules_RegisterType
    CHECK (RegisterType IN (N'OT', N'TRIP', N'LATE_EARLY'));

/* 2) Request table (same audit columns as F03TripRequests) */
IF OBJECT_ID(N'dbo.F03LateEarlyRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03LateEarlyRequests
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03LateEarlyRequests PRIMARY KEY,
        EmployeeCode NVARCHAR(50) NOT NULL,                 -- công nhân được đăng ký
        DeptCode INT NULL,
        RequestStatus INT NOT NULL CONSTRAINT DF_F03LateEarlyRequests_RequestStatus DEFAULT(0),
        IsActive BIT NULL CONSTRAINT DF_F03LateEarlyRequests_IsActive DEFAULT(1),
        CreatedBy INT NOT NULL,                              -- UserId người bấm đăng ký
        LastModifiedSource NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03LateEarlyRequests_CreatedAt DEFAULT(GETDATE()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2 NULL,

        RequestCode NVARCHAR(30) NOT NULL,
        LateEarlyType TINYINT NOT NULL,                      -- 1 = Đi muộn, 2 = Về sớm
        WorkDate DATE NOT NULL,
        PlannedTime TIME(0) NOT NULL,                        -- giờ dự kiến đến / rời xưởng
        ReasonCode NVARCHAR(30) NOT NULL,
        ReasonNote NVARCHAR(500) NULL,
        RegisteredByEmployeeCode NVARCHAR(50) NOT NULL,      -- người đăng ký hộ (có thể = EmployeeCode)
        IsOnBehalf BIT NOT NULL CONSTRAINT DF_F03LateEarlyRequests_IsOnBehalf DEFAULT(0),
        IsRetroactive BIT NOT NULL CONSTRAINT DF_F03LateEarlyRequests_IsRetro DEFAULT(0), -- đăng ký bổ sung sau giờ
        CancelReason NVARCHAR(500) NULL,
        CONSTRAINT CK_F03LateEarlyRequests_Type CHECK (LateEarlyType IN (1,2))
    );
END;

-- Actual punch time is never entered manually. It is read from F03HrmAttendanceCalculated
-- (or F03HrmAttendanceHistory when the current row is absent). Remove obsolete manual fields
-- when this script is upgraded from an earlier draft of the schema.
IF COL_LENGTH(N'dbo.F03LateEarlyRequests', N'ActualTime') IS NOT NULL
    ALTER TABLE dbo.F03LateEarlyRequests DROP COLUMN ActualTime;
IF COL_LENGTH(N'dbo.F03LateEarlyRequests', N'ConfirmedBy') IS NOT NULL
    ALTER TABLE dbo.F03LateEarlyRequests DROP COLUMN ConfirmedBy;
IF COL_LENGTH(N'dbo.F03LateEarlyRequests', N'ConfirmedAt') IS NOT NULL
    ALTER TABLE dbo.F03LateEarlyRequests DROP COLUMN ConfirmedAt;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_F03LateEarlyRequests_RequestCode' AND object_id = OBJECT_ID(N'dbo.F03LateEarlyRequests'))
    CREATE UNIQUE INDEX UX_F03LateEarlyRequests_RequestCode ON dbo.F03LateEarlyRequests(RequestCode);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03LateEarlyRequests_Employee_Date' AND object_id = OBJECT_ID(N'dbo.F03LateEarlyRequests'))
    CREATE INDEX IX_F03LateEarlyRequests_Employee_Date ON dbo.F03LateEarlyRequests(EmployeeCode, WorkDate, LateEarlyType) WHERE IsActive = 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03LateEarlyRequests_Dept_Date_Status' AND object_id = OBJECT_ID(N'dbo.F03LateEarlyRequests'))
    CREATE INDEX IX_F03LateEarlyRequests_Dept_Date_Status ON dbo.F03LateEarlyRequests(DeptCode, WorkDate, RequestStatus) WHERE IsActive = 1;

-- Historical rejected/cancelled requests are retained but excluded from the live-request key.
UPDATE dbo.F03LateEarlyRequests
SET IsActive = 0
WHERE IsActive = 1 AND RequestStatus IN (4,5);

IF EXISTS
(
    SELECT 1
    FROM dbo.F03LateEarlyRequests
    WHERE IsActive = 1
    GROUP BY EmployeeCode, WorkDate, LateEarlyType
    HAVING COUNT(*) > 1
)
    THROW 51505, N'Có đăng ký Đi muộn/Về sớm đang hoạt động bị trùng nhân viên + ngày + loại. Hãy rà soát dữ liệu trước khi tạo unique index.', 1;

-- One live request per employee/date/type. Cancelled and rejected rows are deactivated
-- by the application, so the filtered unique index also closes the concurrent-create race.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_F03LateEarlyRequests_Employee_Date_Type_Active' AND object_id = OBJECT_ID(N'dbo.F03LateEarlyRequests'))
    CREATE UNIQUE INDEX UX_F03LateEarlyRequests_Employee_Date_Type_Active
        ON dbo.F03LateEarlyRequests(EmployeeCode, WorkDate, LateEarlyType)
        WHERE IsActive = 1;

/* 3) Chặn trùng: một công nhân chỉ có 1 đơn còn hiệu lực / loại / ngày
      (RequestStatus: 4 = Cancelled, 3 = Rejected cần khớp enum ApprovalStatus - kiểm tra trước khi deploy) */
-- Kiểm tra trùng thực hiện ở tầng service (xem LateEarlyService.ValidateNoDuplicateAsync).

/* 4) Fallback registrar rule (DepartmentCode = 0): mặc định lấy cùng chức vụ đã được phép đăng ký OT.
      HR có thể chỉnh lại theo từng bộ phận ở màn hình Quản lý người đăng ký. */
IF NOT EXISTS (SELECT 1 FROM dbo.F03OTRegistrationRules WHERE DepartmentCode = 0 AND RegisterType = N'LATE_EARLY')
    INSERT dbo.F03OTRegistrationRules(DepartmentCode, RegisterType, Mode, IsActive)
    VALUES (0, N'LATE_EARLY', 3, 1);

DECLARE @LeRuleId INT = (SELECT Id FROM dbo.F03OTRegistrationRules WHERE DepartmentCode = 0 AND RegisterType = N'LATE_EARLY');
DECLARE @OtRuleId INT = (SELECT Id FROM dbo.F03OTRegistrationRules WHERE DepartmentCode = 0 AND RegisterType = N'OT');

INSERT dbo.F03OTRegistrarPositions(RegistrationRuleId, DepartmentCode, PositionCode, RegisterType)
SELECT @LeRuleId, 0, p.PositionCode, N'LATE_EARLY'
FROM dbo.F03OTRegistrarPositions p
WHERE p.RegistrationRuleId = @OtRuleId AND p.RegisterType = N'OT'
  AND NOT EXISTS (SELECT 1 FROM dbo.F03OTRegistrarPositions x
                  WHERE x.RegistrationRuleId = @LeRuleId AND x.PositionCode = p.PositionCode AND x.RegisterType = N'LATE_EARLY');

/* 5) Make the module visible to the shared calendar provider registry. */
IF OBJECT_ID(N'dbo.F03CalendarModuleDefinitions', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.F03CalendarModuleDefinitions WHERE ModuleCode = N'LATE_EARLY')
BEGIN
    INSERT dbo.F03CalendarModuleDefinitions(ModuleCode, ModuleName, SupportsCalendar, DefaultEnabled)
    VALUES (N'LATE_EARLY', N'Đi muộn / Về sớm', 1, 1);
END;

IF OBJECT_ID(N'dbo.F03CalendarModulePolicies', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.F03CalendarModulePolicies WHERE ModuleCode = N'LATE_EARLY')
BEGIN
    INSERT dbo.F03CalendarModulePolicies(ModuleCode, IsEnabled)
    VALUES (N'LATE_EARLY', 1);
END;

COMMIT TRANSACTION;
