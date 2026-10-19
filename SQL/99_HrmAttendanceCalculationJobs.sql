/*
    Persistent job queue for long-running HRM attendance calculations.
    The existing dbo.usp_CalculateHrmAttendance is intentionally unchanged.
    One job is processed one WorkDate at a time by the background worker so
    cancel/resume happens only at a safe date boundary.
*/

IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculationJob', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03HrmAttendanceCalculationJob
    (
        JobId uniqueidentifier NOT NULL CONSTRAINT PK_F03HrmAttendanceCalculationJob PRIMARY KEY,
        DeptCode int NULL,
        EmployeeCode nvarchar(50) NULL,
        FromDate date NOT NULL,
        ToDate date NOT NULL,
        ReopenPayrollPeriod bit NOT NULL CONSTRAINT DF_F03HrmAttendanceCalculationJob_Reopen DEFAULT(0),
        Priority tinyint NOT NULL CONSTRAINT DF_F03HrmAttendanceCalculationJob_Priority DEFAULT(1),
        TriggeredBy nvarchar(100) NOT NULL,
        Status nvarchar(30) NOT NULL CONSTRAINT DF_F03HrmAttendanceCalculationJob_Status DEFAULT(N'Queued'),
        CurrentWorkDate date NULL,
        CompletedDateCount int NOT NULL CONSTRAINT DF_F03HrmAttendanceCalculationJob_Completed DEFAULT(0),
        TotalDateCount int NOT NULL,
        CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_F03HrmAttendanceCalculationJob_Created DEFAULT(SYSUTCDATETIME()),
        StartedAt datetime2(3) NULL,
        LastProgressAt datetime2(3) NULL,
        FinishedAt datetime2(3) NULL,
        CancelRequestedAt datetime2(3) NULL,
        CancelRequestedBy nvarchar(100) NULL,
        LastCalculationBatchId uniqueidentifier NULL,
        ErrorMessage nvarchar(2000) NULL,
        UpdatedAt datetime2(3) NOT NULL CONSTRAINT DF_F03HrmAttendanceCalculationJob_Updated DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT CK_F03HrmAttendanceCalculationJob_DateRange CHECK (FromDate <= ToDate),
        CONSTRAINT CK_F03HrmAttendanceCalculationJob_Status CHECK (Status IN
            (N'Queued',N'Running',N'CancelRequested',N'Cancelled',N'Completed',N'Failed',N'Stalled',N'Terminated'))
    );
END;
GO

IF COL_LENGTH(N'dbo.F03HrmAttendanceCalculationJob', N'Priority') IS NULL
BEGIN
    ALTER TABLE dbo.F03HrmAttendanceCalculationJob
        ADD Priority tinyint NOT NULL
            CONSTRAINT DF_F03HrmAttendanceCalculationJob_Priority DEFAULT(1) WITH VALUES;
END;
GO

IF COL_LENGTH(N'dbo.F03HrmAttendanceCalculationJob', N'SkipCoveredDates') IS NULL
BEGIN
    ALTER TABLE dbo.F03HrmAttendanceCalculationJob
        ADD SkipCoveredDates bit NOT NULL
            CONSTRAINT DF_F03HrmAttendanceCalculationJob_SkipCoveredDates DEFAULT(0) WITH VALUES;
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id=OBJECT_ID(N'dbo.F03HrmAttendanceCalculationJob')
      AND name=N'IX_F03HrmAttendanceCalculationJob_Queue'
)
BEGIN
    CREATE INDEX IX_F03HrmAttendanceCalculationJob_Queue
        ON dbo.F03HrmAttendanceCalculationJob(Status,CreatedAt)
        INCLUDE(JobId,DeptCode,EmployeeCode,FromDate,ToDate,CurrentWorkDate,CompletedDateCount,TotalDateCount,LastProgressAt,CancelRequestedAt);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id=OBJECT_ID(N'dbo.F03HrmAttendanceCalculationJob')
      AND name=N'IX_F03HrmAttendanceCalculationJob_PriorityQueue'
)
BEGIN
    CREATE INDEX IX_F03HrmAttendanceCalculationJob_PriorityQueue
        ON dbo.F03HrmAttendanceCalculationJob(Status,Priority,CreatedAt)
        INCLUDE(JobId,DeptCode,EmployeeCode,FromDate,ToDate,SkipCoveredDates,LastProgressAt);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id=OBJECT_ID(N'dbo.F03HrmAttendanceCalculationJob')
      AND name=N'IX_F03HrmAttendanceCalculationJob_Trigger'
)
BEGIN
    CREATE INDEX IX_F03HrmAttendanceCalculationJob_Trigger
        ON dbo.F03HrmAttendanceCalculationJob(TriggeredBy,Status,CreatedAt)
        INCLUDE(JobId,FromDate,ToDate,LastCalculationBatchId);
END;
GO
