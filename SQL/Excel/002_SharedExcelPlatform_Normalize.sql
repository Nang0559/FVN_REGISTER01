/*
 Shared Excel Platform - legacy upgrade only.
 001_SharedExcelPlatform.sql is the canonical owner for new databases.
 This script only upgrades an older F03ExcelSchemas shape and creates missing
 dependent objects needed to complete the canonical model.
*/

IF OBJECT_ID(N'dbo.F03ExcelSchemas', N'U') IS NULL
    THROW 51001, 'F03ExcelSchemas must be created by 001_SharedExcelPlatform.sql first.', 1;
GO

/* Upgrade an older single-table schema store without recreating the canonical table. */
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'ModuleCode') IS NULL ALTER TABLE dbo.F03ExcelSchemas ADD ModuleCode NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'EntityCode') IS NULL ALTER TABLE dbo.F03ExcelSchemas ADD EntityCode NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'SchemaCode') IS NULL AND COL_LENGTH(N'dbo.F03ExcelSchemas', N'SchemaKey') IS NOT NULL ALTER TABLE dbo.F03ExcelSchemas ADD SchemaCode NVARCHAR(150) NULL;
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'SchemaName') IS NULL ALTER TABLE dbo.F03ExcelSchemas ADD SchemaName NVARCHAR(250) NULL;
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'Description') IS NULL ALTER TABLE dbo.F03ExcelSchemas ADD Description NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'Status') IS NULL ALTER TABLE dbo.F03ExcelSchemas ADD Status INT NOT NULL CONSTRAINT DF_F03ExcelSchemas_Status_Migration DEFAULT(0);
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'CurrentVersionId') IS NULL ALTER TABLE dbo.F03ExcelSchemas ADD CurrentVersionId INT NULL;
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'SourceFileName') IS NULL ALTER TABLE dbo.F03ExcelSchemas ADD SourceFileName NVARCHAR(500) NULL;
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'IsActive') IS NULL ALTER TABLE dbo.F03ExcelSchemas ADD IsActive BIT NOT NULL CONSTRAINT DF_F03ExcelSchemas_IsActive_Migration DEFAULT(0);
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'CreatedBy') IS NULL ALTER TABLE dbo.F03ExcelSchemas ADD CreatedBy NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'CreatedAt') IS NULL ALTER TABLE dbo.F03ExcelSchemas ADD CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelSchemas_CreatedAt_Migration DEFAULT(SYSUTCDATETIME());
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'UpdatedBy') IS NULL ALTER TABLE dbo.F03ExcelSchemas ADD UpdatedBy NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'UpdatedAt') IS NULL ALTER TABLE dbo.F03ExcelSchemas ADD UpdatedAt DATETIME2(0) NULL;
GO

