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

/* Canonical Shared Excel Platform owns Equipment schema/import metadata. */
IF EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2306)
    UPDATE dbo.F03Functions
    SET FunctionName=N'Equipment.Import', Detail=N'Import Excel thiết bị theo schema của từng phòng ban.', ModuleCode=N'Equipment', ActionCode=N'Import', ScopeCode=N'Department', DisplayOrder=360
    WHERE FunctionCode=2306;
ELSE
    INSERT dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive,ModuleCode,ActionCode,ScopeCode,DisplayOrder)
    VALUES(2306,N'Equipment.Import',N'Import Excel thiết bị theo schema của từng phòng ban.',1,GETDATE(),1,N'Equipment',N'Import',N'Department',360);
GO
