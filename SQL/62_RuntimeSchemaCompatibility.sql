USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
  Runtime schema compatibility gate.
  Purpose: make an existing database converge to the columns currently consumed
  by EF Core even when the database was deployed from an older SQL baseline.
  All changes are idempotent.
*/

/* ---------------------------------------------------------------------------
   PAYROLL PERIODS
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.F03PayrollCalculationPeriods', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03PayrollCalculationPeriods
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03PayrollCalculationPeriods_Compat PRIMARY KEY,
        IsActive bit NOT NULL CONSTRAINT DF_F03PayrollPeriods_Compat_IsActive DEFAULT 1,
        CreatedBy int NOT NULL CONSTRAINT DF_F03PayrollPeriods_Compat_CreatedBy DEFAULT 0,
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_F03PayrollPeriods_Compat_CreatedAt DEFAULT GETDATE(),
        ModifiedBy int NULL,
        ModifiedAt datetime2(0) NULL,
        LastModifiedSource nvarchar(50) NULL,
        PeriodCode nvarchar(20) NOT NULL,
        FromDate date NOT NULL,
        ToDate date NOT NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_F03PayrollPeriods_Compat_Status DEFAULT N'Open',
        CalculatedAt datetime2(0) NULL,
        CalculatedBy int NULL,
        LockedAt datetime2(0) NULL,
        LockedBy int NULL,
        ExportedAt datetime2(0) NULL,
        ExportedBy int NULL
    );
END;
GO

IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'IsActive') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD IsActive bit NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'CreatedBy') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD CreatedBy int NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'CreatedAt') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD CreatedAt datetime2(0) NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'ModifiedBy') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD ModifiedBy int NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'ModifiedAt') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD ModifiedAt datetime2(0) NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'LastModifiedSource') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD LastModifiedSource nvarchar(50) NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'PeriodCode') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD PeriodCode nvarchar(20) NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'FromDate') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD FromDate date NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'ToDate') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD ToDate date NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'Status') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD Status nvarchar(20) NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'CalculatedAt') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD CalculatedAt datetime2(0) NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'CalculatedBy') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD CalculatedBy int NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'LockedAt') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD LockedAt datetime2(0) NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'LockedBy') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD LockedBy int NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'ExportedAt') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD ExportedAt datetime2(0) NULL;
IF COL_LENGTH(N'dbo.F03PayrollCalculationPeriods',N'ExportedBy') IS NULL ALTER TABLE dbo.F03PayrollCalculationPeriods ADD ExportedBy int NULL;
GO

/* ---------------------------------------------------------------------------
   EMAIL QUEUE
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.F03EmailQueues', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EmailQueues
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EmailQueues_Compat PRIMARY KEY,
        IsActive bit NULL DEFAULT 1,
        CreatedBy int NOT NULL DEFAULT 0,
        LastModifiedSource nvarchar(50) NULL,
        CreatedAt datetime2(0) NOT NULL DEFAULT GETDATE(),
        ModifiedBy int NULL,
        ModifiedAt datetime2(0) NULL,
        ToEmail nvarchar(255) NOT NULL,
        Subject nvarchar(255) NOT NULL,
        Body nvarchar(max) NOT NULL,
        TemplateCode nvarchar(50) NULL,
        EmailProfileCode nvarchar(20) NULL,
        DispatchMode nvarchar(30) NOT NULL DEFAULT N'AutoSend',
        Payload nvarchar(max) NULL,
        Status nvarchar(30) NOT NULL DEFAULT N'Pending',
        RetryCount int NOT NULL DEFAULT 0,
        MaxRetry int NOT NULL DEFAULT 3,
        ErrorMessage nvarchar(1000) NULL,
        LastAttemptAt datetime2(0) NULL,
        ApprovedAt datetime2(0) NULL,
        ApprovedBy int NULL,
        CancelledAt datetime2(0) NULL,
        CancelledBy int NULL,
        SentAt datetime2(0) NULL
    );
END;
GO

IF COL_LENGTH(N'dbo.F03EmailQueues',N'EmailProfileCode') IS NULL ALTER TABLE dbo.F03EmailQueues ADD EmailProfileCode nvarchar(20) NULL;
IF COL_LENGTH(N'dbo.F03EmailQueues',N'DispatchMode') IS NULL ALTER TABLE dbo.F03EmailQueues ADD DispatchMode nvarchar(30) NOT NULL CONSTRAINT DF_F03EmailQueues_Compat_DispatchMode DEFAULT N'AutoSend';
IF COL_LENGTH(N'dbo.F03EmailQueues',N'LastAttemptAt') IS NULL ALTER TABLE dbo.F03EmailQueues ADD LastAttemptAt datetime2(0) NULL;
IF COL_LENGTH(N'dbo.F03EmailQueues',N'ApprovedAt') IS NULL ALTER TABLE dbo.F03EmailQueues ADD ApprovedAt datetime2(0) NULL;
IF COL_LENGTH(N'dbo.F03EmailQueues',N'ApprovedBy') IS NULL ALTER TABLE dbo.F03EmailQueues ADD ApprovedBy int NULL;
IF COL_LENGTH(N'dbo.F03EmailQueues',N'CancelledAt') IS NULL ALTER TABLE dbo.F03EmailQueues ADD CancelledAt datetime2(0) NULL;
IF COL_LENGTH(N'dbo.F03EmailQueues',N'CancelledBy') IS NULL ALTER TABLE dbo.F03EmailQueues ADD CancelledBy int NULL;
GO

PRINT N'62_RuntimeSchemaCompatibility: payroll/email runtime schema is aligned.';
GO
