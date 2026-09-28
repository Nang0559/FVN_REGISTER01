USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/*
===============================================================================
FVN HRM ATTENDANCE - IMMUTABLE BATCH SNAPSHOT

Purpose
-------
22_00 keeps F03HrmAttendanceCalculated / F03HrmOTActual as the CURRENT result
store used by the application.  CalculationBatchId identifies the calculation
that produced the current row.

This migration adds the missing immutable batch snapshot layer.  Every row
written by usp_CalculateHrmAttendance is copied to a batch-owned snapshot, so
rerunning a date/department can replace CURRENT state without destroying the
previous calculation result.

This intentionally does NOT change the HRM-compatible calculation algorithm,
current-state uniqueness, or application queries against F03HrmAttendanceCalculated.
===============================================================================
*/

IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot', N'U') IS NULL
BEGIN
    SELECT TOP (0) *
    INTO dbo.F03HrmAttendanceCalculatedBatchSnapshot
    FROM dbo.F03HrmAttendanceCalculated;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot')
      AND name = N'UX_F03HrmAttendanceCalculatedBatchSnapshot_BatchEmployeeDate'
)
BEGIN
    CREATE UNIQUE INDEX UX_F03HrmAttendanceCalculatedBatchSnapshot_BatchEmployeeDate
        ON dbo.F03HrmAttendanceCalculatedBatchSnapshot
        (CalculationBatchId, HrmEmployeeId, WorkDate);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot')
      AND name = N'IX_F03HrmAttendanceCalculatedBatchSnapshot_Batch'
)
BEGIN
    CREATE INDEX IX_F03HrmAttendanceCalculatedBatchSnapshot_Batch
        ON dbo.F03HrmAttendanceCalculatedBatchSnapshot
        (CalculationBatchId, WorkDate, HrmEmployeeId);
END;
GO

IF OBJECT_ID(N'dbo.tr_F03HrmAttendanceCalculated_BatchSnapshot', N'TR') IS NOT NULL
    DROP TRIGGER dbo.tr_F03HrmAttendanceCalculated_BatchSnapshot;
GO

CREATE TRIGGER dbo.tr_F03HrmAttendanceCalculated_BatchSnapshot
ON dbo.F03HrmAttendanceCalculated
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    /*
       INSERT creates the batch snapshot. UPDATE keeps the snapshot aligned
       with post-insert display-field updates performed by the calculation
       procedure. The snapshot remains isolated by CalculationBatchId.
    */
    MERGE dbo.F03HrmAttendanceCalculatedBatchSnapshot AS target
    USING inserted AS source
       ON target.CalculationBatchId = source.CalculationBatchId
      AND target.HrmEmployeeId = source.HrmEmployeeId
      AND target.WorkDate = source.WorkDate
    WHEN MATCHED THEN
        UPDATE SET
            CalculationVersion = source.CalculationVersion,
            EmployeeCode = source.EmployeeCode,
            FullName = source.FullName,
            HrmDeptId = source.HrmDeptId,
            DeptCode = source.DeptCode,
            HrmPositionId = source.HrmPositionId,
            ShiftId = source.ShiftId,
            ShiftAbbr = source.ShiftAbbr,
            CheckInGate = source.CheckInGate,
            CheckInTime = source.CheckInTime,
            CheckOutGate = source.CheckOutGate,
            CheckOutTime = source.CheckOutTime,
            ExitGate = source.ExitGate,
            ExitTime = source.ExitTime,
            EntryGate = source.EntryGate,
            EntryTime = source.EntryTime,
            WorkMinutesDay = source.WorkMinutesDay,
            WorkMinutesNight = source.WorkMinutesNight,
            OTMinutesDay = source.OTMinutesDay,
            OTMinutesNight = source.OTMinutesNight,
            OTMinutesDayTC = source.OTMinutesDayTC,
            OTMinutesNightTC = source.OTMinutesNightTC,
            OTRecognizedMinutesDay = source.OTRecognizedMinutesDay,
            OTRecognizedMinutesNight = source.OTRecognizedMinutesNight,
            LateMinutesDay = source.LateMinutesDay,
            LateMinutesNight = source.LateMinutesNight,
            EarlyLeaveMinutesDay = source.EarlyLeaveMinutesDay,
            EarlyLeaveMinutesNight = source.EarlyLeaveMinutesNight,
            RequiredMinutes = source.RequiredMinutes,
            LeaveTotal = source.LeaveTotal,
            LeaveAnnual = source.LeaveAnnual,
            Leave100 = source.Leave100,
            Leave70 = source.Leave70,
            LeaveUnpaid = source.LeaveUnpaid,
            LeaveBH100 = source.LeaveBH100,
            LeaveBH70 = source.LeaveBH70,
            LeaveBusinessTrip = source.LeaveBusinessTrip,
            LeaveCompensatory = source.LeaveCompensatory,
            LeaveOther = source.LeaveOther,
            LeaveTypeCode = source.LeaveTypeCode,
            LeaveReason = source.LeaveReason,
            Note = source.Note,
            HrmType = source.HrmType,
            HrmHoliday = source.HrmHoliday,
            HrmEmployeeHoliday = source.HrmEmployeeHoliday,
            IsLocked = source.IsLocked,
            HrmBCGhiChu = source.HrmBCGhiChu,
            HrmBCLyDoNghi = source.HrmBCLyDoNghi,
            HrmBCNghiTotal = source.HrmBCNghiTotal,
            HrmBCNghiPhep = source.HrmBCNghiPhep,
            HrmBCNghiH100 = source.HrmBCNghiH100,
            HrmBCNghiH70 = source.HrmBCNghiH70,
            HrmBCNghiKL = source.HrmBCNghiKL,
            HrmBCNghiBH100 = source.HrmBCNghiBH100,
            HrmBCNghiBH70 = source.HrmBCNghiBH70,
            HrmBCNghiCongTac = source.HrmBCNghiCongTac,
            HrmBCNghiBu = source.HrmBCNghiBu,
            HrmBCNghiKhac = source.HrmBCNghiKhac,
            HrmBCDaXacNhanLamThem = source.HrmBCDaXacNhanLamThem,
            HrmBCLoaiLamThem = source.HrmBCLoaiLamThem,
            HrmBCTinhLamThem = source.HrmBCTinhLamThem,
            HrmBCNgayLe = source.HrmBCNgayLe,
            HrmBCNgayLeNV = source.HrmBCNgayLeNV,
            HrmShiftDayType = source.HrmShiftDayType,
            AttendanceDisplayValue = source.AttendanceDisplayValue,
            OtDisplayValue = source.OtDisplayValue,
            CalculatedAt = source.CalculatedAt,
            CalculatedBy = source.CalculatedBy,
            SourceSystem = source.SourceSystem
    WHEN NOT MATCHED BY TARGET THEN
        INSERT
        SELECT source.*;
