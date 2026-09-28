USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/*
===============================================================================
FVN HRM ATTENDANCE - IMMUTABLE BATCH SNAPSHOT

F03HrmAttendanceCalculated / F03HrmOTActual remain the CURRENT result stores.
This layer preserves every calculation batch independently, so replacing
current rows during recalculation no longer destroys the previous batch.
===============================================================================
*/

IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot', N'U') IS NULL
BEGIN
    SELECT TOP (0) *
    INTO dbo.F03HrmAttendanceCalculatedBatchSnapshot
    FROM dbo.F03HrmAttendanceCalculated;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot') AND name=N'UX_F03HrmAttendanceCalculatedBatchSnapshot_BatchEmployeeDate')
    CREATE UNIQUE INDEX UX_F03HrmAttendanceCalculatedBatchSnapshot_BatchEmployeeDate
    ON dbo.F03HrmAttendanceCalculatedBatchSnapshot(CalculationBatchId,HrmEmployeeId,WorkDate);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot') AND name=N'IX_F03HrmAttendanceCalculatedBatchSnapshot_Batch')
    CREATE INDEX IX_F03HrmAttendanceCalculatedBatchSnapshot_Batch
    ON dbo.F03HrmAttendanceCalculatedBatchSnapshot(CalculationBatchId,WorkDate,HrmEmployeeId);
GO

IF OBJECT_ID(N'dbo.tr_F03HrmAttendanceCalculated_BatchSnapshot',N'TR') IS NOT NULL
    DROP TRIGGER dbo.tr_F03HrmAttendanceCalculated_BatchSnapshot;
GO

