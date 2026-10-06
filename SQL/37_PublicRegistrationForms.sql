/* FVN_REGISTER - Public Registration Forms v1. Separate from F03PublicInformation. */
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.F03PublicForms', N'U') IS NULL
BEGIN
CREATE TABLE dbo.F03PublicForms(
 Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03PublicForms PRIMARY KEY,
 FormCode nvarchar(50) NOT NULL, Title nvarchar(300) NOT NULL, Description nvarchar(2000) NULL, CategoryCode nvarchar(50) NULL,
 Status nvarchar(20) NOT NULL CONSTRAINT DF_F03PublicForms_Status DEFAULT N'Draft',
 StartAt datetime2 NULL, EndAt datetime2 NULL,
 AllowMultipleSubmit bit NOT NULL CONSTRAINT DF_F03PublicForms_AllowMultiple DEFAULT 0,
 RequireApproval bit NOT NULL CONSTRAINT DF_F03PublicForms_RequireApproval DEFAULT 0,
 MaxSubmissions int NULL, Version int NOT NULL CONSTRAINT DF_F03PublicForms_Version DEFAULT 1,
 CreatedBy int NOT NULL, CreatedAt datetime2 NOT NULL CONSTRAINT DF_F03PublicForms_CreatedAt DEFAULT GETDATE(),
 ModifiedBy int NULL, ModifiedAt datetime2 NULL, LastModifiedSource nvarchar(max) NULL, PublishedAt datetime2 NULL, ClosedAt datetime2 NULL,
 IsActive bit NULL CONSTRAINT DF_F03PublicForms_IsActive DEFAULT 1,
 CONSTRAINT UQ_F03PublicForms_FormCode UNIQUE(FormCode),
 CONSTRAINT CK_F03PublicForms_Status CHECK(Status IN (N'Draft',N'Published',N'Closed',N'Archived')),
 CONSTRAINT CK_F03PublicForms_DateRange CHECK(EndAt IS NULL OR StartAt IS NULL OR EndAt >= StartAt),
 CONSTRAINT CK_F03PublicForms_Max CHECK(MaxSubmissions IS NULL OR MaxSubmissions > 0)
); END;

IF OBJECT_ID(N'dbo.F03PublicFormQuestions', N'U') IS NULL
BEGIN
CREATE TABLE dbo.F03PublicFormQuestions(
 Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03PublicFormQuestions PRIMARY KEY,
 FormId int NOT NULL, QuestionCode nvarchar(50) NOT NULL, QuestionText nvarchar(1000) NOT NULL,
 QuestionType nvarchar(30) NOT NULL, HelpText nvarchar(1000) NULL, Placeholder nvarchar(300) NULL,
 IsRequired bit NOT NULL CONSTRAINT DF_F03PublicFormQuestions_IsRequired DEFAULT 0,
 Sequence int NOT NULL CONSTRAINT DF_F03PublicFormQuestions_Sequence DEFAULT 1,
 IsActive bit NULL CONSTRAINT DF_F03PublicFormQuestions_IsActive DEFAULT 1, LastModifiedSource nvarchar(max) NULL,
 CONSTRAINT FK_F03PublicFormQuestions_Form FOREIGN KEY(FormId) REFERENCES dbo.F03PublicForms(Id) ON DELETE CASCADE,
 CONSTRAINT UQ_F03PublicFormQuestions_Code UNIQUE(FormId,QuestionCode),
 CONSTRAINT CK_F03PublicFormQuestions_Type CHECK(QuestionType IN (N'Text',N'Textarea',N'Number',N'Date',N'Time',N'DateTime',N'SingleChoice',N'MultiChoice',N'YesNo',N'Department',N'Employee',N'File'))
); END;

