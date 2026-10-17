USE [FVN_REGISTER];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.F03OTSelfRegistrationPositions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03OTSelfRegistrationPositions
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03OTSelfRegistrationPositions PRIMARY KEY,
        DepartmentCode int NOT NULL CONSTRAINT DF_F03OTSelfRegistrationPositions_Department DEFAULT(0),
        RegisterType nvarchar(10) NOT NULL CONSTRAINT DF_F03OTSelfRegistrationPositions_Type DEFAULT(N'OT'),
        PositionCode nvarchar(50) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_F03OTSelfRegistrationPositions_Active DEFAULT(1),
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_F03OTSelfRegistrationPositions_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CreatedBy int NOT NULL CONSTRAINT DF_F03OTSelfRegistrationPositions_CreatedBy DEFAULT(0)
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.F03OTSelfRegistrationPositions') AND name=N'UX_F03OTSelfRegistrationPositions_Scope')
    CREATE UNIQUE INDEX UX_F03OTSelfRegistrationPositions_Scope
        ON dbo.F03OTSelfRegistrationPositions(DepartmentCode,RegisterType,PositionCode);
COMMIT TRANSACTION;
GO
