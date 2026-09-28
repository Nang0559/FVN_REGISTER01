/*
  Department Data Schema / versioned import metadata.
  The table is intentionally kept under the Equipment namespace for the first
  consumer, but the model is reusable: SchemaKind identifies future consumers
  such as Checklist, Attendance and Payroll.

  Rules:
    - CreatedBy is the owner/editor of the schema family.
    - Users with the Import/Schema capability in the same department can view,
      use and clone other users' schemas, but cannot edit them.
    - Clone creates a new SchemaKey and therefore an independent schema family.
    - Versions within one SchemaKey belong to the same owner and are immutable
      once Active; a new Draft version is created for later changes.
*/

IF OBJECT_ID(N'dbo.F03EquipmentSchemas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentSchemas
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentSchemas PRIMARY KEY,
        DeptCode nvarchar(20) NOT NULL,
        SchemaName nvarchar(150) NOT NULL,
        SchemaKind nvarchar(20) NOT NULL CONSTRAINT DF_F03EquipmentSchemas_SchemaKind DEFAULT(N'Equipment'),
        SchemaKey nvarchar(64) NOT NULL,
        Version int NOT NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_F03EquipmentSchemas_Status DEFAULT(N'Draft'),
        SourceSchemaId int NULL,
        SourceFileName nvarchar(260) NULL,
        CreatedFromExcel bit NOT NULL CONSTRAINT DF_F03EquipmentSchemas_CreatedFromExcel DEFAULT(0),
        IsActive bit NULL CONSTRAINT DF_F03EquipmentSchemas_IsActive DEFAULT(1),
        CreatedBy int NOT NULL,
        LastModifiedSource nvarchar(200) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_F03EquipmentSchemas_CreatedAt DEFAULT(SYSDATETIME()),
        ModifiedBy int NULL,
        ModifiedAt datetime2 NULL
    );
END;
GO

/* Safe upgrade for databases that already have the original schema table. */
IF COL_LENGTH(N'dbo.F03EquipmentSchemas', N'SchemaKind') IS NULL
    ALTER TABLE dbo.F03EquipmentSchemas ADD SchemaKind nvarchar(20) NOT NULL CONSTRAINT DF_F03EquipmentSchemas_SchemaKind DEFAULT(N'Equipment');
IF COL_LENGTH(N'dbo.F03EquipmentSchemas', N'SchemaKey') IS NULL
    ALTER TABLE dbo.F03EquipmentSchemas ADD SchemaKey nvarchar(64) NULL;
IF COL_LENGTH(N'dbo.F03EquipmentSchemas', N'SourceSchemaId') IS NULL
    ALTER TABLE dbo.F03EquipmentSchemas ADD SourceSchemaId int NULL;
IF COL_LENGTH(N'dbo.F03EquipmentSchemas', N'SourceFileName') IS NULL
    ALTER TABLE dbo.F03EquipmentSchemas ADD SourceFileName nvarchar(260) NULL;
IF COL_LENGTH(N'dbo.F03EquipmentSchemas', N'CreatedFromExcel') IS NULL
    ALTER TABLE dbo.F03EquipmentSchemas ADD CreatedFromExcel bit NOT NULL CONSTRAINT DF_F03EquipmentSchemas_CreatedFromExcel DEFAULT(0);
GO

/* Existing versions are grouped by department + schema name. */
UPDATE s
SET SchemaKey = CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT(UPPER(LTRIM(RTRIM(s.DeptCode))), N'|', UPPER(LTRIM(RTRIM(s.SchemaName))))), 2)
FROM dbo.F03EquipmentSchemas s
WHERE NULLIF(LTRIM(RTRIM(s.SchemaKey)), N'') IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.F03EquipmentSchemas') AND name = N'SchemaKey' AND is_nullable = 1)
    ALTER TABLE dbo.F03EquipmentSchemas ALTER COLUMN SchemaKey nvarchar(64) NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_F03EquipmentSchemas_SourceSchema')