IF OBJECT_ID(N'dbo.F03PublicFormQuestionOptions', N'U') IS NULL
BEGIN
CREATE TABLE dbo.F03PublicFormQuestionOptions(
 Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03PublicFormQuestionOptions PRIMARY KEY,
 QuestionId int NOT NULL, OptionCode nvarchar(50) NOT NULL, OptionText nvarchar(300) NOT NULL,
 Sequence int NOT NULL CONSTRAINT DF_F03PublicFormQuestionOptions_Sequence DEFAULT 1,
 IsActive bit NULL CONSTRAINT DF_F03PublicFormQuestionOptions_IsActive DEFAULT 1, LastModifiedSource nvarchar(max) NULL,
 CONSTRAINT FK_F03PublicFormQuestionOptions_Question FOREIGN KEY(QuestionId) REFERENCES dbo.F03PublicFormQuestions(Id) ON DELETE CASCADE,
 CONSTRAINT UQ_F03PublicFormQuestionOptions_Code UNIQUE(QuestionId,OptionCode)
); END;

IF OBJECT_ID(N'dbo.F03PublicFormAudiences', N'U') IS NULL
BEGIN
CREATE TABLE dbo.F03PublicFormAudiences(
 Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03PublicFormAudiences PRIMARY KEY,
 FormId int NOT NULL, ScopeType nvarchar(20) NOT NULL, ScopeValue nvarchar(100) NULL,
 IsActive bit NULL CONSTRAINT DF_F03PublicFormAudiences_IsActive DEFAULT 1, LastModifiedSource nvarchar(max) NULL,
 CONSTRAINT FK_F03PublicFormAudiences_Form FOREIGN KEY(FormId) REFERENCES dbo.F03PublicForms(Id) ON DELETE CASCADE,
 CONSTRAINT CK_F03PublicFormAudiences_Type CHECK(ScopeType IN (N'AllCompany',N'Department',N'Position',N'Employee'))
); END;

IF OBJECT_ID(N'dbo.F03PublicFormSubmissions', N'U') IS NULL
BEGIN
CREATE TABLE dbo.F03PublicFormSubmissions(
 Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03PublicFormSubmissions PRIMARY KEY,
 FormId int NOT NULL, EmployeeCode nvarchar(50) NOT NULL,
 SubmittedAt datetime2 NOT NULL CONSTRAINT DF_F03PublicFormSubmissions_SubmittedAt DEFAULT GETDATE(),
 Status nvarchar(20) NOT NULL CONSTRAINT DF_F03PublicFormSubmissions_Status DEFAULT N'Submitted',
 FormVersion int NOT NULL CONSTRAINT DF_F03PublicFormSubmissions_Version DEFAULT 1,
 IsCancelled bit NOT NULL CONSTRAINT DF_F03PublicFormSubmissions_IsCancelled DEFAULT 0,
 CancelledAt datetime2 NULL, CancelledBy int NULL, LastModifiedSource nvarchar(max) NULL,
 CONSTRAINT FK_F03PublicFormSubmissions_Form FOREIGN KEY(FormId) REFERENCES dbo.F03PublicForms(Id) ON DELETE CASCADE,
 CONSTRAINT CK_F03PublicFormSubmissions_Status CHECK(Status IN (N'Submitted',N'Approved',N'Rejected',N'Cancelled'))
); END;

IF OBJECT_ID(N'dbo.F03PublicFormAnswers', N'U') IS NULL
BEGIN
CREATE TABLE dbo.F03PublicFormAnswers(
 Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03PublicFormAnswers PRIMARY KEY,
 SubmissionId int NOT NULL, QuestionId int NOT NULL, TextValue nvarchar(max) NULL,
 NumberValue decimal(18,4) NULL, DateValue datetime2 NULL, BoolValue bit NULL, JsonValue nvarchar(max) NULL, LastModifiedSource nvarchar(max) NULL,
 CONSTRAINT FK_F03PublicFormAnswers_Submission FOREIGN KEY(SubmissionId) REFERENCES dbo.F03PublicFormSubmissions(Id) ON DELETE CASCADE,
 CONSTRAINT FK_F03PublicFormAnswers_Question FOREIGN KEY(QuestionId) REFERENCES dbo.F03PublicFormQuestions(Id),
 CONSTRAINT UQ_F03PublicFormAnswers UNIQUE(SubmissionId,QuestionId)
); END;

