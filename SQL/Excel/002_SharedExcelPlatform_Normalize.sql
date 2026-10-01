/*
  Shared Excel Platform canonical storage.
  This script is additive and idempotent. Legacy module-specific tables must be
  migrated only after their actual column contract has been verified in the
  target database. No legacy table is dropped by this script.
*/

IF OBJECT_ID(N'dbo.F03ExcelSchemas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelSchemas
    (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelSchemas PRIMARY KEY,
        ModuleCode         NVARCHAR(100) NOT NULL,
        EntityCode         NVARCHAR(150) NOT NULL,
        SchemaKey          NVARCHAR(200) NOT NULL,
        SchemaName         NVARCHAR(250) NOT NULL,
        Description        NVARCHAR(1000) NULL,
        Status             INT NOT NULL CONSTRAINT DF_F03ExcelSchemas_Status DEFAULT (0),
        CurrentVersionId   INT NULL,
        SourceFileName     NVARCHAR(260) NULL,
        IsActive           BIT NOT NULL CONSTRAINT DF_F03ExcelSchemas_IsActive DEFAULT (0),
        CreatedBy          NVARCHAR(100) NULL,
        CreatedAt          DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelSchemas_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedBy          NVARCHAR(100) NULL,
        UpdatedAt          DATETIME2(0) NULL
    );

    CREATE UNIQUE INDEX UX_F03ExcelSchemas_Module_Entity_Key
        ON dbo.F03ExcelSchemas(ModuleCode, EntityCode, SchemaKey);
END;
GO

IF OBJECT_ID(N'dbo.F03ExcelSchemaVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelSchemaVersions
    (
        Id                   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelSchemaVersions PRIMARY KEY,
        SchemaId             INT NOT NULL,
        VersionNo            INT NOT NULL,
        Status               INT NOT NULL CONSTRAINT DF_F03ExcelSchemaVersions_Status DEFAULT (0),
        SheetIndex            INT NOT NULL,
        SheetName             NVARCHAR(250) NOT NULL,
        HeaderRowIndex        INT NOT NULL,
        DataStartRowIndex    INT NOT NULL,
        DataEndRowIndex      INT NULL,
        SelectedColumnsJson  NVARCHAR(MAX) NOT NULL,
        Culture               NVARCHAR(20) NULL,
        CreatedBy             NVARCHAR(100) NULL,
        CreatedAt             DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelSchemaVersions_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_F03ExcelSchemaVersions_Schema FOREIGN KEY (SchemaId) REFERENCES dbo.F03ExcelSchemas(Id),
        CONSTRAINT UX_F03ExcelSchemaVersions_Schema_Version UNIQUE (SchemaId, VersionNo)
    );
END;
GO

IF OBJECT_ID(N'dbo.F03ExcelSchemaFields', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelSchemaFields
    (
        Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelSchemaFields PRIMARY KEY,
        SchemaVersionId     INT NOT NULL,
        FieldKey             NVARCHAR(200) NOT NULL,
        DataType             NVARCHAR(50) NOT NULL,
        SourceColumnIndex   INT NOT NULL,
        SourceColumnName    NVARCHAR(250) NULL,
        HeaderName          NVARCHAR(250) NULL,
        ResourceKey         NVARCHAR(250) NULL,
        TargetProperty      NVARCHAR(250) NULL,
        Format              NVARCHAR(100) NULL,
        ValidationRule      NVARCHAR(1000) NULL,
        DefaultValue        NVARCHAR(500) NULL,
        IsRequired          BIT NOT NULL CONSTRAINT DF_F03ExcelSchemaFields_IsRequired DEFAULT (0),
        AllowEmpty          BIT NOT NULL CONSTRAINT DF_F03ExcelSchemaFields_AllowEmpty DEFAULT (1),
        DisplayOrder        INT NOT NULL CONSTRAINT DF_F03ExcelSchemaFields_DisplayOrder DEFAULT (0),
        CONSTRAINT FK_F03ExcelSchemaFields_Version FOREIGN KEY (SchemaVersionId) REFERENCES dbo.F03ExcelSchemaVersions(Id),
        CONSTRAINT UX_F03ExcelSchemaFields_Version_Field UNIQUE (SchemaVersionId, FieldKey)
    );
END;
GO

IF OBJECT_ID(N'dbo.F03ExcelImportBatches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelImportBatches
    (
        Id                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_F03ExcelImportBatches PRIMARY KEY,
        ModuleCode         NVARCHAR(100) NOT NULL,
        EntityCode         NVARCHAR(150) NOT NULL,
        SchemaId           INT NOT NULL,
        SchemaVersionId    INT NULL,
        FileName           NVARCHAR(260) NOT NULL,
        FileHash           NVARCHAR(128) NULL,
        Status             INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_Status DEFAULT (0),
        TotalRows          INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_TotalRows DEFAULT (0),
        ValidRows          INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_ValidRows DEFAULT (0),
        InvalidRows        INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_InvalidRows DEFAULT (0),
        ImportedRows       INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_ImportedRows DEFAULT (0),
        FailedRows         INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_FailedRows DEFAULT (0),
        CreatedBy          NVARCHAR(100) NULL,
        CreatedAt          DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelImportBatches_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CompletedAt        DATETIME2(0) NULL,
        CONSTRAINT FK_F03ExcelImportBatches_Schema FOREIGN KEY (SchemaId) REFERENCES dbo.F03ExcelSchemas(Id),
        CONSTRAINT FK_F03ExcelImportBatches_Version FOREIGN KEY (SchemaVersionId) REFERENCES dbo.F03ExcelSchemaVersions(Id)
    );
    CREATE INDEX IX_F03ExcelImportBatches_Module_Entity_CreatedAt
        ON dbo.F03ExcelImportBatches(ModuleCode, EntityCode, CreatedAt DESC);
END;
GO

IF OBJECT_ID(N'dbo.F03ExcelImportRows', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelImportRows
    (
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelImportRows PRIMARY KEY,
        BatchId        UNIQUEIDENTIFIER NOT NULL,
        RowNumber      INT NOT NULL,
        Status         INT NOT NULL CONSTRAINT DF_F03ExcelImportRows_Status DEFAULT (0),
        RawJson        NVARCHAR(MAX) NULL,
        NormalizedJson NVARCHAR(MAX) NULL,
        ErrorCount     INT NOT NULL CONSTRAINT DF_F03ExcelImportRows_ErrorCount DEFAULT (0),
        CONSTRAINT FK_F03ExcelImportRows_Batch FOREIGN KEY (BatchId) REFERENCES dbo.F03ExcelImportBatches(Id) ON DELETE CASCADE,
        CONSTRAINT UX_F03ExcelImportRows_Batch_Row UNIQUE (BatchId, RowNumber)
    );
END;
GO

IF OBJECT_ID(N'dbo.F03ExcelImportErrors', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelImportErrors
    (
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelImportErrors PRIMARY KEY,
        BatchId        UNIQUEIDENTIFIER NOT NULL,
        RowId          BIGINT NULL,
        RowNumber      INT NOT NULL,
        ColumnIndex    INT NULL,
        FieldKey       NVARCHAR(200) NULL,
        ErrorCode      NVARCHAR(100) NOT NULL,
        Severity       INT NOT NULL CONSTRAINT DF_F03ExcelImportErrors_Severity DEFAULT (2),
        ErrorMessage   NVARCHAR(2000) NOT NULL,
        RawValue       NVARCHAR(2000) NULL,
        CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelImportErrors_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_F03ExcelImportErrors_Batch FOREIGN KEY (BatchId) REFERENCES dbo.F03ExcelImportBatches(Id) ON DELETE CASCADE,
        CONSTRAINT FK_F03ExcelImportErrors_Row FOREIGN KEY (RowId) REFERENCES dbo.F03ExcelImportRows(Id) ON DELETE NO ACTION
    );
    CREATE INDEX IX_F03ExcelImportErrors_Batch_Row ON dbo.F03ExcelImportErrors(BatchId, RowNumber);
END;
GO

IF OBJECT_ID(N'dbo.F03ExcelExportTemplates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelExportTemplates
    (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelExportTemplates PRIMARY KEY,
        ModuleCode     NVARCHAR(100) NOT NULL,
        EntityCode     NVARCHAR(150) NOT NULL,
        SchemaId       INT NULL,
        TemplateKey    NVARCHAR(200) NOT NULL,
        TemplateName   NVARCHAR(250) NOT NULL,
        Culture        NVARCHAR(20) NULL,
        DefinitionJson NVARCHAR(MAX) NOT NULL,
        IsActive       BIT NOT NULL CONSTRAINT DF_F03ExcelExportTemplates_IsActive DEFAULT (1),
        CreatedBy      NVARCHAR(100) NULL,
        CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelExportTemplates_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedBy      NVARCHAR(100) NULL,
        UpdatedAt      DATETIME2(0) NULL,
        CONSTRAINT FK_F03ExcelExportTemplates_Schema FOREIGN KEY (SchemaId) REFERENCES dbo.F03ExcelSchemas(Id)
    );
    CREATE UNIQUE INDEX UX_F03ExcelExportTemplates_Module_Entity_Key
        ON dbo.F03ExcelExportTemplates(ModuleCode, EntityCode, TemplateKey);
END;
GO

/* Current active schema must always be recoverable from one canonical location. */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_F03ExcelSchemas_CurrentVersion')
BEGIN
    ALTER TABLE dbo.F03ExcelSchemas WITH CHECK
        ADD CONSTRAINT FK_F03ExcelSchemas_CurrentVersion
        FOREIGN KEY (CurrentVersionId) REFERENCES dbo.F03ExcelSchemaVersions(Id);
END;
GO