BEGIN
    ALTER TABLE dbo.F03EquipmentSchemas WITH CHECK
        ADD CONSTRAINT FK_F03EquipmentSchemas_SourceSchema
        FOREIGN KEY(SourceSchemaId) REFERENCES dbo.F03EquipmentSchemas(Id) ON DELETE NO ACTION;
END;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_F03EquipmentSchemas_Dept_Version' AND object_id = OBJECT_ID(N'dbo.F03EquipmentSchemas'))
    DROP INDEX UX_F03EquipmentSchemas_Dept_Version ON dbo.F03EquipmentSchemas;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_F03EquipmentSchemas_Dept_SchemaKey_Version' AND object_id = OBJECT_ID(N'dbo.F03EquipmentSchemas'))
    CREATE UNIQUE INDEX UX_F03EquipmentSchemas_Dept_SchemaKey_Version
        ON dbo.F03EquipmentSchemas(DeptCode, SchemaKey, Version);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03EquipmentSchemas_Dept_Status' AND object_id = OBJECT_ID(N'dbo.F03EquipmentSchemas'))
    CREATE INDEX IX_F03EquipmentSchemas_Dept_Status ON dbo.F03EquipmentSchemas(DeptCode, Status);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03EquipmentSchemas_Dept_SchemaKey' AND object_id = OBJECT_ID(N'dbo.F03EquipmentSchemas'))
    CREATE INDEX IX_F03EquipmentSchemas_Dept_SchemaKey ON dbo.F03EquipmentSchemas(DeptCode, SchemaKey, Version DESC);
GO

IF OBJECT_ID(N'dbo.F03EquipmentFieldDefinitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentFieldDefinitions
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentFieldDefinitions PRIMARY KEY,
        SchemaId int NOT NULL,
        DeptCode nvarchar(20) NOT NULL,
        FieldKey nvarchar(60) NOT NULL,
        FieldLabel nvarchar(150) NOT NULL,
        DataType nvarchar(20) NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_DataType DEFAULT(N'Text'),
        IsRequired bit NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_IsRequired DEFAULT(0),
        IsImportable bit NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_IsImportable DEFAULT(1),
        IsSearchable bit NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_IsSearchable DEFAULT(0),
        IsActiveField bit NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_IsActiveField DEFAULT(1),
        DisplayOrder int NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_DisplayOrder DEFAULT(0),
        MaxLength int NULL,
        DefaultValue nvarchar(500) NULL,
        OptionsJson nvarchar(2000) NULL,
        IsActive bit NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_IsActive DEFAULT(1),
        CreatedBy int NOT NULL,
        LastModifiedSource nvarchar(200) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_F03EquipmentFieldDefinitions_CreatedAt DEFAULT(SYSDATETIME()),
        ModifiedBy int NULL,
        ModifiedAt datetime2 NULL
    );
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.F03EquipmentFieldDefinitions', 'SchemaId') IS NULL
        ALTER TABLE dbo.F03EquipmentFieldDefinitions ADD SchemaId int NULL;
    IF COL_LENGTH('dbo.F03EquipmentFieldDefinitions', 'MaxLength') IS NULL
        ALTER TABLE dbo.F03EquipmentFieldDefinitions ADD MaxLength int NULL;
    IF COL_LENGTH('dbo.F03EquipmentFieldDefinitions', 'DefaultValue') IS NULL
        ALTER TABLE dbo.F03EquipmentFieldDefinitions ADD DefaultValue nvarchar(500) NULL;
END;
GO

/* Bootstrap one active schema for each department represented by legacy fields. */
DECLARE @DeptCode nvarchar(20), @SchemaId int;
DECLARE dept_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT DISTINCT UPPER(LTRIM(RTRIM(DeptCode)))
    FROM dbo.F03EquipmentFieldDefinitions
    WHERE NULLIF(LTRIM(RTRIM(DeptCode)), N'') IS NOT NULL;
