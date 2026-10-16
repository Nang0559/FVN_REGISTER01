USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

/* Signed / stamped documents attached to public information (PDF, images, Office files). Files live on disk under uploads/public-information. */
IF OBJECT_ID(N'dbo.F03PublicInformationAttachment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03PublicInformationAttachment
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03PublicInformationAttachment PRIMARY KEY,
        PublicInformationId int NOT NULL,
        FileName nvarchar(260) NOT NULL,
        FilePath nvarchar(500) NOT NULL,
        FileExtension nvarchar(20) NULL,
        FileSize bigint NOT NULL CONSTRAINT DF_F03PublicInformationAttachment_FileSize DEFAULT(0),
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_F03PublicInformationAttachment_CreatedAt DEFAULT(GETDATE()),
        CreatedBy int NOT NULL CONSTRAINT DF_F03PublicInformationAttachment_CreatedBy DEFAULT(0),
        ModifiedAt datetime2(0) NULL,
        ModifiedBy int NULL,
        CONSTRAINT FK_F03PublicInformationAttachment_Info FOREIGN KEY (PublicInformationId) REFERENCES dbo.F03PublicInformation(Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03PublicInformationAttachment_Info' AND object_id=OBJECT_ID(N'dbo.F03PublicInformationAttachment'))
    CREATE INDEX IX_F03PublicInformationAttachment_Info ON dbo.F03PublicInformationAttachment(PublicInformationId);
GO
