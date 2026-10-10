/*
  Equipment change approval foundation.
  Apply after the current equipment schema. Existing requests are preserved.
  Each AssetChange request owns its own before/after snapshot; the inventory-wide
  submission snapshot is recorded once per submission in F03EquipmentChangeBatches.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.F03EquipmentRequests', N'AssetBeforeSnapshotJson') IS NULL
    ALTER TABLE dbo.F03EquipmentRequests ADD AssetBeforeSnapshotJson nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.F03EquipmentRequests', N'AssetAfterSnapshotJson') IS NULL
    ALTER TABLE dbo.F03EquipmentRequests ADD AssetAfterSnapshotJson nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.F03EquipmentRequests', N'AssetBeforeHash') IS NULL
    ALTER TABLE dbo.F03EquipmentRequests ADD AssetBeforeHash nvarchar(64) NULL;
IF COL_LENGTH(N'dbo.F03EquipmentRequests', N'AssetSnapshotCapturedAtUtc') IS NULL
    ALTER TABLE dbo.F03EquipmentRequests ADD AssetSnapshotCapturedAtUtc datetime2(7) NULL;
IF COL_LENGTH(N'dbo.F03EquipmentRequests', N'AssetChangeAppliedAtUtc') IS NULL
    ALTER TABLE dbo.F03EquipmentRequests ADD AssetChangeAppliedAtUtc datetime2(7) NULL;

IF OBJECT_ID(N'dbo.F03EquipmentChangeBatches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentChangeBatches
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentChangeBatches PRIMARY KEY,
        BatchToken uniqueidentifier NOT NULL CONSTRAINT DF_F03EquipmentChangeBatches_BatchToken DEFAULT NEWSEQUENTIALID(),
        SubmittedByUserId int NOT NULL,
        DepartmentCode nvarchar(50) NULL,
        SubmittedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_F03EquipmentChangeBatches_SubmittedAtUtc DEFAULT SYSUTCDATETIME(),
        InventorySnapshotJson nvarchar(max) NOT NULL,
        InventorySnapshotSha256 nvarchar(64) NOT NULL,
        ItemCount int NOT NULL,
        ChangedItemCount int NOT NULL,
        Status int NOT NULL CONSTRAINT DF_F03EquipmentChangeBatches_Status DEFAULT 0,
        Note nvarchar(1000) NULL,
        CONSTRAINT UQ_F03EquipmentChangeBatches_BatchToken UNIQUE (BatchToken),
        CONSTRAINT CK_F03EquipmentChangeBatches_ItemCounts CHECK
          (ItemCount >= 0 AND ChangedItemCount >= 0 AND ChangedItemCount <= ItemCount),
        CONSTRAINT CK_F03EquipmentChangeBatches_SnapshotJson CHECK (ISJSON(InventorySnapshotJson) = 1)
    );
END;

IF OBJECT_ID(N'dbo.F03EquipmentChangeBatchItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentChangeBatchItems
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentChangeBatchItems PRIMARY KEY,
        BatchId bigint NOT NULL,
        AssetId int NOT NULL,
        EquipmentRequestId int NOT NULL,
        BeforeHash nvarchar(64) NOT NULL,
        AfterHash nvarchar(64) NOT NULL,
        ItemStatus int NOT NULL CONSTRAINT DF_F03EquipmentChangeBatchItems_Status DEFAULT 0,
        ConflictDetectedAtUtc datetime2(7) NULL,
        ConflictDetail nvarchar(1000) NULL,
        CONSTRAINT FK_F03EquipmentChangeBatchItems_Batch FOREIGN KEY (BatchId)
          REFERENCES dbo.F03EquipmentChangeBatches(Id),
        CONSTRAINT FK_F03EquipmentChangeBatchItems_Request FOREIGN KEY (EquipmentRequestId)
          REFERENCES dbo.F03EquipmentRequests(Id),
        CONSTRAINT UQ_F03EquipmentChangeBatchItems_Request UNIQUE (EquipmentRequestId),
        CONSTRAINT UQ_F03EquipmentChangeBatchItems_BatchAsset UNIQUE (BatchId, AssetId)
    );
    CREATE INDEX IX_F03EquipmentChangeBatchItems_BatchStatus
      ON dbo.F03EquipmentChangeBatchItems(BatchId, ItemStatus);
END;

COMMIT TRANSACTION;