OPEN dept_cursor;
FETCH NEXT FROM dept_cursor INTO @DeptCode;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT TOP (1) @SchemaId = Id
    FROM dbo.F03EquipmentSchemas
    WHERE DeptCode = @DeptCode AND Status = N'Active' AND IsActive = 1
    ORDER BY Version DESC, Id DESC;

    IF @SchemaId IS NULL
    BEGIN
        DECLARE @SchemaKey nvarchar(64) = CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT(@DeptCode, N'|Equipment')), 2);
        INSERT dbo.F03EquipmentSchemas(DeptCode, SchemaName, SchemaKind, SchemaKey, Version, Status, IsActive, CreatedBy)
        VALUES(@DeptCode, CONCAT(@DeptCode, N' Equipment'), N'Equipment', @SchemaKey, 1, N'Active', 1, 0);
        SET @SchemaId = SCOPE_IDENTITY();
    END;

    UPDATE dbo.F03EquipmentFieldDefinitions
    SET SchemaId = @SchemaId
    WHERE UPPER(LTRIM(RTRIM(DeptCode))) = @DeptCode
      AND (SchemaId IS NULL OR SchemaId = 0);

    FETCH NEXT FROM dept_cursor INTO @DeptCode;
END;
CLOSE dept_cursor;
DEALLOCATE dept_cursor;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.F03EquipmentFieldDefinitions') AND name = N'SchemaId' AND is_nullable = 1)
    ALTER TABLE dbo.F03EquipmentFieldDefinitions ALTER COLUMN SchemaId int NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_F03EquipmentFieldDefinitions_F03EquipmentSchemas')
BEGIN
    ALTER TABLE dbo.F03EquipmentFieldDefinitions WITH CHECK
        ADD CONSTRAINT FK_F03EquipmentFieldDefinitions_F03EquipmentSchemas
        FOREIGN KEY(SchemaId) REFERENCES dbo.F03EquipmentSchemas(Id) ON DELETE CASCADE;
END;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_F03EquipmentFieldDefinitions_Dept_FieldKey' AND object_id = OBJECT_ID(N'dbo.F03EquipmentFieldDefinitions'))
    DROP INDEX UX_F03EquipmentFieldDefinitions_Dept_FieldKey ON dbo.F03EquipmentFieldDefinitions;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_F03EquipmentFieldDefinitions_Schema_FieldKey' AND object_id = OBJECT_ID(N'dbo.F03EquipmentFieldDefinitions'))
    CREATE UNIQUE INDEX UX_F03EquipmentFieldDefinitions_Schema_FieldKey
        ON dbo.F03EquipmentFieldDefinitions(SchemaId, FieldKey);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03EquipmentFieldDefinitions_Dept_Active' AND object_id = OBJECT_ID(N'dbo.F03EquipmentFieldDefinitions'))
    CREATE INDEX IX_F03EquipmentFieldDefinitions_Dept_Active
        ON dbo.F03EquipmentFieldDefinitions(DeptCode, IsActive, IsActiveField, DisplayOrder);
GO

/* Import batches are bound to the exact schema version used for staging. */
IF OBJECT_ID(N'dbo.F03EquipmentImportBatches', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.F03EquipmentImportBatches', N'SchemaId') IS NULL
        ALTER TABLE dbo.F03EquipmentImportBatches ADD SchemaId int NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_F03EquipmentImportBatches_F03EquipmentSchemas')
        ALTER TABLE dbo.F03EquipmentImportBatches WITH CHECK
            ADD CONSTRAINT FK_F03EquipmentImportBatches_F03EquipmentSchemas
            FOREIGN KEY(SchemaId) REFERENCES dbo.F03EquipmentSchemas(Id) ON DELETE NO ACTION;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_F03EquipmentImportBatches_Dept_Schema_Status' AND object_id = OBJECT_ID(N'dbo.F03EquipmentImportBatches'))
        CREATE INDEX IX_F03EquipmentImportBatches_Dept_Schema_Status
            ON dbo.F03EquipmentImportBatches(DeptCode, SchemaId, Status);
END;
GO

/* Only fields belonging to the active version are importable by legacy callers. */
UPDATE f
SET f.IsActiveField = CASE WHEN s.Status = N'Active' AND s.IsActive = 1 THEN 1 ELSE 0 END
FROM dbo.F03EquipmentFieldDefinitions f
JOIN dbo.F03EquipmentSchemas s ON s.Id = f.SchemaId;
GO