/* BaseAuditEntity compatibility for databases where these tables were created by an older version of this script. */
DECLARE @PublicFormAuditTable sysname;
DECLARE @Sql nvarchar(max);
DECLARE PublicFormAuditCursor CURSOR LOCAL FAST_FORWARD FOR
SELECT v.TableName
FROM (VALUES
    (N'F03PublicForms'),
    (N'F03PublicFormQuestions'),
    (N'F03PublicFormQuestionOptions'),
    (N'F03PublicFormAudiences'),
    (N'F03PublicFormSubmissions'),
    (N'F03PublicFormAnswers')
) v(TableName)
WHERE OBJECT_ID(N'dbo.' + v.TableName, N'U') IS NOT NULL;
OPEN PublicFormAuditCursor;
FETCH NEXT FROM PublicFormAuditCursor INTO @PublicFormAuditTable;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF COL_LENGTH(N'dbo.' + @PublicFormAuditTable, N'LastModifiedSource') IS NULL
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@PublicFormAuditTable) + N' ADD LastModifiedSource nvarchar(max) NULL;';
        EXEC sys.sp_executesql @Sql;
    END;
    FETCH NEXT FROM PublicFormAuditCursor INTO @PublicFormAuditTable;
END;
CLOSE PublicFormAuditCursor;
DEALLOCATE PublicFormAuditCursor;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03PublicForms_StatusWindow' AND object_id=OBJECT_ID(N'dbo.F03PublicForms')) CREATE INDEX IX_F03PublicForms_StatusWindow ON dbo.F03PublicForms(Status,StartAt,EndAt,IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03PublicFormAudiences_Form' AND object_id=OBJECT_ID(N'dbo.F03PublicFormAudiences')) CREATE INDEX IX_F03PublicFormAudiences_Form ON dbo.F03PublicFormAudiences(FormId,ScopeType,ScopeValue,IsActive);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03PublicFormSubmissions_FormEmployee' AND object_id=OBJECT_ID(N'dbo.F03PublicFormSubmissions')) CREATE INDEX IX_F03PublicFormSubmissions_FormEmployee ON dbo.F03PublicFormSubmissions(FormId,EmployeeCode,Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03PublicFormAnswers_Submission' AND object_id=OBJECT_ID(N'dbo.F03PublicFormAnswers')) CREATE INDEX IX_F03PublicFormAnswers_Submission ON dbo.F03PublicFormAnswers(SubmissionId);

/* Immutable administration history: records who created/edited/published/closed each form. */
IF OBJECT_ID(N'dbo.F03PublicFormAudits',N'U') IS NULL
BEGIN
CREATE TABLE dbo.F03PublicFormAudits(
 Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03PublicFormAudits PRIMARY KEY,
 FormId int NOT NULL,
 ActionCode nvarchar(40) NOT NULL,
 ActorEmployeeCode nvarchar(50) NULL,
 BeforeJson nvarchar(max) NULL,
 AfterJson nvarchar(max) NULL,
 CreatedBy int NOT NULL,
 CreatedAt datetime2 NOT NULL CONSTRAINT DF_F03PublicFormAudits_CreatedAt DEFAULT GETDATE(),
 ModifiedBy int NULL,
 ModifiedAt datetime2 NULL,
 LastModifiedSource nvarchar(max) NULL,
 IsActive bit NULL CONSTRAINT DF_F03PublicFormAudits_IsActive DEFAULT 1,
 CONSTRAINT FK_F03PublicFormAudits_Form FOREIGN KEY(FormId) REFERENCES dbo.F03PublicForms(Id) ON DELETE CASCADE
);
END;

/* Recipient feedback is intentionally not a child cascade of the form because a form already cascades to submissions. Keeping this relationship NO ACTION avoids multiple cascade paths while preserving the optional submission link. */
IF OBJECT_ID(N'dbo.F03PublicFormFeedbacks',N'U') IS NULL
BEGIN
CREATE TABLE dbo.F03PublicFormFeedbacks(
 Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03PublicFormFeedbacks PRIMARY KEY,
 FormId int NOT NULL,
 SubmissionId int NULL,
 EmployeeCode nvarchar(50) NOT NULL,
 Content nvarchar(max) NOT NULL,
 CreatedBy int NOT NULL,
 CreatedAt datetime2 NOT NULL CONSTRAINT DF_F03PublicFormFeedbacks_CreatedAt DEFAULT GETDATE(),
 ModifiedBy int NULL,
 ModifiedAt datetime2 NULL,
 LastModifiedSource nvarchar(max) NULL,
 IsActive bit NULL CONSTRAINT DF_F03PublicFormFeedbacks_IsActive DEFAULT 1,
 CONSTRAINT FK_F03PublicFormFeedbacks_Form FOREIGN KEY(FormId) REFERENCES dbo.F03PublicForms(Id) ON DELETE NO ACTION,
 CONSTRAINT FK_F03PublicFormFeedbacks_Submission FOREIGN KEY(SubmissionId) REFERENCES dbo.F03PublicFormSubmissions(Id) ON DELETE SET NULL
);
END;

