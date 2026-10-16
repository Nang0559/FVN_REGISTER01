USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

/*
  FVN_REGISTER - Lịch chạy background job chỉnh được từ UI (Admin > Lịch chạy background).

  1. dbo.F03BackgroundJobSchedules: mỗi job một dòng (bật/tắt, chu kỳ phút, yêu cầu chạy ngay,
     kết quả lần chạy gần nhất).
  2. Function 3301 BackgroundJob.View / 3302 BackgroundJob.Manage, cấp cho Role 1,2.
  3. Seed 8 job với chu kỳ mặc định (HRM sync 30 phút).

  Idempotent, có thể chạy lại. Nếu bảng chưa tồn tại, API vẫn chạy với chu kỳ mặc định trong code.
  Cấu hình HrmSync:PollMinutes trong appsettings KHÔNG còn được dùng - chu kỳ lấy từ bảng này.
*/

IF OBJECT_ID(N'dbo.F03BackgroundJobSchedules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03BackgroundJobSchedules
    (
        JobKey                 nvarchar(100) NOT NULL CONSTRAINT PK_F03BackgroundJobSchedules PRIMARY KEY,
        DisplayName            nvarchar(200) NOT NULL,
        Description            nvarchar(500) NULL,
        IsEnabled              bit           NOT NULL CONSTRAINT DF_F03BackgroundJobSchedules_IsEnabled DEFAULT (1),
        IntervalMinutes        int           NOT NULL CONSTRAINT DF_F03BackgroundJobSchedules_Interval DEFAULT (30),
        DefaultIntervalMinutes int           NOT NULL CONSTRAINT DF_F03BackgroundJobSchedules_DefaultInterval DEFAULT (30),
        RunRequested           bit           NOT NULL CONSTRAINT DF_F03BackgroundJobSchedules_RunRequested DEFAULT (0),
        LastStartedAt          datetime2(0)  NULL,
        LastFinishedAt         datetime2(0)  NULL,
        LastStatus             nvarchar(20)  NULL,
        LastDurationMs         int           NULL,
        LastMessage            nvarchar(500) NULL,
        ModifiedBy             int           NULL,
        ModifiedAt             datetime2(0)  NULL,
        CONSTRAINT CK_F03BackgroundJobSchedules_Interval CHECK (IntervalMinutes BETWEEN 1 AND 10080)
    );
END;
GO

/* Seed: chỉ thêm job chưa có; không đè cấu hình người dùng đã chỉnh. Giữ khớp BackgroundJobCatalog.cs */
;WITH src(JobKey, DisplayName, Description, IntervalMinutes) AS
(
    SELECT * FROM (VALUES
        (N'hrm-sync',                    N'Đồng bộ HRM',                      N'Import HRM, sync Department/Employee/..., provision User + Approver. Tác vụ nặng nhất.', 30),
        (N'email-queue',                 N'Gửi email trong hàng đợi',         N'Xử lý hàng đợi email.', 5),
        (N'escalation',                  N'Leo thang phê duyệt (Leave/OT)',   N'Quét yêu cầu quá hạn phê duyệt và leo thang.', 15),
        (N'action-item-lifecycle',       N'Vòng đời Action Item',             N'Chuyển Action Item quá hạn, sửa dữ liệu mồ côi.', 10),
        (N'equipment-inspection',        N'Sinh lịch kiểm tra thiết bị',      N'Tạo task kiểm tra thiết bị định kỳ.', 10),
        (N'execution-reconciliation',    N'Đối soát thực thi công',           N'Đối soát chấm công theo ExecutionPolicy.', 30),
        (N'endpoint-credential-cleanup', N'Dọn credential endpoint hết hạn',  N'Thu hồi credential hết thời gian grace (tác vụ bảo mật, không được tắt).', 15),
        (N'security-function-discovery', N'Quét function bảo mật',            N'Đối chiếu SecurityFunction trong code với DB (không được tắt).', 360)
    ) v(JobKey, DisplayName, Description, IntervalMinutes)
)
INSERT dbo.F03BackgroundJobSchedules (JobKey, DisplayName, Description, IntervalMinutes, DefaultIntervalMinutes)
SELECT s.JobKey, s.DisplayName, s.Description, s.IntervalMinutes, s.IntervalMinutes
FROM src s
WHERE NOT EXISTS (SELECT 1 FROM dbo.F03BackgroundJobSchedules j WHERE j.JobKey = s.JobKey);
GO

/* ---------------- Function 3301 / 3302 ---------------- */
IF OBJECT_ID(N'dbo.F03Functions', N'U') IS NULL
    THROW 53301, N'F03Functions is required before seeding BackgroundJob functions.', 1;
GO

DECLARE @Seed TABLE (Code int, Name nvarchar(200), Detail nvarchar(500), ActionCode nvarchar(100));
INSERT @Seed VALUES
    (3301, N'BackgroundJob.View',   N'Xem lịch chạy background job',     N'View'),
    (3302, N'BackgroundJob.Manage', N'Quản lý lịch chạy background job', N'Manage');

DECLARE @code int, @name nvarchar(200), @detail nvarchar(500), @action nvarchar(100);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Code, Name, Detail, ActionCode FROM @Seed;
OPEN c;
FETCH NEXT FROM c INTO @code, @name, @detail, @action;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode = @code)
    BEGIN
        IF COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
            INSERT INTO dbo.F03Functions
                (FunctionCode, FunctionKey, FunctionName, Detail, ModuleCode, ActionCode, ScopeCode, LifecycleStatus, IsActive)
            VALUES (@code, @name, @name, @detail, N'BackgroundJob', @action, N'All', N'Active', 1);
        ELSE
            INSERT INTO dbo.F03Functions
                (FunctionCode, FunctionName, Detail, ModuleCode, ActionCode, ScopeCode, LifecycleStatus, IsActive)
            VALUES (@code, @name, @detail, N'BackgroundJob', @action, N'All', N'Active', 1);
    END;

    IF OBJECT_ID(N'dbo.F03SecurityFunctionRegistry', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.F03Functions', N'FunctionKey') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.F03SecurityFunctionRegistry WHERE FunctionKey = @name)
    BEGIN
        INSERT INTO dbo.F03SecurityFunctionRegistry
        (
            FunctionKey, FunctionCode, DefinitionName, ModuleCode, ActionCode, ScopeCode,
            LifecycleStatus, SourceType, DefinitionHash, FirstDiscoveredAt, LastSeenAt, IsIgnored
        )
        VALUES
        (
            @name, @code, @name, N'BackgroundJob', @action, N'All', N'Active', N'SqlSeed',
            CONVERT(varchar(128), HASHBYTES('SHA2_256', @name + N'|' + CONVERT(nvarchar(20), @code) + N'|' + @name), 2),
            GETDATE(), GETDATE(), 0
        );
    END;

    FETCH NEXT FROM c INTO @code, @name, @detail, @action;
END;
CLOSE c;
DEALLOCATE c;
GO

INSERT INTO dbo.F03RoleFunctions (IdRole, IdFunction, IsActive, CreatedBy, CreatedAt)
SELECT r.Id, f.Id, 1, 0, GETDATE()
FROM dbo.F03Roles AS r
CROSS JOIN dbo.F03Functions AS f
WHERE r.RoleCode IN (1, 2)
  AND r.IsActive = 1
  AND f.FunctionCode IN (3301, 3302)
  AND f.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.F03RoleFunctions rf WHERE rf.IdRole = r.Id AND rf.IdFunction = f.Id);

UPDATE rf
SET rf.IsActive = 1
FROM dbo.F03RoleFunctions AS rf
INNER JOIN dbo.F03Roles AS r ON r.Id = rf.IdRole
INNER JOIN dbo.F03Functions AS f ON f.Id = rf.IdFunction
WHERE r.RoleCode IN (1, 2)
  AND r.IsActive = 1
  AND f.FunctionCode IN (3301, 3302)
  AND (rf.IsActive IS NULL OR rf.IsActive = 0);
GO

SELECT JobKey, IsEnabled, IntervalMinutes, DefaultIntervalMinutes FROM dbo.F03BackgroundJobSchedules ORDER BY JobKey;
SELECT FunctionCode, FunctionName FROM dbo.F03Functions WHERE FunctionCode IN (3301, 3302);
GO
