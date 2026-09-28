/*
FVN_REGISTER - SQL 02C Table prerequisites

03_Tables.sql contains compatibility ALTER TABLE statements near its beginning.
SQL Server resolves those ALTER TABLE targets when compiling the batch, before the
CREATE TABLE statements later in 03_Tables.sql are reached. On a clean database
this causes Msg 1088 for F03Employees / F03StagingEmployee (and can affect the
related OT tables as well).

Create the canonical tables first. 03_Tables.sql then becomes idempotent and
its later upgrade blocks can safely reconcile existing installations.
*/
USE [FVN_REGISTER];
GO

IF OBJECT_ID(N'dbo.F03Employees', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03Employees
    (
        Id int IDENTITY PRIMARY KEY,
        IsActive bit NULL DEFAULT 1,
        CreatedBy int NOT NULL DEFAULT 0,
        LastModifiedSource nvarchar(50),
        CreatedAt datetime2(0) NOT NULL DEFAULT GETDATE(),
        ModifiedBy int NULL,
        ModifiedAt datetime2(0) NULL,
        EmployeeCode nvarchar(50) NOT NULL,
        EmployeeName nvarchar(100) NOT NULL,
        DeptCode nvarchar(20) NOT NULL,
        PositionCode nvarchar(20) NOT NULL,
        BirthDate datetime2(0) NULL,
        GenderCode int NULL,
        EmailAddress nvarchar(100) NOT NULL,
        PhoneNumber nvarchar(20) NULL,
        FirstWorkingDate datetime2(0) NULL,
        EndWorkingDate datetime2(0) NULL,
        TotalLeaveDays decimal(10,2) NOT NULL DEFAULT 0,
        EmployeeNo int NULL,
        LevelApprove int NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.F03StagingEmployee', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03StagingEmployee
    (
        Id int IDENTITY PRIMARY KEY,
        EntityKey nvarchar(255) NOT NULL,
        Action int NOT NULL,
        IsProcessed bit NOT NULL DEFAULT 0,
        ErrorMessage nvarchar(1000) NULL,
        CreatedAt datetime2(0) NOT NULL,
        CreatedBy nvarchar(100) NULL,
        EmployeeName nvarchar(255) NOT NULL,
        DeptCode nvarchar(20) NULL,
        PositionCode nvarchar(20) NULL,
        EmailAddress nvarchar(100) NOT NULL,
        PhoneNumber nvarchar(20) NULL,
        BirthDate datetime2(0) NULL,
        GenderCode int NULL,
        FirstWorkingDate datetime2(0) NULL,
        EndWorkingDate datetime2(0) NULL,
        TotalLeaveDays decimal(10,2) NULL,
        EmployeeNo int NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.F03OTTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03OTTypes
    (
        Id int IDENTITY PRIMARY KEY,
        IsActive bit NULL DEFAULT 1,
        CreatedBy int NOT NULL DEFAULT 0,
        LastModifiedSource nvarchar(50),
        CreatedAt datetime2(0) NOT NULL DEFAULT GETDATE(),
        ModifiedBy int NULL,
        ModifiedAt datetime2(0) NULL,
        OTTypeCode nvarchar(50) NOT NULL,
        OTTypeName nvarchar(200) NOT NULL,
        OTTypeName2 nvarchar(200) NULL,
        RateMultiplier decimal(5,2) NOT NULL DEFAULT 1.50,
        HRMCode nvarchar(50) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.F03OTEmployees', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03OTEmployees
    (
        Id int IDENTITY PRIMARY KEY,
        IsActive bit NULL DEFAULT 1,
        CreatedBy int NOT NULL DEFAULT 0,
        LastModifiedSource nvarchar(50),
        CreatedAt datetime2(0) NOT NULL DEFAULT GETDATE(),
        ModifiedBy int NULL,
        ModifiedAt datetime2(0) NULL,
        OTRequestId int NOT NULL,
        EmployeeCode nvarchar(50) NOT NULL,
        EmployeeName nvarchar(100) NULL,
        DeptCode nvarchar(20) NULL,
        DeptName nvarchar(100) NULL,
        CvCode nvarchar(10) NULL,
        OTTypeCode nvarchar(20) NULL,
        StartTime datetime2(0) NULL,
        EndTime datetime2(0) NULL,
        OTHours decimal(5,2) NOT NULL,
        ActualHours decimal(5,2) NULL,
        ActualStartTime datetime2(0) NULL,
        ActualEndTime datetime2(0) NULL,
        OTReasonCategoryCode nvarchar(10) NULL,
        OTReasonDetail nvarchar(500) NULL,
        OTRateMultiplier decimal(5,2) NOT NULL DEFAULT 1.50,
        ValidationStatus int NOT NULL DEFAULT 0,
        ValidationMessage nvarchar(255) NULL,
        Note nvarchar(500) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.F03StagingOTType', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03StagingOTType
    (
        Id int IDENTITY PRIMARY KEY,
        EntityKey nvarchar(255) NOT NULL,
        Action int NOT NULL,
        IsProcessed bit NOT NULL DEFAULT 0,
        ErrorMessage nvarchar(1000) NULL,
        CreatedAt datetime2(0) NOT NULL,
        CreatedBy nvarchar(100) NULL,
        OTTypeName nvarchar(255) NOT NULL,
        OTTypeName2 nvarchar(255) NULL,
        RateMultiplier decimal(5,2) NOT NULL,
        HRMCode nvarchar(50) NULL
    );
END;
GO

PRINT N'02C_TablePrerequisites: OK';
GO
