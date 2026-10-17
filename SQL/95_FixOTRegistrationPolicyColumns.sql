/*
===============================================================================
70 - BỔ SUNG CỘT CÒN THIẾU CHO BẢNG CHÍNH SÁCH NGƯỜI ĐĂNG KÝ OT / PHẢN HỒI CẤP TRÊN
===============================================================================
Chạy SAU 93_OTTripRegistrationPolicy.sql và 94_SupervisorFeedbackCompletion.sql. Idempotent.

Lý do: các entity F03OTRegistrationRule / F03OTRegistrar / F03OTRegistrarPosition / F03SupervisorFeedback
kế thừa BaseAuditEntity nên EF Core luôn SELECT các cột IsActive, LastModifiedSource, CreatedBy (int, NOT NULL).
Script 68 chưa tạo đủ các cột này → lỗi "Invalid column name 'LastModifiedSource' / 'IsActive'" khi truy vấn,
và dòng seed có CreatedBy = NULL sẽ lỗi khi EF đọc vào kiểu int.
===============================================================================
*/
SET NOCOUNT ON;
GO

/* 1) LastModifiedSource cho cả 4 bảng */
IF COL_LENGTH(N'dbo.F03OTRegistrationRules', N'LastModifiedSource') IS NULL
    ALTER TABLE dbo.F03OTRegistrationRules ADD LastModifiedSource NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.F03OTRegistrars', N'LastModifiedSource') IS NULL
    ALTER TABLE dbo.F03OTRegistrars ADD LastModifiedSource NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.F03OTRegistrarPositions', N'LastModifiedSource') IS NULL
    ALTER TABLE dbo.F03OTRegistrarPositions ADD LastModifiedSource NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.F03SupervisorFeedbacks', N'LastModifiedSource') IS NULL
    ALTER TABLE dbo.F03SupervisorFeedbacks ADD LastModifiedSource NVARCHAR(50) NULL;
GO

/* 2) IsActive cho 3 bảng chưa có (F03OTRegistrationRules đã có từ script 68) */
IF COL_LENGTH(N'dbo.F03OTRegistrars', N'IsActive') IS NULL
    ALTER TABLE dbo.F03OTRegistrars ADD IsActive BIT NOT NULL CONSTRAINT DF_F03OTRegistrars_IsActive DEFAULT 1;
IF COL_LENGTH(N'dbo.F03OTRegistrarPositions', N'IsActive') IS NULL
    ALTER TABLE dbo.F03OTRegistrarPositions ADD IsActive BIT NOT NULL CONSTRAINT DF_F03OTRegistrarPositions_IsActive DEFAULT 1;
IF COL_LENGTH(N'dbo.F03SupervisorFeedbacks', N'IsActive') IS NULL
    ALTER TABLE dbo.F03SupervisorFeedbacks ADD IsActive BIT NOT NULL CONSTRAINT DF_F03SupervisorFeedbacks_IsActive DEFAULT 1;
GO

/* 3) CreatedBy: entity là int NOT NULL, dòng seed của script 68 đang để NULL */
UPDATE dbo.F03OTRegistrationRules  SET CreatedBy = 0 WHERE CreatedBy IS NULL;
UPDATE dbo.F03OTRegistrars         SET CreatedBy = 0 WHERE CreatedBy IS NULL;
UPDATE dbo.F03OTRegistrarPositions SET CreatedBy = 0 WHERE CreatedBy IS NULL;
UPDATE dbo.F03SupervisorFeedbacks  SET CreatedBy = 0 WHERE CreatedBy IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.F03OTRegistrationRules') AND name = N'CreatedBy' AND is_nullable = 1)
    ALTER TABLE dbo.F03OTRegistrationRules ALTER COLUMN CreatedBy INT NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.F03OTRegistrars') AND name = N'CreatedBy' AND is_nullable = 1)
    ALTER TABLE dbo.F03OTRegistrars ALTER COLUMN CreatedBy INT NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.F03OTRegistrarPositions') AND name = N'CreatedBy' AND is_nullable = 1)
    ALTER TABLE dbo.F03OTRegistrarPositions ALTER COLUMN CreatedBy INT NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.F03SupervisorFeedbacks') AND name = N'CreatedBy' AND is_nullable = 1)
    ALTER TABLE dbo.F03SupervisorFeedbacks ALTER COLUMN CreatedBy INT NOT NULL;
GO

/* 4) Mặc định CreatedBy = 0 cho các lần seed/insert thủ công sau này */
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.F03OTRegistrationRules') AND COL_NAME(parent_object_id, parent_column_id) = N'CreatedBy')
    ALTER TABLE dbo.F03OTRegistrationRules ADD CONSTRAINT DF_F03OTRegistrationRules_CreatedBy DEFAULT 0 FOR CreatedBy;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.F03OTRegistrars') AND COL_NAME(parent_object_id, parent_column_id) = N'CreatedBy')
    ALTER TABLE dbo.F03OTRegistrars ADD CONSTRAINT DF_F03OTRegistrars_CreatedBy DEFAULT 0 FOR CreatedBy;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.F03OTRegistrarPositions') AND COL_NAME(parent_object_id, parent_column_id) = N'CreatedBy')
    ALTER TABLE dbo.F03OTRegistrarPositions ADD CONSTRAINT DF_F03OTRegistrarPositions_CreatedBy DEFAULT 0 FOR CreatedBy;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.F03SupervisorFeedbacks') AND COL_NAME(parent_object_id, parent_column_id) = N'CreatedBy')
    ALTER TABLE dbo.F03SupervisorFeedbacks ADD CONSTRAINT DF_F03SupervisorFeedbacks_CreatedBy DEFAULT 0 FOR CreatedBy;
GO
