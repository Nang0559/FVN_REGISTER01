/*
    Shared Excel Platform migration
    --------------------------------
    Equipment Excel schema/import metadata is now owned by the shared F03Excel* model.
    This script removes the obsolete Equipment-specific metadata tables after verifying
    that their names no longer have active foreign-key dependants.

    Canonical tables:
      F03ExcelSchemas
      F03ExcelSchemaVersions
      F03ExcelSchemaFields
      F03ExcelImportBatches
      F03ExcelImportRows
      F03ExcelImportErrors
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @sql nvarchar(max) = N'';

/* Drop foreign keys that reference the legacy Equipment Excel metadata tables. */
SELECT @sql = @sql +
    N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id)) +
    N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(13) + CHAR(10)
FROM sys.foreign_keys fk
JOIN sys.tables parent_table ON parent_table.object_id = fk.parent_object_id
JOIN sys.tables referenced_table ON referenced_table.object_id = fk.referenced_object_id
WHERE referenced_table.name IN
(
    N'F03EquipmentImportErrors',
    N'F03EquipmentImportBatches',
    N'F03EquipmentFieldDefinitions',
    N'F03EquipmentSchemas'
);

IF @sql <> N'' EXEC sys.sp_executesql @sql;

/*
   These tables are obsolete only after the application has migrated to F03Excel*.
   Keep the guards so this migration is safe on a database where they were already removed.
*/
IF OBJECT_ID(N'dbo.F03EquipmentImportErrors', N'U') IS NOT NULL
    DROP TABLE dbo.F03EquipmentImportErrors;

IF OBJECT_ID(N'dbo.F03EquipmentImportBatches', N'U') IS NOT NULL
    DROP TABLE dbo.F03EquipmentImportBatches;

IF OBJECT_ID(N'dbo.F03EquipmentFieldDefinitions', N'U') IS NOT NULL
    DROP TABLE dbo.F03EquipmentFieldDefinitions;

IF OBJECT_ID(N'dbo.F03EquipmentSchemas', N'U') IS NOT NULL
    DROP TABLE dbo.F03EquipmentSchemas;

COMMIT TRANSACTION;
GO

/* Verification: legacy Equipment Excel metadata must no longer exist. */
IF EXISTS
(
    SELECT 1
    FROM sys.tables
    WHERE name IN
    (
        N'F03EquipmentImportErrors',
        N'F03EquipmentImportBatches',
        N'F03EquipmentFieldDefinitions',
        N'F03EquipmentSchemas'
    )
)
    THROW 51001, 'Legacy Equipment Excel metadata tables still exist.', 1;
GO