/* Repair an older database where Feedbacks was created with the invalid CASCADE + SET NULL combination. */
IF OBJECT_ID(N'dbo.F03PublicFormFeedbacks',N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03PublicFormFeedbacks_Form' AND parent_object_id=OBJECT_ID(N'dbo.F03PublicFormFeedbacks'))
        ALTER TABLE dbo.F03PublicFormFeedbacks DROP CONSTRAINT FK_F03PublicFormFeedbacks_Form;
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03PublicFormFeedbacks_Form' AND parent_object_id=OBJECT_ID(N'dbo.F03PublicFormFeedbacks'))
        ALTER TABLE dbo.F03PublicFormFeedbacks ADD CONSTRAINT FK_F03PublicFormFeedbacks_Form FOREIGN KEY(FormId) REFERENCES dbo.F03PublicForms(Id) ON DELETE NO ACTION;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03PublicFormAudits_FormCreated' AND object_id=OBJECT_ID(N'dbo.F03PublicFormAudits')) CREATE INDEX IX_F03PublicFormAudits_FormCreated ON dbo.F03PublicFormAudits(FormId,CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03PublicFormFeedbacks_FormEmployee' AND object_id=OBJECT_ID(N'dbo.F03PublicFormFeedbacks')) CREATE INDEX IX_F03PublicFormFeedbacks_FormEmployee ON dbo.F03PublicFormFeedbacks(FormId,EmployeeCode,CreatedAt DESC);

/* F03Functions has a unique FunctionKey in newer schemas. Older seed statements omitted it and therefore inserted the empty default repeatedly. Temporarily remove the unique index while seeding, then normalize all missing keys and recreate it. */
IF OBJECT_ID(N'dbo.F03Functions',N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03Functions_FunctionKey' AND object_id=OBJECT_ID(N'dbo.F03Functions'))
        DROP INDEX UX_F03Functions_FunctionKey ON dbo.F03Functions;
END;

IF OBJECT_ID(N'dbo.F03Functions',N'U') IS NOT NULL
BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2807)
 INSERT INTO dbo.F03Functions(FunctionCode,FunctionName,Detail,ModuleCode,ActionCode,ScopeCode,DisplayOrder,IsActive)
 VALUES(2807,N'Public Registration Form - Manage',N'Tạo, thiết kế, publish, đóng và quản lý biểu mẫu đăng ký động',N'PublicForm',N'Manage',N'All',2807,1);
 ELSE
 UPDATE dbo.F03Functions SET FunctionName=N'Public Registration Form - Manage',Detail=N'Tạo, thiết kế, publish, đóng và quản lý biểu mẫu đăng ký động',ModuleCode=N'PublicForm',ActionCode=N'Manage',ScopeCode=N'All',DisplayOrder=2807,IsActive=1 WHERE FunctionCode=2807;
END;