CREATE TRIGGER dbo.tr_F03HrmAttendanceCalculated_BatchSnapshot
ON dbo.F03HrmAttendanceCalculated
AFTER INSERT,UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE target
       SET CalculationVersion=source.CalculationVersion,
           EmployeeCode=source.EmployeeCode, FullName=source.FullName,
           HrmDeptId=source.HrmDeptId, DeptCode=source.DeptCode,
           HrmPositionId=source.HrmPositionId, ShiftId=source.ShiftId, ShiftAbbr=source.ShiftAbbr,
           CheckInGate=source.CheckInGate, CheckInTime=source.CheckInTime,
           CheckOutGate=source.CheckOutGate, CheckOutTime=source.CheckOutTime,
           ExitGate=source.ExitGate, ExitTime=source.ExitTime,
           EntryGate=source.EntryGate, EntryTime=source.EntryTime,
           WorkMinutesDay=source.WorkMinutesDay, WorkMinutesNight=source.WorkMinutesNight,
           OTMinutesDay=source.OTMinutesDay, OTMinutesNight=source.OTMinutesNight,
           OTMinutesDayTC=source.OTMinutesDayTC, OTMinutesNightTC=source.OTMinutesNightTC,
           OTRecognizedMinutesDay=source.OTRecognizedMinutesDay, OTRecognizedMinutesNight=source.OTRecognizedMinutesNight,
           LateMinutesDay=source.LateMinutesDay, LateMinutesNight=source.LateMinutesNight,
           EarlyLeaveMinutesDay=source.EarlyLeaveMinutesDay, EarlyLeaveMinutesNight=source.EarlyLeaveMinutesNight,
           RequiredMinutes=source.RequiredMinutes,
           LeaveTotal=source.LeaveTotal, LeaveAnnual=source.LeaveAnnual, Leave100=source.Leave100,
           Leave70=source.Leave70, LeaveUnpaid=source.LeaveUnpaid, LeaveBH100=source.LeaveBH100,
           LeaveBH70=source.LeaveBH70, LeaveBusinessTrip=source.LeaveBusinessTrip,
           LeaveCompensatory=source.LeaveCompensatory, LeaveOther=source.LeaveOther,
           LeaveTypeCode=source.LeaveTypeCode, LeaveReason=source.LeaveReason, Note=source.Note,
           HrmType=source.HrmType, HrmHoliday=source.HrmHoliday, HrmEmployeeHoliday=source.HrmEmployeeHoliday,
           IsLocked=source.IsLocked, HrmBCGhiChu=source.HrmBCGhiChu, HrmBCLyDoNghi=source.HrmBCLyDoNghi,
           HrmBCNghiTotal=source.HrmBCNghiTotal, HrmBCNghiPhep=source.HrmBCNghiPhep,
           HrmBCNghiH100=source.HrmBCNghiH100, HrmBCNghiH70=source.HrmBCNghiH70,
           HrmBCNghiKL=source.HrmBCNghiKL, HrmBCNghiBH100=source.HrmBCNghiBH100,
           HrmBCNghiBH70=source.HrmBCNghiBH70, HrmBCNghiCongTac=source.HrmBCNghiCongTac,
           HrmBCNghiBu=source.HrmBCNghiBu, HrmBCNghiKhac=source.HrmBCNghiKhac,
           HrmBCDaXacNhanLamThem=source.HrmBCDaXacNhanLamThem,
           HrmBCLoaiLamThem=source.HrmBCLoaiLamThem, HrmBCTinhLamThem=source.HrmBCTinhLamThem,
           HrmBCNgayLe=source.HrmBCNgayLe, HrmBCNgayLeNV=source.HrmBCNgayLeNV,
           HrmShiftDayType=source.HrmShiftDayType,
           AttendanceDisplayValue=source.AttendanceDisplayValue, OtDisplayValue=source.OtDisplayValue,
           CalculatedAt=source.CalculatedAt, CalculatedBy=source.CalculatedBy, SourceSystem=source.SourceSystem
    FROM dbo.F03HrmAttendanceCalculatedBatchSnapshot target
    INNER JOIN inserted source
      ON target.CalculationBatchId=source.CalculationBatchId
     AND target.HrmEmployeeId=source.HrmEmployeeId
     AND target.WorkDate=source.WorkDate;

    INSERT dbo.F03HrmAttendanceCalculatedBatchSnapshot
    (
        CalculationBatchId,CalculationVersion,WorkDate,HrmEmployeeId,EmployeeCode,FullName,HrmDeptId,DeptCode,HrmPositionId,ShiftId,ShiftAbbr,
        CheckInGate,CheckInTime,CheckOutGate,CheckOutTime,ExitGate,ExitTime,EntryGate,EntryTime,WorkMinutesDay,WorkMinutesNight,
        OTMinutesDay,OTMinutesNight,OTMinutesDayTC,OTMinutesNightTC,OTRecognizedMinutesDay,OTRecognizedMinutesNight,LatemInutesDay,LatemInutesNight,
        EarlyLeaveMinutesDay,EarlyLeaveMinutesNight,RequiredMinutes,LeaveTotal,LeaveAnnual,Leave100,Leave70,LeaveUnpaid,LeaveBH100,LeaveBH70,LeaveBusinessTrip,
        LeaveCompensatory,LeaveOther,LeaveTypeCode,LeaveReason,Note,HrmType,HrmHoliday,HrmEmployeeHoliday,IsLocked,HrmBCGhiChu,HrmBCLyDoNghi,
        HrmBCNghiTotal,HrmBCNghiPhep,HrmBCNghiH100,HrmBCNghiH70,HrmBCNghiKL,HrmBCNghiBH100,HrmBCNghiBH70,HrmBCNghiCongTac,HrmBCNghiBu,HrmBCNghiKhac,
        HrmBCDaXacNhanLamThem,HrmBCLoaiLamThem,HrmBCTinhLamThem,HrmBCNgayLe,HrmBCNgayLeNV,HrmShiftDayType,AttendanceDisplayValue,OtDisplayValue,CalculatedAt,CalculatedBy,SourceSystem
    )
    SELECT source.CalculationBatchId,source.CalculationVersion,source.WorkDate,source.HrmEmployeeId,source.EmployeeCode,source.FullName,source.HrmDeptId,source.DeptCode,source.HrmPositionId,source.ShiftId,source.ShiftAbbr,
           source.CheckInGate,source.CheckInTime,source.CheckOutGate,source.CheckOutTime,source.ExitGate,source.ExitTime,source.EntryGate,source.EntryTime,source.WorkMinutesDay,source.WorkMinutesNight,
           source.OTMinutesDay,source.OTMinutesNight,source.OTMinutesDayTC,source.OTMinutesNightTC,source.OTRecognizedMinutesDay,source.OTRecognizedMinutesNight,source.LateMinutesDay,source.LateMinutesNight,
           source.EarlyLeaveMinutesDay,source.EarlyLeaveMinutesNight,source.RequiredMinutes,source.LeaveTotal,source.LeaveAnnual,source.Leave100,source.Leave70,source.LeaveUnpaid,source.LeaveBH100,source.LeaveBH70,source.LeaveBusinessTrip,
           source.LeaveCompensatory,source.LeaveOther,source.LeaveTypeCode,source.LeaveReason,source.Note,source.HrmType,source.HrmHoliday,source.HrmEmployeeHoliday,source.IsLocked,source.HrmBCGhiChu,source.HrmBCLyDoNghi,
           source.HrmBCNghiTotal,source.HrmBCNghiPhep,source.HrmBCNghiH100,source.HrmBCNghiH70,source.HrmBCNghiKL,source.HrmBCNghiBH100,source.HrmBCNghiBH70,source.HrmBCNghiCongTac,source.HrmBCNghiBu,source.HrmBCNghiKhac,
           source.HrmBCDaXacNhanLamThem,source.HrmBCLoaiLamThem,source.HrmBCTinhLamThem,source.HrmBCNgayLe,source.HrmBCNgayLeNV,source.HrmShiftDayType,source.AttendanceDisplayValue,source.OtDisplayValue,source.CalculatedAt,source.CalculatedBy,source.SourceSystem
    FROM inserted source
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.F03HrmAttendanceCalculatedBatchSnapshot target
        WHERE target.CalculationBatchId=source.CalculationBatchId
          AND target.HrmEmployeeId=source.HrmEmployeeId
          AND target.WorkDate=source.WorkDate
    );
