/*
===============================================================================
28B - Trips schema
Canonical SQL deployment owner migrated from Database/Trips/001-002.
Runs after core tables and WorkCalendar and before ExecutionReconciliation.
===============================================================================
*/

IF OBJECT_ID(N'dbo.F03TripRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03TripRequests
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03TripRequests PRIMARY KEY,
        EmployeeCode NVARCHAR(50) NOT NULL,
        DeptCode NVARCHAR(20) NULL,
        RequestStatus INT NOT NULL CONSTRAINT DF_F03TripRequests_RequestStatus DEFAULT(0),
        IsActive BIT NULL CONSTRAINT DF_F03TripRequests_IsActive DEFAULT(1),
        CreatedBy INT NOT NULL,
        LastModifiedSource NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_F03TripRequests_CreatedAt DEFAULT(GETDATE()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2 NULL,
        TripCode NVARCHAR(30) NOT NULL,
        StartDate DATETIME2 NOT NULL,
        EndDate DATETIME2 NOT NULL,
        Destination NVARCHAR(250) NOT NULL,
        Purpose NVARCHAR(1000) NOT NULL,
        CustomerOrPartner NVARCHAR(250) NULL,
        TransportMethod NVARCHAR(100) NULL,
        CompanionEmployeeCodes NVARCHAR(500) NULL,
        EstimatedCost DECIMAL(18,2) NULL,
        Accommodation NVARCHAR(500) NULL,
        Note NVARCHAR(1000) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03TripRequests_TripCode' AND object_id=OBJECT_ID(N'dbo.F03TripRequests'))
    CREATE UNIQUE INDEX UX_F03TripRequests_TripCode ON dbo.F03TripRequests(TripCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03TripRequests_Employee_Period' AND object_id=OBJECT_ID(N'dbo.F03TripRequests'))
    CREATE INDEX IX_F03TripRequests_Employee_Period ON dbo.F03TripRequests(EmployeeCode,StartDate,EndDate);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_F03TripRequests_DateRange' AND parent_object_id=OBJECT_ID(N'dbo.F03TripRequests'))
    ALTER TABLE dbo.F03TripRequests ADD CONSTRAINT CK_F03TripRequests_DateRange CHECK(EndDate>=StartDate);
GO

IF OBJECT_ID(N'dbo.F03TripActual', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03TripActual
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03TripActual PRIMARY KEY,
        TripRequestId INT NOT NULL,
        EmployeeCode NVARCHAR(50) NOT NULL,
        ActualStartDate DATETIME2 NOT NULL,
        ActualEndDate DATETIME2 NOT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_F03TripActual_Status DEFAULT(N'Scheduled'),
        IsActive BIT NOT NULL CONSTRAINT DF_F03TripActual_IsActive DEFAULT(1),
        CreatedBy INT NOT NULL CONSTRAINT DF_F03TripActual_CreatedBy DEFAULT(0),
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_F03TripActual_CreatedAt DEFAULT(GETDATE()),
        ModifiedBy INT NULL,
        ModifiedAt DATETIME2(0) NULL,
        LastModifiedSource NVARCHAR(50) NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_F03TripActual_TripRequest' AND object_id=OBJECT_ID(N'dbo.F03TripActual'))
    CREATE UNIQUE INDEX UX_F03TripActual_TripRequest ON dbo.F03TripActual(TripRequestId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_F03TripActual_EmployeePeriod' AND object_id=OBJECT_ID(N'dbo.F03TripActual'))
    CREATE INDEX IX_F03TripActual_EmployeePeriod ON dbo.F03TripActual(EmployeeCode,ActualStartDate,ActualEndDate,Status);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_F03TripActual_TripRequest')
    ALTER TABLE dbo.F03TripActual WITH CHECK ADD CONSTRAINT FK_F03TripActual_TripRequest FOREIGN KEY(TripRequestId) REFERENCES dbo.F03TripRequests(Id);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_F03TripActual_DateRange')
    ALTER TABLE dbo.F03TripActual WITH CHECK ADD CONSTRAINT CK_F03TripActual_DateRange CHECK(ActualEndDate>=ActualStartDate);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_F03TripActual_Status')
    ALTER TABLE dbo.F03TripActual WITH CHECK ADD CONSTRAINT CK_F03TripActual_Status CHECK(Status IN(N'Scheduled',N'InProgress',N'Completed',N'Cancelled'));
GO