IF OBJECT_ID(N'dbo.F03Functions',N'U') IS NOT NULL
BEGIN
    INSERT dbo.F03Functions(IsActive,CreatedBy,FunctionCode,FunctionName,Detail,ModuleCode,ActionCode,ScopeCode,DisplayOrder)
    SELECT 1,0,v.FunctionCode,v.FunctionName,v.Detail,N'PublicForm',v.ActionCode,N'All',v.DisplayOrder
    FROM (VALUES
        (2808,N'PublicForm.SubmissionView',N'Xem danh sách, chi tiết và tổng hợp đăng ký biểu mẫu',N'SubmissionView',2808),
        (2809,N'PublicForm.Export',N'Xuất Excel dữ liệu đăng ký biểu mẫu',N'Export',2809)
    ) v(FunctionCode,FunctionName,Detail,ActionCode,DisplayOrder)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.F03Functions f WHERE f.FunctionCode=v.FunctionCode);

    UPDATE f SET FunctionName=v.FunctionName,Detail=v.Detail,ModuleCode=N'PublicForm',ActionCode=v.ActionCode,ScopeCode=N'All',DisplayOrder=v.DisplayOrder,IsActive=1
    FROM dbo.F03Functions f JOIN (VALUES
        (2808,N'PublicForm.SubmissionView',N'Xem danh sách, chi tiết và tổng hợp đăng ký biểu mẫu',N'SubmissionView',2808),
        (2809,N'PublicForm.Export',N'Xuất Excel dữ liệu đăng ký biểu mẫu',N'Export',2809)
    ) v(FunctionCode,FunctionName,Detail,ActionCode,DisplayOrder) ON f.FunctionCode=v.FunctionCode;
END;
GO

IF OBJECT_ID(N'dbo.F03Functions',N'U') IS NOT NULL
BEGIN
    INSERT dbo.F03Functions(IsActive,CreatedBy,FunctionCode,FunctionName,Detail,ModuleCode,ActionCode,ScopeCode,DisplayOrder)
    SELECT 1,0,v.FunctionCode,v.FunctionName,v.Detail,N'PublicForm',v.ActionCode,v.ScopeCode,v.DisplayOrder
    FROM (VALUES
        (2810,N'PublicForm.View',N'Xem biểu mẫu được chỉ định cho bản thân',N'View',N'Own',2810),
        (2811,N'PublicForm.Submit',N'Trả lời và gửi biểu mẫu được chỉ định',N'Submit',N'Own',2811),
        (2812,N'PublicForm.Feedback',N'Gửi phản hồi về biểu mẫu',N'Feedback',N'Own',2812),
        (2813,N'PublicForm.Create',N'Tạo biểu mẫu',N'Create',N'All',2813),
        (2814,N'PublicForm.Edit',N'Sửa, publish và đóng biểu mẫu',N'Edit',N'All',2814),
        (2815,N'PublicForm.AssignAudience',N'Chỉ định nhân viên hoặc bộ phận nhận biểu mẫu',N'AssignAudience',N'All',2815),
        (2816,N'PublicForm.ResultView',N'Xem kết quả và phản hồi đã gửi',N'ResultView',N'All',2816),
        (2817,N'PublicForm.ResultExport',N'Xuất kết quả biểu mẫu',N'Export',N'All',2817),
        (2818,N'PublicForm.AuditView',N'Xem lịch sử tạo, sửa và thay đổi trạng thái biểu mẫu',N'AuditView',N'All',2818)
    ) v(FunctionCode,FunctionName,Detail,ActionCode,ScopeCode,DisplayOrder)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.F03Functions f WHERE f.FunctionCode=v.FunctionCode);

    UPDATE f SET FunctionName=v.FunctionName,Detail=v.Detail,ModuleCode=N'PublicForm',ActionCode=v.ActionCode,ScopeCode=v.ScopeCode,DisplayOrder=v.DisplayOrder,IsActive=1
    FROM dbo.F03Functions f JOIN (VALUES
        (2810,N'PublicForm.View',N'Xem biểu mẫu được chỉ định cho bản thân',N'View',N'Own',2810),
        (2811,N'PublicForm.Submit',N'Trả lời và gửi biểu mẫu được chỉ định',N'Submit',N'Own',2811),
        (2812,N'PublicForm.Feedback',N'Gửi phản hồi về biểu mẫu',N'Feedback',N'Own',2812),
        (2813,N'PublicForm.Create',N'Tạo biểu mẫu',N'Create',N'All',2813),
        (2814,N'PublicForm.Edit',N'Sửa, publish và đóng biểu mẫu',N'Edit',N'All',2814),
        (2815,N'PublicForm.AssignAudience',N'Chỉ định nhân viên hoặc bộ phận nhận biểu mẫu',N'AssignAudience',N'All',2815),
        (2816,N'PublicForm.ResultView',N'Xem kết quả và phản hồi đã gửi',N'ResultView',N'All',2816),
        (2817,N'PublicForm.ResultExport',N'Xuất kết quả biểu mẫu',N'Export',N'All',2817),
        (2818,N'PublicForm.AuditView',N'Xem lịch sử tạo, sửa và thay đổi trạng thái biểu mẫu',N'AuditView',N'All',2818)
    ) v(FunctionCode,FunctionName,Detail,ActionCode,ScopeCode,DisplayOrder) ON f.FunctionCode=v.FunctionCode;