END;
GO

/* Correct the two column names above if an older deployment created the file with a typo. */
IF COL_LENGTH(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot',N'LatemInutesDay') IS NOT NULL
    AND COL_LENGTH(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot',N'LateMinutesDay') IS NULL
    EXEC sp_rename N'dbo.F03HrmAttendanceCalculatedBatchSnapshot.LatemInutesDay',N'LateMinutesDay','COLUMN';
IF COL_LENGTH(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot',N'LatemInutesNight') IS NOT NULL
    AND COL_LENGTH(N'dbo.F03HrmAttendanceCalculatedBatchSnapshot',N'LateMinutesNight') IS NULL
    EXEC sp_rename N'dbo.F03HrmAttendanceCalculatedBatchSnapshot.LatemInutesNight',N'LateMinutesNight','COLUMN';
GO

IF OBJECT_ID(N'dbo.F03HrmOTActualBatchSnapshot',N'U') IS NULL
BEGIN
    SELECT TOP (0) * INTO dbo.F03HrmOTActualBatchSnapshot FROM dbo.F03HrmOTActual;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.F03HrmOTActualBatchSnapshot') AND name=N'UX_F03HrmOTActualBatchSnapshot_BatchEmployeeDate')
    CREATE UNIQUE INDEX UX_F03HrmOTActualBatchSnapshot_BatchEmployeeDate
    ON dbo.F03HrmOTActualBatchSnapshot(CalculationBatchId,HrmEmployeeId,WorkDate);
GO

IF OBJECT_ID(N'dbo.tr_F03HrmOTActual_BatchSnapshot',N'TR') IS NOT NULL
    DROP TRIGGER dbo.tr_F03HrmOTActual_BatchSnapshot;
GO

CREATE TRIGGER dbo.tr_F03HrmOTActual_BatchSnapshot
ON dbo.F03HrmOTActual
AFTER INSERT,UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE target
       SET EmployeeCode=source.EmployeeCode,DeptCode=source.DeptCode,
           ActualStartTime=source.ActualStartTime,ActualEndTime=source.ActualEndTime,
           ActualMinutes=source.ActualMinutes,ActualOTDayMinutes=source.ActualOTDayMinutes,
           ActualOTNightMinutes=source.ActualOTNightMinutes,RecognizedOTMinutes=source.RecognizedOTMinutes,
           SourceAttendanceId=source.SourceAttendanceId,CalculatedAt=source.CalculatedAt
    FROM dbo.F03HrmOTActualBatchSnapshot target
    INNER JOIN inserted source
      ON target.CalculationBatchId=source.CalculationBatchId
     AND target.HrmEmployeeId=source.HrmEmployeeId
     AND target.WorkDate=source.WorkDate;

    INSERT dbo.F03HrmOTActualBatchSnapshot
    (CalculationBatchId,WorkDate,HrmEmployeeId,EmployeeCode,DeptCode,ActualStartTime,ActualEndTime,ActualMinutes,ActualOTDayMinutes,ActualOTNightMinutes,RecognizedOTMinutes,SourceAttendanceId,CalculatedAt)
    SELECT source.CalculationBatchId,source.WorkDate,source.HrmEmployeeId,source.EmployeeCode,source.DeptCode,source.ActualStartTime,source.ActualEndTime,source.ActualMinutes,source.ActualOTDayMinutes,source.ActualOTNightMinutes,source.RecognizedOTMinutes,source.SourceAttendanceId,source.CalculatedAt
    FROM inserted source
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.F03HrmOTActualBatchSnapshot target
        WHERE target.CalculationBatchId=source.CalculationBatchId
          AND target.HrmEmployeeId=source.HrmEmployeeId
          AND target.WorkDate=source.WorkDate
    );
END;
GO

PRINT N'Attendance batch snapshot layer installed.';
GO
