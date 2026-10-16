/*
  Hotfix for deployments running Equipment code with ManagedByEmployeeCode
  mapped in EF Core while the SQL Server schema has not been upgraded.
  Safe to execute more than once.
*/
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.F03EquipmentAssets', N'U') IS NULL
        THROW 51001, 'Required table dbo.F03EquipmentAssets does not exist. Apply the equipment base schema first.', 1;

    IF COL_LENGTH(N'dbo.F03EquipmentAssets', N'ManagedByEmployeeCode') IS NULL
        ALTER TABLE dbo.F03EquipmentAssets ADD ManagedByEmployeeCode nvarchar(50) NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.F03EquipmentAssets')
          AND name = N'IX_F03EquipmentAssets_ManagedByEmployeeCode'
    )
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_F03EquipmentAssets_ManagedByEmployeeCode
            ON dbo.F03EquipmentAssets(ManagedByEmployeeCode)
            WHERE ManagedByEmployeeCode IS NOT NULL;';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