END;
GO

/* Normalize FunctionKey for the seeded PublicForm capabilities and any pre-existing blank legacy keys. */
IF OBJECT_ID(N'dbo.F03Functions',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.F03Functions',N'FunctionKey') IS NOT NULL
BEGIN
    /* Dynamic: FunctionKey does not exist on a fresh database until 46_SecurityFunctionRegistry.sql. */
    EXEC sys.sp_executesql N'    UPDATE f SET FunctionKey = CONCAT(N''PublicForm.'', f.FunctionCode)
    FROM dbo.F03Functions f
    WHERE f.FunctionCode BETWEEN 2807 AND 2818;

    UPDATE f
    SET FunctionKey = CONCAT(
        COALESCE(NULLIF(LTRIM(RTRIM(f.ModuleCode)),N''''),N''Legacy''),N''.'',
        COALESCE(NULLIF(LTRIM(RTRIM(f.ActionCode)),N''''),N''Function''),N''.'',
        f.FunctionCode,N''.'',f.Id)
    FROM dbo.F03Functions f
    WHERE f.FunctionKey IS NULL OR LTRIM(RTRIM(f.FunctionKey))=N'''';

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N''UX_F03Functions_FunctionKey'' AND object_id=OBJECT_ID(N''dbo.F03Functions''))
        CREATE UNIQUE INDEX UX_F03Functions_FunctionKey ON dbo.F03Functions(FunctionKey);';
END;
GO

IF OBJECT_ID(N'dbo.F03RoleFunctions',N'U') IS NOT NULL
BEGIN
    INSERT dbo.F03RoleFunctions(IdRole,IdFunction,ScopeCode,AccessMode)
    SELECT r.Id,f.Id,N'Own',N'Personal'
    FROM dbo.F03Roles r CROSS JOIN dbo.F03Functions f
    WHERE r.RoleCode IN (3,4,5) AND f.FunctionCode IN (2810,2811,2812)
      AND NOT EXISTS (SELECT 1 FROM dbo.F03RoleFunctions rf WHERE rf.IdRole=r.Id AND rf.IdFunction=f.Id);

    INSERT dbo.F03RoleFunctions(IdRole,IdFunction,ScopeCode,AccessMode)
    SELECT r.Id,f.Id,N'All',N'Management'
    FROM dbo.F03Roles r CROSS JOIN dbo.F03Functions f
    WHERE r.RoleCode IN (1,2,3,4,5)
      AND f.FunctionCode IN (2813,2814,2815,2816,2817,2818)
      AND NOT EXISTS (SELECT 1 FROM dbo.F03RoleFunctions rf WHERE rf.IdRole=r.Id AND rf.IdFunction=f.Id);

    UPDATE rf SET ScopeCode=N'All',AccessMode=N'Management'
    FROM dbo.F03RoleFunctions rf
    JOIN dbo.F03Roles r ON r.Id=rf.IdRole
    JOIN dbo.F03Functions f ON f.Id=rf.IdFunction
    WHERE f.FunctionCode IN (2813,2814,2815,2816,2817,2818) AND r.RoleCode IN (1,2,3,4,5);
END;
GO

IF OBJECT_ID(N'dbo.F03RoleFunctions',N'U') IS NOT NULL
BEGIN
    INSERT dbo.F03RoleFunctions(IdRole,IdFunction)
    SELECT r.Id,f.Id
    FROM dbo.F03Roles r CROSS JOIN dbo.F03Functions f
    WHERE r.RoleCode IN (1,2)
      AND f.FunctionCode IN (2808,2809)
      AND NOT EXISTS (SELECT 1 FROM dbo.F03RoleFunctions rf WHERE rf.IdRole=r.Id AND rf.IdFunction=f.Id);
END;
GO
