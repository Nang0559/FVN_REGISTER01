/*
  Repair legacy equipment rows where CustomDataJson is NULL.
  The application model treats CustomDataJson as a required JSON document.
  Safe to execute repeatedly.
*/
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.F03EquipmentAssets', N'U') IS NULL
        THROW 51002, 'Required table dbo.F03EquipmentAssets does not exist.', 1;

    IF COL_LENGTH(N'dbo.F03EquipmentAssets', N'CustomDataJson') IS NULL
        ALTER TABLE dbo.F03EquipmentAssets
            ADD CustomDataJson nvarchar(max) NOT NULL
                CONSTRAINT DF_F03EquipmentAssets_CustomDataJson DEFAULT (N'{}') WITH VALUES;
    ELSE
    BEGIN
        UPDATE dbo.F03EquipmentAssets
        SET CustomDataJson = N'{}'
        WHERE CustomDataJson IS NULL;

        IF NOT EXISTS
        (
            SELECT 1
            FROM sys.default_constraints dc
            JOIN sys.columns c
              ON c.object_id = dc.parent_object_id
             AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.F03EquipmentAssets')
              AND c.name = N'CustomDataJson'
        )
            ALTER TABLE dbo.F03EquipmentAssets
                ADD CONSTRAINT DF_F03EquipmentAssets_CustomDataJson
                DEFAULT (N'{}') FOR CustomDataJson;

        IF EXISTS
        (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.F03EquipmentAssets')
              AND name = N'CustomDataJson'
              AND is_nullable = 1
        )
            ALTER TABLE dbo.F03EquipmentAssets
                ALTER COLUMN CustomDataJson nvarchar(max) NOT NULL;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