/* SchemaKey exists only on the legacy table shape. A statement that names a missing column of an
   EXISTING table fails at batch compile time (Msg 207) even inside a false IF, so it must be dynamic SQL. */
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'SchemaCode') IS NOT NULL AND COL_LENGTH(N'dbo.F03ExcelSchemas', N'SchemaKey') IS NOT NULL
    EXEC sys.sp_executesql N'UPDATE dbo.F03ExcelSchemas SET SchemaCode=COALESCE(NULLIF(SchemaCode,N''''),SchemaKey,CONCAT(N''LEGACY-'',Id)) WHERE SchemaCode IS NULL OR SchemaCode=N'''';';
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
        Status INT NOT NULL CONSTRAINT DF_F03ExcelSchemaVersions_Status DEFAULT(0),
        SourceFileName NVARCHAR(500) NULL,
        SelectedColumnsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_F03ExcelSchemaVersions_SelectedColumns DEFAULT(N'[]'),
        Culture NVARCHAR(20) NOT NULL CONSTRAINT DF_F03ExcelSchemaVersions_Culture DEFAULT(N'vi-VN'),
        CreatedBy NVARCHAR(100) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelSchemaVersions_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_F03ExcelSchemaVersions_Schema FOREIGN KEY(SchemaId) REFERENCES dbo.F03ExcelSchemas(Id),
        CONSTRAINT UQ_F03ExcelSchemaVersions_Version UNIQUE(SchemaId,VersionNo)
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
        DataType NVARCHAR(50) NOT NULL,
        SourceColumnIndex INT NOT NULL,
        SourceColumnName NVARCHAR(255) NULL,
        HeaderName NVARCHAR(255) NULL,
        ResourceKey NVARCHAR(255) NULL,
        TargetProperty NVARCHAR(255) NULL,
        Format NVARCHAR(100) NULL,
        ValidationRule NVARCHAR(2000) NULL,
        DefaultValue NVARCHAR(1000) NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_F03ExcelSchemaFields_Required DEFAULT(0),
        AllowEmpty BIT NOT NULL CONSTRAINT DF_F03ExcelSchemaFields_AllowEmpty DEFAULT(1),
        MaxLength INT NULL,
        DisplayOrder INT NOT NULL CONSTRAINT DF_F03ExcelSchemaFields_DisplayOrder DEFAULT(0),
        CONSTRAINT FK_F03ExcelSchemaFields_Version FOREIGN KEY(SchemaVersionId) REFERENCES dbo.F03ExcelSchemaVersions(Id),
        CONSTRAINT UQ_F03ExcelSchemaFields_Field UNIQUE(SchemaVersionId,FieldKey)
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
        Status INT NOT NULL CONSTRAINT DF_F03ExcelImportBatches_Status DEFAULT(0),
        TotalRows INT NOT NULL DEFAULT(0), ValidRows INT NOT NULL DEFAULT(0), InvalidRows INT NOT NULL DEFAULT(0), ImportedRows INT NOT NULL DEFAULT(0), FailedRows INT NOT NULL DEFAULT(0),
        CreatedBy NVARCHAR(100) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelImportBatches_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CompletedAt DATETIME2(0) NULL,
        CONSTRAINT FK_F03ExcelImportBatches_Schema FOREIGN KEY(SchemaId) REFERENCES dbo.F03ExcelSchemas(Id),
        CONSTRAINT FK_F03ExcelImportBatches_Version FOREIGN KEY(SchemaVersionId) REFERENCES dbo.F03ExcelSchemaVersions(Id)
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
        Status INT NOT NULL DEFAULT(0),
        RawDataJson NVARCHAR(MAX) NULL,
        NormalizedDataJson NVARCHAR(MAX) NULL,
        ErrorCount INT NOT NULL DEFAULT(0),
        CONSTRAINT FK_F03ExcelImportRows_Batch FOREIGN KEY(BatchId) REFERENCES dbo.F03ExcelImportBatches(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_F03ExcelImportRows_Row UNIQUE(BatchId,RowNumber)
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
        Severity INT NOT NULL DEFAULT(2),
        ErrorMessage NVARCHAR(2000) NOT NULL,
        RawValue NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03ExcelImportErrors_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_F03ExcelImportErrors_Batch FOREIGN KEY(BatchId) REFERENCES dbo.F03ExcelImportBatches(Id) ON DELETE CASCADE,
        CONSTRAINT FK_F03ExcelImportErrors_Row FOREIGN KEY(RowId) REFERENCES dbo.F03ExcelImportRows(Id)
    );
END;
GO

IF OBJECT_ID(N'dbo.F03ExcelExportTemplates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03ExcelExportTemplates
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelExportTemplates PRIMARY KEY,
        ModuleCode NVARCHAR(100) NOT NULL,
        EntityCode NVARCHAR(100) NOT NULL,
        SchemaId INT NULL,
        TemplateKey NVARCHAR(200) NOT NULL,
        TemplateName NVARCHAR(250) NOT NULL,
        Culture NVARCHAR(20) NULL,
        DefinitionJson NVARCHAR(MAX) NOT NULL,
        IsActive BIT NOT NULL DEFAULT(1),
        CreatedBy NVARCHAR(100) NULL,
        CreatedAt DATETIME2(0) NOT NULL DEFAULT(SYSUTCDATETIME()),
        UpdatedBy NVARCHAR(100) NULL,
        UpdatedAt DATETIME2(0) NULL,
        CONSTRAINT FK_F03ExcelExportTemplates_Schema FOREIGN KEY(SchemaId) REFERENCES dbo.F03ExcelSchemas(Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03ExcelSchemas_CurrentVersion')
    ALTER TABLE dbo.F03ExcelSchemas WITH CHECK ADD CONSTRAINT FK_F03ExcelSchemas_CurrentVersion FOREIGN KEY(CurrentVersionId) REFERENCES dbo.F03ExcelSchemaVersions(Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03ExcelSchemas_Module_Entity_Code' AND object_id=OBJECT_ID(N'dbo.F03ExcelSchemas'))
    CREATE UNIQUE INDEX UX_F03ExcelSchemas_Module_Entity_Code ON dbo.F03ExcelSchemas(ModuleCode,EntityCode,SchemaCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03ExcelImportBatches_Module_Entity_CreatedAt' AND object_id=OBJECT_ID(N'dbo.F03ExcelImportBatches'))
    CREATE INDEX IX_F03ExcelImportBatches_Module_Entity_CreatedAt ON dbo.F03ExcelImportBatches(ModuleCode,EntityCode,CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03ExcelImportErrors_Batch_Row' AND object_id=OBJECT_ID(N'dbo.F03ExcelImportErrors'))
    CREATE INDEX IX_F03ExcelImportErrors_Batch_Row ON dbo.F03ExcelImportErrors(BatchId,RowNumber);
GO
