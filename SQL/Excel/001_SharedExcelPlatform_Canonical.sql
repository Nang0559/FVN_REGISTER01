/*
 Shared Excel Platform - canonical persistence model.
 This migration is additive. Legacy module-specific Excel tables are intentionally
 not dropped here; they must be migrated and verified before removal.
*/

IF OBJECT_ID(N'dbo.F03ExcelSchemas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelSchemas
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelSchemas PRIMARY KEY,
        ModuleCode NVARCHAR(100) NOT NULL,
        EntityCode NVARCHAR(100) NOT NULL,
        SchemaCode NVARCHAR(150) NOT NULL,
        SchemaName NVARCHAR(250) NOT NULL,
        Description NVARCHAR(1000) NULL,
        Status INT NOT NULL CONSTRAINT DF_F03ExcelSchemas_Status DEFAULT (0),
        CurrentVersionId INT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_F03ExcelSchemas_IsActive DEFAULT (1),
        CreatedBy NVARCHAR(100) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelSchemas_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedBy NVARCHAR(100) NULL,
        UpdatedAt DATETIME2(0) NULL,
        CONSTRAINT UQ_F03ExcelSchemas_ModuleEntityCode UNIQUE (ModuleCode, EntityCode, SchemaCode)
    );
END;
GO

IF OBJECT_ID(N'dbo.F03ExcelSchemaVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelSchemaVersions
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelSchemaVersions PRIMARY KEY,
        SchemaId INT NOT NULL,
        VersionNo INT NOT NULL,
        SheetIndex INT NOT NULL,
        SheetName NVARCHAR(255) NOT NULL,
        HeaderRowIndex INT NOT NULL,
        DataStartRowIndex INT NOT NULL,
        DataEndRowIndex INT NULL,
        Status INT NOT NULL CONSTRAINT DF_F03ExcelSchemaVersions_Status DEFAULT (0),
        SourceFileName NVARCHAR(500) NULL,
        CreatedBy NVARCHAR(100) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelSchemaVersions_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_F03ExcelSchemaVersions_Schema FOREIGN KEY (SchemaId) REFERENCES dbo.F03ExcelSchemas(Id),
        CONSTRAINT UQ_F03ExcelSchemaVersions_Version UNIQUE (SchemaId, VersionNo)
    );
END;
GO

IF OBJECT_ID(N'dbo.F03ExcelSchemaFields', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelSchemaFields
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelSchemaFields PRIMARY KEY,
        SchemaVersionId INT NOT NULL,
        FieldKey NVARCHAR(150) NOT NULL,
        FieldType NVARCHAR(50) NOT NULL,
        SourceColumnIndex INT NOT NULL,
        SourceColumnName NVARCHAR(255) NULL,
        HeaderName NVARCHAR(255) NULL,
        Required BIT NOT NULL CONSTRAINT DF_F03ExcelSchemaFields_Required DEFAULT (0),
        MaxLength INT NULL,
        Format NVARCHAR(100) NULL,
        DefaultValue NVARCHAR(1000) NULL,
        ValidationRule NVARCHAR(2000) NULL,
        DisplayOrder INT NOT NULL CONSTRAINT DF_F03ExcelSchemaFields_DisplayOrder DEFAULT (0),
        CONSTRAINT FK_F03ExcelSchemaFields_Version FOREIGN KEY (SchemaVersionId) REFERENCES dbo.F03ExcelSchemaVersions(Id),
        CONSTRAINT UQ_F03ExcelSchemaFields_Field UNIQUE (SchemaVersionId, FieldKey)
    );
END;
GO

