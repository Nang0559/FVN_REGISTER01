USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
GO

/*
  HRM attendance calculation is intentionally kept serial and date-transactional.
  These indexes do not change dbo.usp_CalculateHrmAttendance behaviour; they reduce
  the repeated current-state DELETE/lookup and post-calculation symbol-pass cost.
*/
IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculated',N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1 FROM sys.indexes
       WHERE name=N'IX_F03HrmAttendanceCalculated_Employee_WorkDate'
         AND object_id=OBJECT_ID(N'dbo.F03HrmAttendanceCalculated',N'U')
   )
BEGIN
    CREATE INDEX IX_F03HrmAttendanceCalculated_Employee_WorkDate
        ON dbo.F03HrmAttendanceCalculated(HrmEmployeeId,WorkDate);
END;
GO

IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculated',N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1 FROM sys.indexes
       WHERE name=N'IX_F03HrmAttendanceCalculated_Batch_WorkDate'
         AND object_id=OBJECT_ID(N'dbo.F03HrmAttendanceCalculated',N'U')
   )
BEGIN
    CREATE INDEX IX_F03HrmAttendanceCalculated_Batch_WorkDate
        ON dbo.F03HrmAttendanceCalculated(CalculationBatchId,WorkDate)
        INCLUDE(HrmEmployeeId,EmployeeCode,CheckInTime,CheckOutTime,DayType,ShiftId,ShiftAbbr);
END;
GO

IF OBJECT_ID(N'dbo.F03HrmOTActual',N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1 FROM sys.indexes
       WHERE name=N'IX_F03HrmOTActual_Employee_WorkDate'
         AND object_id=OBJECT_ID(N'dbo.F03HrmOTActual',N'U')
   )
BEGIN
    CREATE INDEX IX_F03HrmOTActual_Employee_WorkDate
        ON dbo.F03HrmOTActual(HrmEmployeeId,WorkDate);
END;
GO

IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculationRun',N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1 FROM sys.indexes
       WHERE name=N'IX_F03HrmAttendanceCalculationRun_Employee_Coverage'
         AND object_id=OBJECT_ID(N'dbo.F03HrmAttendanceCalculationRun',N'U')
   )
BEGIN
    CREATE INDEX IX_F03HrmAttendanceCalculationRun_Employee_Coverage
        ON dbo.F03HrmAttendanceCalculationRun
        (Status,CalculationVersion,EmployeeCode,FromDate,ToDate)
        INCLUDE(DeptCode,CalculationBatchId,StartedAt,FinishedAt,SqlSessionId,SqlSessionLoginTime,SqlConnectionId);
END;
GO

IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculationRun',N'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1 FROM sys.indexes
       WHERE name=N'IX_F03HrmAttendanceCalculationRun_Dept_Coverage'
         AND object_id=OBJECT_ID(N'dbo.F03HrmAttendanceCalculationRun',N'U')
   )
BEGIN
    CREATE INDEX IX_F03HrmAttendanceCalculationRun_Dept_Coverage
        ON dbo.F03HrmAttendanceCalculationRun
        (Status,CalculationVersion,DeptCode,FromDate,ToDate)
        INCLUDE(EmployeeCode,CalculationBatchId,StartedAt,FinishedAt,SqlSessionId,SqlSessionLoginTime,SqlConnectionId);
END;
GO
