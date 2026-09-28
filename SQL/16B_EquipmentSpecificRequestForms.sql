/*
===============================================================================
16B - Equipment-specific request forms
Canonical SQL deployment owner migrated from Database/Equipment/006.
Must run after 16_EquipmentFlexibleImport.sql and before later Equipment extensions.
===============================================================================
*/

IF COL_LENGTH(N'dbo.F03EquipmentAssets', N'EquipmentSchemaKey') IS NULL
BEGIN
    ALTER TABLE dbo.F03EquipmentAssets ADD EquipmentSchemaKey nvarchar(64) NULL;
END;
GO

IF COL_LENGTH(N'dbo.F03EquipmentRequests', N'FormId') IS NULL
BEGIN
    ALTER TABLE dbo.F03EquipmentRequests ADD FormId int NULL;
END;
GO

IF COL_LENGTH(N'dbo.F03EquipmentRequests', N'FormSubmissionId') IS NULL
BEGIN
    ALTER TABLE dbo.F03EquipmentRequests ADD FormSubmissionId int NULL;
END;
GO

IF COL_LENGTH(N'dbo.F03EquipmentRequests', N'FormCode') IS NULL
BEGIN
    ALTER TABLE dbo.F03EquipmentRequests ADD FormCode nvarchar(50) NULL;
END;
GO

IF COL_LENGTH(N'dbo.F03PublicFormSubmissions', N'EquipmentAssetId') IS NULL
BEGIN
    ALTER TABLE dbo.F03PublicFormSubmissions ADD EquipmentAssetId int NULL;
END;
GO

IF OBJECT_ID(N'dbo.F03EquipmentFormAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentFormAssignments
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentFormAssignments PRIMARY KEY,
        EquipmentSchemaKey nvarchar(64) NOT NULL,
        FormId int NOT NULL,
        DisplayOrder int NOT NULL CONSTRAINT DF_F03EquipmentFormAssignments_DisplayOrder DEFAULT(0),
        IsActive bit NOT NULL CONSTRAINT DF_F03EquipmentFormAssignments_IsActive DEFAULT(1),
        CreatedBy int NOT NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_F03EquipmentFormAssignments_CreatedAt DEFAULT(GETDATE()),
        ModifiedBy int NULL,
        ModifiedAt datetime2 NULL,
        CONSTRAINT FK_F03EquipmentFormAssignments_PublicForm FOREIGN KEY(FormId)
            REFERENCES dbo.F03PublicForms(Id)
    );

    CREATE UNIQUE INDEX UX_F03EquipmentFormAssignments_Type_Form
        ON dbo.F03EquipmentFormAssignments(EquipmentSchemaKey, FormId);

    CREATE INDEX IX_F03EquipmentFormAssignments_Type_Active
        ON dbo.F03EquipmentFormAssignments(EquipmentSchemaKey, IsActive);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode = 2320)
BEGIN
    INSERT INTO dbo.F03Functions(FunctionCode, FunctionName, Detail, CreatedBy, CreatedAt, IsActive)
    VALUES(2320, N'Equipment.FormManage', N'Quản lý các biểu mẫu yêu cầu áp dụng theo loại thiết bị.', 1, GETDATE(), 1);
END;
GO

/* 2320 is intentionally not granted automatically. */