IF OBJECT_ID(N'dbo.F03ExcelImportBatches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelImportBatches
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelImportBatches PRIMARY KEY,
        SchemaId INT NOT NULL,
        SchemaVersionId INT NOT NULL,
        ModuleCode NVARCHAR(100) NOT NULL,
        EntityCode NVARCHAR(100) NOT NULL,
        FileName NVARCHAR(500) NOT NULL,
        FileHash NVARCHAR(128) NULL,
        Status INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_Status DEFAULT (0),
        TotalRows INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_TotalRows DEFAULT (0),
        ValidRows INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_ValidRows DEFAULT (0),
        InvalidRows INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_InvalidRows DEFAULT (0),
        ImportedRows INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_ImportedRows DEFAULT (0),
        FailedRows INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_FailedRows DEFAULT (0),
        CreatedBy NVARCHAR(100) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelImportBatches_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CompletedAt DATETIME2(0) NULL,
        CONSTRAINT FK_F03ExcelImportBatches_Schema FOREIGN KEY (SchemaId) REFERENCES dbo.F03ExcelSchemas(Id),
        CONSTRAINT FK_F03ExcelImportBatches_Version FOREIGN KEY (SchemaVersionId) REFERENCES dbo.F03ExcelSchemaVersions(Id)
    );
END;
GO

IF OBJECT_ID(N'dbo.F03ExcelImportRows', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelImportRows
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelImportRows PRIMARY KEY,
        BatchId BIGINT NOT NULL,
        RowNumber INT NOT NULL,
        Status INT NOT NULL CONSTRAINT DF_F03ExcelImportRows_Status DEFAULT (0),
        RawDataJson NVARCHAR(MAX) NULL,
        NormalizedDataJson NVARCHAR(MAX) NULL,
        ErrorCount INT NOT NULL CONSTRAINT DF_F03ExcelImportRows_ErrorCount DEFAULT (0),
        CONSTRAINT FK_F03ExcelImportRows_Batch FOREIGN KEY (BatchId) REFERENCES dbo.F03ExcelImportBatches(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_F03ExcelImportRows_Row UNIQUE (BatchId, RowNumber)
    );
END;
GO

IF OBJECT_ID(N'dbo.F03ExcelImportErrors', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelImportErrors
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelImportErrors PRIMARY KEY,
        BatchId BIGINT NOT NULL,
        RowId BIGINT NULL,
        RowNumber INT NOT NULL,
        ColumnIndex INT NULL,
        ColumnName NVARCHAR(255) NULL,
        FieldKey NVARCHAR(150) NULL,
        ErrorCode NVARCHAR(100) NOT NULL,
        Severity INT NOT NULL CONSTRAINT DF_F03ExcelImportErrors_Severity DEFAULT (2),
        ErrorMessage NVARCHAR(2000) NOT NULL,
        RawValue NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelImportErrors_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_F03ExcelImportErrors_Batch FOREIGN KEY (BatchId) REFERENCES dbo.F03ExcelImportBatches(Id) ON DELETE CASCADE,
        CONSTRAINT FK_F03ExcelImportErrors_Row FOREIGN KEY (RowId) REFERENCES dbo.F03ExcelImportRows(Id) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03ExcelSchemas_ModuleEntity_Status' AND object_id = OBJECT_ID(N'dbo.F03ExcelSchemas'))
    CREATE INDEX IX_F03ExcelSchemas_ModuleEntity_Status ON dbo.F03ExcelSchemas(ModuleCode, EntityCode, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03ExcelImportBatches_ModuleEntity_Status' AND object_id = OBJECT_ID(N'dbo.F03ExcelImportBatches'))
    CREATE INDEX IX_F03ExcelImportBatches_ModuleEntity_Status ON dbo.F03ExcelImportBatches(ModuleCode, EntityCode, Status, CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03ExcelImportErrors_Batch_Row' AND object_id = OBJECT_ID(N'dbo.F03ExcelImportErrors'))
    CREATE INDEX IX_F03ExcelImportErrors_Batch_Row ON dbo.F03ExcelImportErrors(BatchId, RowNumber);
GO

/*
 Lifecycle contract:
 SchemaStatus: 0 Draft, 1 Active, 2 Retired.
 ImportBatchStatus follows the shared Core enum.
 Draft deletion is a soft lifecycle operation at service level: only Draft schemas
 without committed/staged dependencies may be physically deleted.
*/
