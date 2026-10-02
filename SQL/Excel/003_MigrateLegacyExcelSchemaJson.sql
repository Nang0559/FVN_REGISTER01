/*
  Migrates the previous F03ExcelSchemas single-row JSON representation when it
  exists. The script is intentionally conditional so installations that already
  use the canonical model remain untouched.
*/

IF OBJECT_ID(N'dbo.F03ExcelSchemas', N'U') IS NULL OR COL_LENGTH(N'dbo.F03ExcelSchemas', N'DefinitionJson') IS NULL
    RETURN;
GO

/* Fill canonical header columns from the old serialized definition. */
UPDATE s
SET
    ModuleCode = COALESCE(NULLIF(s.ModuleCode,N''), JSON_VALUE(s.DefinitionJson,'$.ModuleCode')),
    EntityCode = COALESCE(NULLIF(s.EntityCode,N''), JSON_VALUE(s.DefinitionJson,'$.EntityCode')),
    SchemaKey = COALESCE(NULLIF(s.SchemaKey,N''), JSON_VALUE(s.DefinitionJson,'$.SchemaKey'), CONCAT(N'LEGACY-',s.Id)),
    SchemaName = COALESCE(NULLIF(s.SchemaName,N''), JSON_VALUE(s.DefinitionJson,'$.SchemaName'), CONCAT(N'Legacy schema ',s.Id)),
    SourceFileName = COALESCE(s.SourceFileName, JSON_VALUE(s.DefinitionJson,'$.SourceFileName')),
    IsActive = CASE WHEN s.Status=1 THEN 1 ELSE 0 END
WHERE ISJSON(s.DefinitionJson)=1;
GO

/* Preserve soft-deleted legacy drafts as deleted canonical schemas. */
IF COL_LENGTH(N'dbo.F03ExcelSchemas', N'IsDeleted') IS NOT NULL
BEGIN
    UPDATE s SET Status=920, IsActive=0
    FROM dbo.F03ExcelSchemas s
    WHERE s.IsDeleted=1;
END;
GO

/* One canonical version is created for each legacy schema that has not been migrated. */
INSERT INTO dbo.F03ExcelSchemaVersions
(
    SchemaId, VersionNo, Status, SheetIndex, SheetName, HeaderRowIndex,
    DataStartRowIndex, DataEndRowIndex, SelectedColumnsJson, Culture, CreatedAt
)
SELECT
    s.Id,
    COALESCE(TRY_CONVERT(INT,JSON_VALUE(s.DefinitionJson,'$.Version')),1),
    CASE WHEN s.Status=1 THEN 1 ELSE 0 END,
    COALESCE(TRY_CONVERT(INT,JSON_VALUE(s.DefinitionJson,'$.SheetIndex')),0),
    COALESCE(JSON_VALUE(s.DefinitionJson,'$.SheetName'),N'Sheet1'),
    COALESCE(TRY_CONVERT(INT,JSON_VALUE(s.DefinitionJson,'$.HeaderRowIndex')),0),
    COALESCE(TRY_CONVERT(INT,JSON_VALUE(s.DefinitionJson,'$.DataStartRowIndex')),1),
    TRY_CONVERT(INT,JSON_VALUE(s.DefinitionJson,'$.DataEndRowIndex')),
    COALESCE(JSON_QUERY(s.DefinitionJson,'$.SelectedColumnIndexes'),N'[]'),
    COALESCE(JSON_VALUE(s.DefinitionJson,'$.Culture'),N'vi-VN'),
    COALESCE(s.CreatedAt,SYSUTCDATETIME())
FROM dbo.F03ExcelSchemas s
WHERE ISJSON(s.DefinitionJson)=1
  AND NOT EXISTS (SELECT 1 FROM dbo.F03ExcelSchemaVersions v WHERE v.SchemaId=s.Id);
GO

/* Point the canonical header at the migrated version. */
UPDATE s
SET CurrentVersionId=v.Id
FROM dbo.F03ExcelSchemas s
CROSS APPLY
(
    SELECT TOP(1) Id
    FROM dbo.F03ExcelSchemaVersions v
    WHERE v.SchemaId=s.Id
    ORDER BY v.VersionNo DESC, v.Id DESC
) v
WHERE s.CurrentVersionId IS NULL;
GO

/* Migrate serialized field definitions. */
INSERT INTO dbo.F03ExcelSchemaFields
(
    SchemaVersionId, FieldKey, DataType, SourceColumnIndex, HeaderName,
    ResourceKey, TargetProperty, Format, IsRequired, AllowEmpty, DisplayOrder
)
SELECT
    v.Id,
    j.FieldKey,
    COALESCE(j.DataType,N'Text'),
    COALESCE(j.SourceColumnIndex,0),
    j.HeaderName,
    j.ResourceKey,
    j.TargetProperty,
    j.Format,
    COALESCE(j.Required,0),
    COALESCE(j.AllowEmpty,1),
    COALESCE(j.DisplayOrder,0)
FROM dbo.F03ExcelSchemas s
JOIN dbo.F03ExcelSchemaVersions v ON v.SchemaId=s.Id
CROSS APPLY OPENJSON(s.DefinitionJson,'$.Fields')
WITH
(
    FieldKey NVARCHAR(200) '$.FieldKey',
    DataType NVARCHAR(50) '$.DataType',
    Required BIT '$.Required',
    SourceColumnIndex INT '$.SourceColumnIndex',
    HeaderName NVARCHAR(250) '$.HeaderName',
    Format NVARCHAR(100) '$.Format',
    ResourceKey NVARCHAR(250) '$.ResourceKey',
    TargetProperty NVARCHAR(250) '$.TargetProperty',
    DisplayOrder INT '$.DisplayOrder',
    AllowEmpty BIT '$.AllowEmpty'
) j
WHERE ISJSON(s.DefinitionJson)=1
  AND j.FieldKey IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.F03ExcelSchemaFields f
      WHERE f.SchemaVersionId=v.Id AND f.FieldKey=j.FieldKey
  );
GO

/* Canonical status is now authoritative. */
UPDATE s SET IsActive=CASE WHEN Status=1 THEN 1 ELSE 0 END
FROM dbo.F03ExcelSchemas s;
GO