END;
GO

IF OBJECT_ID(N'dbo.F03HrmOTActualBatchSnapshot', N'U') IS NULL
BEGIN
    SELECT TOP (0) *
    INTO dbo.F03HrmOTActualBatchSnapshot
    FROM dbo.F03HrmOTActual;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.F03HrmOTActualBatchSnapshot')
      AND name = N'UX_F03HrmOTActualBatchSnapshot_BatchEmployeeDate'
)
BEGIN
    CREATE UNIQUE INDEX UX_F03HrmOTActualBatchSnapshot_BatchEmployeeDate
        ON dbo.F03HrmOTActualBatchSnapshot
        (CalculationBatchId, HrmEmployeeId, WorkDate);
END;
GO

IF OBJECT_ID(N'dbo.tr_F03HrmOTActual_BatchSnapshot', N'TR') IS NOT NULL
    DROP TRIGGER dbo.tr_F03HrmOTActual_BatchSnapshot;
GO

CREATE TRIGGER dbo.tr_F03HrmOTActual_BatchSnapshot
ON dbo.F03HrmOTActual
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.F03HrmOTActualBatchSnapshot AS target
    USING inserted AS source
       ON target.CalculationBatchId = source.CalculationBatchId
      AND target.HrmEmployeeId = source.HrmEmployeeId
      AND target.WorkDate = source.WorkDate
    WHEN MATCHED THEN
        UPDATE SET
            EmployeeCode = source.EmployeeCode,
            DeptCode = source.DeptCode,
            ActualStartTime = source.ActualStartTime,
            ActualEndTime = source.ActualEndTime,
            ActualMinutes = source.ActualMinutes,
            ActualOTDayMinutes = source.ActualOTDayMinutes,
            ActualOTNightMinutes = source.ActualOTNightMinutes,
            RecognizedOTMinutes = source.RecognizedOTMinutes,
            SourceAttendanceId = source.SourceAttendanceId,
            CalculatedAt = source.CalculatedAt
    WHEN NOT MATCHED BY TARGET THEN
        INSERT
        SELECT source.*;
END;
GO

PRINT N'Attendance batch snapshot layer installed.';
GO
