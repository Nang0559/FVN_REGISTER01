/* ============================================================
   16 - CANONICAL EQUIPMENT DEPLOYMENT
   ------------------------------------------------------------
   This is the single deployment owner for the Equipment base schema
   previously split across Database/Equipment/001-005.
   00_Deploy_All.sql executes this file before Equipment upgrades
   42/43/45/50, so later scripts may safely extend these objects.
   ============================================================ */

/* 1. Equipment assets */
IF OBJECT_ID(N'dbo.F03EquipmentAssets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentAssets
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentAssets PRIMARY KEY,
        EquipmentCode NVARCHAR(30) NOT NULL,
        EquipmentName NVARCHAR(250) NOT NULL,
        Specification NVARCHAR(1000) NULL,
        SerialNumber NVARCHAR(100) NULL,
        AssetCode NVARCHAR(50) NULL,
        PurchasePrice DECIMAL(18,2) NOT NULL,
        PurchaseDate DATETIME2 NOT NULL,
        ExpectedDepreciationDate DATETIME2 NOT NULL,
        DeptCode NVARCHAR(20) NOT NULL,
        Location NVARCHAR(250) NULL,
        QrToken NVARCHAR(128) NOT NULL,
        IsQrActive BIT NOT NULL CONSTRAINT DF_F03EquipmentAssets_Qr DEFAULT(0),
        IsActive BIT NULL CONSTRAINT DF_F03EquipmentAssets_Active DEFAULT(1),
        Note NVARCHAR(1000) NULL,
        CreatedBy INT NOT NULL,
        LastModifiedSource NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03EquipmentAssets_CreatedAt DEFAULT(GETDATE()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2 NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03EquipmentAssets_Code' AND object_id=OBJECT_ID(N'dbo.F03EquipmentAssets'))
    CREATE UNIQUE INDEX UX_F03EquipmentAssets_Code ON dbo.F03EquipmentAssets(EquipmentCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03EquipmentAssets_Qr' AND object_id=OBJECT_ID(N'dbo.F03EquipmentAssets'))
    CREATE UNIQUE INDEX UX_F03EquipmentAssets_Qr ON dbo.F03EquipmentAssets(QrToken);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EquipmentAssets_Dept' AND object_id=OBJECT_ID(N'dbo.F03EquipmentAssets'))
    CREATE INDEX IX_F03EquipmentAssets_Dept ON dbo.F03EquipmentAssets(DeptCode);
IF COL_LENGTH(N'dbo.F03EquipmentAssets', N'CustomDataJson') IS NULL
    ALTER TABLE dbo.F03EquipmentAssets ADD CustomDataJson NVARCHAR(MAX) NULL;
GO

/* 2. Equipment requests */
IF OBJECT_ID(N'dbo.F03EquipmentRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentRequests
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentRequests PRIMARY KEY,
        EmployeeCode NVARCHAR(50) NOT NULL,
        DeptCode NVARCHAR(20) NOT NULL,
        RequestStatus INT NOT NULL CONSTRAINT DF_F03EquipmentRequests_Status DEFAULT(0),
        IsActive BIT NULL CONSTRAINT DF_F03EquipmentRequests_Active DEFAULT(1),
        CreatedBy INT NOT NULL,
        LastModifiedSource NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03EquipmentRequests_CreatedAt DEFAULT(GETDATE()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2 NULL,
        RequestKind INT NOT NULL,
        AssetId INT NULL,
        SelectedApproverCode NVARCHAR(50) NOT NULL,
        QrToken NVARCHAR(128) NOT NULL,
        OperatorUserId INT NOT NULL,
        EquipmentName NVARCHAR(250) NOT NULL,
        Specification NVARCHAR(1000) NULL,
        SerialNumber NVARCHAR(100) NULL,
        AssetCode NVARCHAR(50) NULL,
        PurchasePrice DECIMAL(18,2) NOT NULL,
        PurchaseDate DATETIME2 NULL,
        ExpectedDepreciationDate DATETIME2 NULL,
        Location NVARCHAR(250) NULL,
        Note NVARCHAR(1000) NULL,
        RepairDate DATETIME2 NULL,
        RepairContent NVARCHAR(1000) NULL,
        RepairVendor NVARCHAR(250) NULL,
        RepairCost DECIMAL(18,2) NULL,
        RepairResult NVARCHAR(1000) NULL,
        CONSTRAINT FK_F03EquipmentRequests_Asset FOREIGN KEY(AssetId) REFERENCES dbo.F03EquipmentAssets(Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EquipmentRequests_Status' AND object_id=OBJECT_ID(N'dbo.F03EquipmentRequests'))
    CREATE INDEX IX_F03EquipmentRequests_Status ON dbo.F03EquipmentRequests(RequestKind,RequestStatus);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EquipmentRequests_Employee' AND object_id=OBJECT_ID(N'dbo.F03EquipmentRequests'))
    CREATE INDEX IX_F03EquipmentRequests_Employee ON dbo.F03EquipmentRequests(EmployeeCode,CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EquipmentRequests_Qr' AND object_id=OBJECT_ID(N'dbo.F03EquipmentRequests'))
    CREATE INDEX IX_F03EquipmentRequests_Qr ON dbo.F03EquipmentRequests(QrToken);
GO

/* 3. Repair history */
IF OBJECT_ID(N'dbo.F03EquipmentRepairHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentRepairHistory
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentRepairHistory PRIMARY KEY,
        AssetId INT NOT NULL,
        RequestId INT NOT NULL,
        RepairDate DATETIME2 NOT NULL,
        OperatorUserId INT NOT NULL,
        RepairCost DECIMAL(18,2) NULL,
        RepairContent NVARCHAR(1000) NOT NULL,
        RepairVendor NVARCHAR(250) NULL,
        RepairResult NVARCHAR(1000) NULL,
        Note NVARCHAR(1000) NULL,
        IsApproved BIT NOT NULL CONSTRAINT DF_F03EquipmentRepairHistory_Approved DEFAULT(0),
        CreatedBy INT NOT NULL,
        LastModifiedSource NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03EquipmentRepairHistory_CreatedAt DEFAULT(GETDATE()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2 NULL,
        CONSTRAINT FK_F03EquipmentRepairHistory_Asset FOREIGN KEY(AssetId) REFERENCES dbo.F03EquipmentAssets(Id),
        CONSTRAINT FK_F03EquipmentRepairHistory_Request FOREIGN KEY(RequestId) REFERENCES dbo.F03EquipmentRequests(Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03EquipmentRepairHistory_Request' AND object_id=OBJECT_ID(N'dbo.F03EquipmentRepairHistory'))
    CREATE UNIQUE INDEX UX_F03EquipmentRepairHistory_Request ON dbo.F03EquipmentRepairHistory(RequestId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EquipmentRepairHistory_AssetDate' AND object_id=OBJECT_ID(N'dbo.F03EquipmentRepairHistory'))
    CREATE INDEX IX_F03EquipmentRepairHistory_AssetDate ON dbo.F03EquipmentRepairHistory(AssetId,RepairDate);
GO

/* 4. Equipment RBAC capabilities */
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2301)
    INSERT dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2301,N'Equipment.View',N'Quyền sử dụng Sổ quản lý thiết bị và tra cứu QR.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2302)
    INSERT dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2302,N'Equipment.Create',N'Đăng ký thiết bị mới.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2303)
    INSERT dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2303,N'Equipment.Edit',N'Chỉnh sửa/gửi đăng ký thiết bị.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2304)
    INSERT dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2304,N'Equipment.Repair',N'Tạo và gửi yêu cầu sửa chữa thiết bị.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2305)
    INSERT dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2305,N'Equipment.Approve',N'Phê duyệt yêu cầu thiết bị.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2306)
    INSERT dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2306,N'Equipment.Import',N'Import Excel thiết bị theo schema của từng phòng ban.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2307)
    INSERT dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2307,N'Equipment.Export',N'Xuất dữ liệu thiết bị.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2308)
    INSERT dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2308,N'Equipment.Cancel',N'Hủy yêu cầu thiết bị.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=900)
    INSERT dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(900,N'EquipmentModule',N'Legacy marker: quyền sử dụng Sổ quản lý thiết bị.',1,GETDATE(),1);
GO

/* 5. Versioned Equipment schema */
IF OBJECT_ID(N'dbo.F03EquipmentSchemas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentSchemas
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentSchemas PRIMARY KEY,
        DeptCode NVARCHAR(20) NOT NULL,
        SchemaName NVARCHAR(150) NOT NULL,
        SchemaKind NVARCHAR(20) NOT NULL CONSTRAINT DF_F03EquipmentSchemas_SchemaKind DEFAULT(N'Equipment'),
        SchemaKey NVARCHAR(64) NOT NULL,
        Version INT NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_F03EquipmentSchemas_Status DEFAULT(N'Draft'),
        SourceSchemaId INT NULL,
        SourceFileName NVARCHAR(260) NULL,
        CreatedFromExcel BIT NOT NULL CONSTRAINT DF_F03EquipmentSchemas_CreatedFromExcel DEFAULT(0),
        IsActive BIT NULL CONSTRAINT DF_F03EquipmentSchemas_IsActive DEFAULT(1),
        CreatedBy INT NOT NULL,
        LastModifiedSource NVARCHAR(200) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03EquipmentSchemas_CreatedAt DEFAULT(SYSDATETIME()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2 NULL,
        CONSTRAINT FK_F03EquipmentSchemas_SourceSchema FOREIGN KEY(SourceSchemaId) REFERENCES dbo.F03EquipmentSchemas(Id)
    );
END;
GO

IF COL_LENGTH(N'dbo.F03EquipmentSchemas',N'SchemaKind') IS NULL
    ALTER TABLE dbo.F03EquipmentSchemas ADD SchemaKind NVARCHAR(20) NOT NULL CONSTRAINT DF_F03EquipmentSchemas_SchemaKind DEFAULT(N'Equipment');
IF COL_LENGTH(N'dbo.F03EquipmentSchemas',N'SchemaKey') IS NULL
    ALTER TABLE dbo.F03EquipmentSchemas ADD SchemaKey NVARCHAR(64) NULL;
IF COL_LENGTH(N'dbo.F03EquipmentSchemas',N'SourceSchemaId') IS NULL
    ALTER TABLE dbo.F03EquipmentSchemas ADD SourceSchemaId INT NULL;
IF COL_LENGTH(N'dbo.F03EquipmentSchemas',N'SourceFileName') IS NULL
    ALTER TABLE dbo.F03EquipmentSchemas ADD SourceFileName NVARCHAR(260) NULL;
IF COL_LENGTH(N'dbo.F03EquipmentSchemas',N'CreatedFromExcel') IS NULL
    ALTER TABLE dbo.F03EquipmentSchemas ADD CreatedFromExcel BIT NOT NULL CONSTRAINT DF_F03EquipmentSchemas_CreatedFromExcel DEFAULT(0);
GO

UPDATE s
SET SchemaKey=CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(UPPER(LTRIM(RTRIM(s.DeptCode))),N'|',UPPER(LTRIM(RTRIM(s.SchemaName))))),2)
FROM dbo.F03EquipmentSchemas s
WHERE NULLIF(LTRIM(RTRIM(s.SchemaKey)),N'') IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.F03EquipmentSchemas') AND name=N'SchemaKey' AND is_nullable=1)
    ALTER TABLE dbo.F03EquipmentSchemas ALTER COLUMN SchemaKey NVARCHAR(64) NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03EquipmentSchemas_SourceSchema')
    ALTER TABLE dbo.F03EquipmentSchemas WITH CHECK ADD CONSTRAINT FK_F03EquipmentSchemas_SourceSchema FOREIGN KEY(SourceSchemaId) REFERENCES dbo.F03EquipmentSchemas(Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03EquipmentSchemas_Dept_SchemaKey_Version' AND object_id=OBJECT_ID(N'dbo.F03EquipmentSchemas'))
    CREATE UNIQUE INDEX UX_F03EquipmentSchemas_Dept_SchemaKey_Version ON dbo.F03EquipmentSchemas(DeptCode,SchemaKey,Version);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EquipmentSchemas_Dept_Status' AND object_id=OBJECT_ID(N'dbo.F03EquipmentSchemas'))
    CREATE INDEX IX_F03EquipmentSchemas_Dept_Status ON dbo.F03EquipmentSchemas(DeptCode,Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EquipmentSchemas_Dept_SchemaKey' AND object_id=OBJECT_ID(N'dbo.F03EquipmentSchemas'))
    CREATE INDEX IX_F03EquipmentSchemas_Dept_SchemaKey ON dbo.F03EquipmentSchemas(DeptCode,SchemaKey,Version DESC);
GO

/* 6. Dynamic field definitions */
IF OBJECT_ID(N'dbo.F03EquipmentFieldDefinitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentFieldDefinitions
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentFieldDefinitions PRIMARY KEY,
        SchemaId INT NULL,
        DeptCode NVARCHAR(20) NOT NULL,
        FieldKey NVARCHAR(60) NOT NULL,
        FieldLabel NVARCHAR(150) NOT NULL,
        DataType NVARCHAR(20) NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_DataType DEFAULT(N'Text'),
        IsRequired BIT NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_IsRequired DEFAULT(0),
        IsImportable BIT NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_IsImportable DEFAULT(1),
        IsSearchable BIT NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_IsSearchable DEFAULT(0),
        IsActiveField BIT NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_IsActiveField DEFAULT(1),
        DisplayOrder INT NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_DisplayOrder DEFAULT(0),
        MaxLength INT NULL,
        DefaultValue NVARCHAR(500) NULL,
        OptionsJson NVARCHAR(2000) NULL,
        IsActive BIT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_IsActive DEFAULT(1),
        CreatedBy INT NOT NULL,
        LastModifiedSource NVARCHAR(200) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_CreatedAt DEFAULT(SYSDATETIME()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2 NULL
    );
END;
GO

IF COL_LENGTH(N'dbo.F03EquipmentFieldDefinitions',N'SchemaId') IS NULL
    ALTER TABLE dbo.F03EquipmentFieldDefinitions ADD SchemaId INT NULL;
IF COL_LENGTH(N'dbo.F03EquipmentFieldDefinitions',N'MaxLength') IS NULL
    ALTER TABLE dbo.F03EquipmentFieldDefinitions ADD MaxLength INT NULL;
IF COL_LENGTH(N'dbo.F03EquipmentFieldDefinitions',N'DefaultValue') IS NULL
    ALTER TABLE dbo.F03EquipmentFieldDefinitions ADD DefaultValue NVARCHAR(500) NULL;
GO

/* 7. Import batches/rows - created here, before all later Equipment upgrades */
IF OBJECT_ID(N'dbo.F03EquipmentImportBatches',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentImportBatches
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentImportBatches PRIMARY KEY,
        SchemaId INT NULL,
        DeptCode NVARCHAR(20) NOT NULL,
        FileName NVARCHAR(260) NOT NULL,
        Status NVARCHAR(30) NOT NULL,
        TotalRows INT NOT NULL CONSTRAINT DF_F03EquipmentImportBatches_TotalRows DEFAULT(0),
        ValidRows INT NOT NULL CONSTRAINT DF_F03EquipmentImportBatches_ValidRows DEFAULT(0),
        InvalidRows INT NOT NULL CONSTRAINT DF_F03EquipmentImportBatches_InvalidRows DEFAULT(0),
        ImportedRows INT NOT NULL CONSTRAINT DF_F03EquipmentImportBatches_ImportedRows DEFAULT(0),
        CompletedAt DATETIME2 NULL,
        IsActive BIT NULL CONSTRAINT DF_F03EquipmentImportBatches_IsActive DEFAULT(1),
        CreatedBy INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03EquipmentImportBatches_CreatedAt DEFAULT(SYSDATETIME()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2 NULL,
        LastModifiedSource NVARCHAR(100) NULL
    );
END;
GO
IF COL_LENGTH(N'dbo.F03EquipmentImportBatches',N'SchemaId') IS NULL
    ALTER TABLE dbo.F03EquipmentImportBatches ADD SchemaId INT NULL;
IF COL_LENGTH(N'dbo.F03EquipmentImportBatches',N'AssignToEmployee') IS NULL
    ALTER TABLE dbo.F03EquipmentImportBatches ADD AssignToEmployee BIT NOT NULL CONSTRAINT DF_F03EquipmentImportBatches_AssignToEmployee DEFAULT(0);
GO

IF OBJECT_ID(N'dbo.F03EquipmentImportRows',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentImportRows
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentImportRows PRIMARY KEY,
        BatchId INT NOT NULL,
        RowNumber INT NOT NULL,
        RawJson NVARCHAR(MAX) NOT NULL,
        Status NVARCHAR(30) NOT NULL,
        ErrorMessage NVARCHAR(2000) NULL,
        AssetId INT NULL,
        IsActive BIT NULL CONSTRAINT DF_F03EquipmentImportRows_IsActive DEFAULT(1),
        CreatedBy INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03EquipmentImportRows_CreatedAt DEFAULT(SYSDATETIME()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2 NULL,
        LastModifiedSource NVARCHAR(100) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03EquipmentImportRows_Batch')
    ALTER TABLE dbo.F03EquipmentImportRows WITH CHECK ADD CONSTRAINT FK_F03EquipmentImportRows_Batch FOREIGN KEY(BatchId) REFERENCES dbo.F03EquipmentImportBatches(Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03EquipmentFieldDefinitions_F03EquipmentSchemas')
    ALTER TABLE dbo.F03EquipmentFieldDefinitions WITH CHECK ADD CONSTRAINT FK_F03EquipmentFieldDefinitions_F03EquipmentSchemas FOREIGN KEY(SchemaId) REFERENCES dbo.F03EquipmentSchemas(Id) ON DELETE CASCADE;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03EquipmentImportBatches_F03EquipmentSchemas')
    ALTER TABLE dbo.F03EquipmentImportBatches WITH CHECK ADD CONSTRAINT FK_F03EquipmentImportBatches_F03EquipmentSchemas FOREIGN KEY(SchemaId) REFERENCES dbo.F03EquipmentSchemas(Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EquipmentImportRows_Batch_Status' AND object_id=OBJECT_ID(N'dbo.F03EquipmentImportRows'))
    CREATE INDEX IX_F03EquipmentImportRows_Batch_Status ON dbo.F03EquipmentImportRows(BatchId,Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EquipmentImportBatches_Dept_Schema_Status' AND object_id=OBJECT_ID(N'dbo.F03EquipmentImportBatches'))
    CREATE INDEX IX_F03EquipmentImportBatches_Dept_Schema_Status ON dbo.F03EquipmentImportBatches(DeptCode,SchemaId,Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03EquipmentFieldDefinitions_Schema_FieldKey' AND object_id=OBJECT_ID(N'dbo.F03EquipmentFieldDefinitions'))
    CREATE UNIQUE INDEX UX_F03EquipmentFieldDefinitions_Schema_FieldKey ON dbo.F03EquipmentFieldDefinitions(SchemaId,FieldKey);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03EquipmentFieldDefinitions_Dept_Active' AND object_id=OBJECT_ID(N'dbo.F03EquipmentFieldDefinitions'))
    CREATE INDEX IX_F03EquipmentFieldDefinitions_Dept_Active ON dbo.F03EquipmentFieldDefinitions(DeptCode,IsActive,IsActiveField,DisplayOrder);
GO

/* 8. Upgrade legacy field rows to a schema version before making SchemaId required. */
DECLARE @DeptCode NVARCHAR(20), @SchemaId INT;
DECLARE dept_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT DISTINCT UPPER(LTRIM(RTRIM(DeptCode))) FROM dbo.F03EquipmentFieldDefinitions
    WHERE NULLIF(LTRIM(RTRIM(DeptCode)),N'') IS NOT NULL;
OPEN dept_cursor;
FETCH NEXT FROM dept_cursor INTO @DeptCode;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @SchemaId=NULL;
    SELECT TOP(1) @SchemaId=Id FROM dbo.F03EquipmentSchemas
    WHERE DeptCode=@DeptCode AND Status=N'Active' AND IsActive=1 ORDER BY Version DESC,Id DESC;
    IF @SchemaId IS NULL
    BEGIN
        DECLARE @SchemaKey NVARCHAR(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(@DeptCode,N'|Equipment')),2);
        INSERT dbo.F03EquipmentSchemas(DeptCode,SchemaName,SchemaKind,SchemaKey,Version,Status,IsActive,CreatedBy)
        VALUES(@DeptCode,CONCAT(@DeptCode,N' Equipment'),N'Equipment',@SchemaKey,1,N'Active',1,0);
        SET @SchemaId=SCOPE_IDENTITY();
    END;
    UPDATE dbo.F03EquipmentFieldDefinitions SET SchemaId=@SchemaId
    WHERE UPPER(LTRIM(RTRIM(DeptCode)))=@DeptCode AND (SchemaId IS NULL OR SchemaId=0);
    FETCH NEXT FROM dept_cursor INTO @DeptCode;
END;
CLOSE dept_cursor; DEALLOCATE dept_cursor;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.F03EquipmentFieldDefinitions') AND name=N'SchemaId' AND is_nullable=1)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03EquipmentFieldDefinitions_Schema_FieldKey' AND object_id=OBJECT_ID(N'dbo.F03EquipmentFieldDefinitions'))
        DROP INDEX UX_F03EquipmentFieldDefinitions_Schema_FieldKey ON dbo.F03EquipmentFieldDefinitions;
    ALTER TABLE dbo.F03EquipmentFieldDefinitions ALTER COLUMN SchemaId INT NOT NULL;
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03EquipmentFieldDefinitions_Schema_FieldKey' AND object_id=OBJECT_ID(N'dbo.F03EquipmentFieldDefinitions'))
    CREATE UNIQUE INDEX UX_F03EquipmentFieldDefinitions_Schema_FieldKey ON dbo.F03EquipmentFieldDefinitions(SchemaId,FieldKey);
GO

/* Canonical import capability */
IF EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2306)
    UPDATE dbo.F03Functions SET FunctionName=N'Equipment.Import',Detail=N'Import Excel thiết bị theo schema của từng phòng ban.',ModuleCode=N'Equipment',ActionCode=N'Import',ScopeCode=N'Department',DisplayOrder=360 WHERE FunctionCode=2306;
ELSE
    INSERT dbo.F03Functions(FunctionCode,FunctionName,Detail,ModuleCode,ActionCode,ScopeCode,DisplayOrder)
    VALUES(2306,N'Equipment.Import',N'Import Excel thiết bị theo schema của từng phòng ban.',N'Equipment',N'Import',N'Department',360);
GO
