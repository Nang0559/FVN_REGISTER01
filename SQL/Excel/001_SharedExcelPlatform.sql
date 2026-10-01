/* Shared Excel Platform: run once per database. Idempotent. */
IF OBJECT_ID(N'dbo.F03ExcelSchemas',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.F03ExcelSchemas(
  Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelSchemas PRIMARY KEY,
  ModuleCode nvarchar(64) NOT NULL,
  EntityCode nvarchar(128) NOT NULL,
  SchemaKey nvarchar(128) NOT NULL,
  SchemaName nvarchar(256) NOT NULL,
  Version int NOT NULL CONSTRAINT DF_F03ExcelSchemas_Version DEFAULT(1),
  Status int NOT NULL CONSTRAINT DF_F03ExcelSchemas_Status DEFAULT(0),
  SourceFileName nvarchar(512) NULL,
  DefinitionJson nvarchar(max) NOT NULL,
  IsDeleted bit NOT NULL CONSTRAINT DF_F03ExcelSchemas_IsDeleted DEFAULT(0),
  CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_F03ExcelSchemas_CreatedAt DEFAULT SYSUTCDATETIME(),
  UpdatedAt datetime2(0) NULL
 );
 CREATE UNIQUE INDEX UX_F03ExcelSchemas_Active ON dbo.F03ExcelSchemas(ModuleCode,EntityCode,SchemaKey) WHERE Status=1 AND IsDeleted=0;
 CREATE INDEX IX_F03ExcelSchemas_Lookup ON dbo.F03ExcelSchemas(ModuleCode,EntityCode,Status,IsDeleted);
END;
IF OBJECT_ID(N'dbo.F03ExcelImportBatches',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.F03ExcelImportBatches(
  Id uniqueidentifier NOT NULL CONSTRAINT PK_F03ExcelImportBatches PRIMARY KEY,
  ModuleCode nvarchar(64) NOT NULL,
  EntityCode nvarchar(128) NOT NULL,
  SchemaId int NOT NULL,
  FileName nvarchar(512) NOT NULL,
  FileHash nvarchar(128) NULL,
  Status int NOT NULL CONSTRAINT DF_F03ExcelImportBatches_Status DEFAULT(0),
  TotalRows int NOT NULL CONSTRAINT DF_F03ExcelImportBatches_TotalRows DEFAULT(0),
  ValidRows int NOT NULL CONSTRAINT DF_F03ExcelImportBatches_ValidRows DEFAULT(0),
  InvalidRows int NOT NULL CONSTRAINT DF_F03ExcelImportBatches_InvalidRows DEFAULT(0),
  ImportedRows int NOT NULL CONSTRAINT DF_F03ExcelImportBatches_ImportedRows DEFAULT(0),
  CreatedBy nvarchar(128) NULL,
  CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_F03ExcelImportBatches_CreatedAt DEFAULT SYSUTCDATETIME(),
  CompletedAt datetime2(0) NULL,
  CONSTRAINT FK_F03ExcelImportBatches_Schema FOREIGN KEY(SchemaId) REFERENCES dbo.F03ExcelSchemas(Id)
 );
 CREATE INDEX IX_F03ExcelImportBatches_Module ON dbo.F03ExcelImportBatches(ModuleCode,EntityCode,CreatedAt DESC);
END;
IF OBJECT_ID(N'dbo.F03ExcelImportErrors',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.F03ExcelImportErrors(
  Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03ExcelImportErrors PRIMARY KEY,
  BatchId uniqueidentifier NOT NULL,
  RowNumber int NOT NULL,
  ColumnIndex int NULL,
  FieldKey nvarchar(128) NULL,
  ErrorCode nvarchar(64) NOT NULL,
  ErrorMessage nvarchar(1000) NOT NULL,
  RawValue nvarchar(max) NULL,
  Severity int NOT NULL CONSTRAINT DF_F03ExcelImportErrors_Severity DEFAULT(2),
  CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_F03ExcelImportErrors_CreatedAt DEFAULT SYSUTCDATETIME(),
  CONSTRAINT FK_F03ExcelImportErrors_Batch FOREIGN KEY(BatchId) REFERENCES dbo.F03ExcelImportBatches(Id) ON DELETE CASCADE
 );
 CREATE INDEX IX_F03ExcelImportErrors_Batch ON dbo.F03ExcelImportErrors(BatchId,RowNumber);
END;
