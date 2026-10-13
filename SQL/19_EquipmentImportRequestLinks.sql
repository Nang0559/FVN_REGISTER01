USE [FVN_REGISTER]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/*
    Idempotency ledger linking each staged equipment-import row to the single
    equipment request draft created from it. The official asset catalog is not
    changed by this table or by the import confirmation operation.
*/
IF OBJECT_ID(N'dbo.F03EquipmentImportRequestLinks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentImportRequestLinks
    (
        Id bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_F03EquipmentImportRequestLinks PRIMARY KEY,
        BatchId bigint NOT NULL,
        ImportRowId bigint NOT NULL,
        RequestId int NOT NULL,
        ActionKind nvarchar(30) NOT NULL,
        CreatedBy int NOT NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_F03EquipmentImportRequestLinks_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_F03EquipmentImportRequestLinks_BatchRow UNIQUE (BatchId, ImportRowId),
        CONSTRAINT UQ_F03EquipmentImportRequestLinks_Request UNIQUE (RequestId)
    );
END
GO
