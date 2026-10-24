USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/*
===============================================================================
HRM ATTENDANCE SOURCE FRESHNESS

Purpose
  A successful dbo.F03HrmAttendanceCalculationRun only proves a date WAS calculated.
  It cannot tell whether HRM changed afterwards (late punch import, leave approved
  after the run, holiday corrected...). This script stores, per employee and work date,
  a fingerprint of the HRM source rows that the last successful calculation was based on.
  Calendar backfill and the stale sweep compare the stored fingerprint to the current
  HRM fingerprint and recalculate only the dates that differ.

Design rules
  - HRM database stays READ ONLY. Nothing here writes to HRM.dbo.*.
  - The fingerprint is captured BEFORE the calculation and committed only AFTER it
    succeeds, so a source change that lands mid-calculation is still detected next time.
  - If the current fingerprint cannot be computed (employee not in HRM / locked), the
    date is NOT reported stale, so it can never trigger an endless recalculation loop.
  - Idempotent and safe to run before the new application build is deployed.

Known gap (documented on purpose)
  Shift schedule tables (HRM.dbo.CC_LichTrinhCa / tblCa) are not part of the fingerprint
  because their employee mapping is not verified in this repository. Add them to
  dbo.fn_HrmAttendanceSourceStamp once the mapping is confirmed.
===============================================================================
*/

IF OBJECT_ID(N'dbo.F03HrmAttendanceSourceStamp', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03HrmAttendanceSourceStamp
    (
        EmployeeCode nvarchar(50) NOT NULL,
        WorkDate date NOT NULL,
        SourceStamp nvarchar(200) NULL,
        CalculatedAt datetime2(3) NULL,
        CalculationBatchId uniqueidentifier NULL,
        PendingStamp nvarchar(200) NULL,
        PendingCaptureId uniqueidentifier NULL,
        PendingAt datetime2(3) NULL,
        CONSTRAINT PK_F03HrmAttendanceSourceStamp PRIMARY KEY CLUSTERED (EmployeeCode, WorkDate)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.F03HrmAttendanceSourceStamp')
      AND name = N'IX_F03HrmAttendanceSourceStamp_Pending'
)
    CREATE INDEX IX_F03HrmAttendanceSourceStamp_Pending
        ON dbo.F03HrmAttendanceSourceStamp (PendingCaptureId, WorkDate)
        INCLUDE (EmployeeCode, PendingStamp);
GO

CREATE OR ALTER FUNCTION dbo.fn_HrmAttendanceSourceStamp
(
    @EmployeeCode nvarchar(50),
    @FromDate date,
    @ToDate date
)
RETURNS TABLE
AS
RETURN
(
    WITH Days AS
    (
        SELECT TOP (CASE WHEN @ToDate >= @FromDate THEN DATEDIFF(DAY, @FromDate, @ToDate) + 1 ELSE 0 END)
               DATEADD(DAY, ROW_NUMBER() OVER (ORDER BY (SELECT 1)) - 1, @FromDate) AS WorkDate
        FROM sys.all_objects
    ),
    Emp AS
    (
        SELECT TOP (1) nv.NVMa, ISNULL(nv.DLocked, 0) AS EmpLocked
        FROM HRM.dbo.tblNhanVien AS nv
        WHERE RTRIM(nv.NVMaNV) = @EmployeeCode
          AND ISNULL(nv.DLocked, 0) = 0
    )
    SELECT d.WorkDate,
           CONCAT(
               N'P', p.Cnt, N':', p.MaxId, N':', p.Agg,
               N'|L', l.Cnt, N':', l.Agg,
               N'|H', h.Cnt, N':', h.Agg,
               N'|B', b.Cnt, N':', b.Agg,
               N'|E', eh.Cnt, N':', eh.Agg,
               N'|G', gh.Cnt, N':', gh.Agg) AS SourceStamp
    FROM Days AS d
    CROSS JOIN Emp AS e
    -- Raw punches (window mirrors overnight C3 handling: previous day .. next day).
    CROSS APPLY
    (
        SELECT COUNT_BIG(*) AS Cnt,
               MAX(r.ID) AS MaxId,
               CHECKSUM_AGG(CHECKSUM(r.ID, r.ThoiGian, r.Status, ISNULL(r.DLocked, 0))) AS Agg
        FROM HRM.dbo.tblCapThe AS c
        INNER JOIN HRM.dbo.RecordDataNew AS r ON r.IDCard = c.CTMaThe
        WHERE c.CTMaNV = e.NVMa
          AND r.ThoiGian >= DATEADD(DAY, -1, d.WorkDate)
          AND r.ThoiGian <  DATEADD(DAY, 2, d.WorkDate)
    ) AS p
    -- Daily leave registrations (DLocked is part of the checksum so lock changes are seen).
    CROSS APPLY
    (
        SELECT COUNT_BIG(*) AS Cnt,
               CHECKSUM_AGG(CHECKSUM(k.DKNMa, k.DKNLoai, k.DKNMaLyDo, k.DKNNghiBu, ISNULL(k.DLocked, 0))) AS Agg
        FROM HRM.dbo.tblDangKyNghi AS k
        WHERE k.DKNMaNV = e.NVMa
          AND CAST(k.DKNNgayApDung AS date) <= d.WorkDate
          AND CAST(k.DKNNgayKetThuc AS date) >= d.WorkDate
    ) AS l
    -- Per-employee holiday / holiday-work assignments.
    CROSS APPLY
    (
        SELECT COUNT_BIG(*) AS Cnt,
               CHECKSUM_AGG(CHECKSUM(x.NLNVMa, x.NNLLoai)) AS Agg
        FROM HRM.dbo.tblNgayNghiLeNhanVien AS x
        WHERE (x.NLNVMaNV = e.NVMa OR x.NLNVMaNV IS NULL)
          AND CAST(x.NLNVNgay AS date) = d.WorkDate
    ) AS h
    -- Compensatory leave registrations.
    CROSS APPLY
    (
        SELECT COUNT_BIG(*) AS Cnt,
               CHECKSUM_AGG(CHECKSUM(y.NBMa, y.NBNgayNghi, y.NBNgayLamBu)) AS Agg
        FROM HRM.dbo.tblDangKyNghiBu AS y
        WHERE y.NBMaNV = e.NVMa
          AND (CAST(y.NBNgayNghi AS date) = d.WorkDate OR CAST(y.NBNgayLamBu AS date) = d.WorkDate)
    ) AS b
    -- Hourly leave registrations.
    CROSS APPLY
    (
        SELECT COUNT_BIG(*) AS Cnt,
               CHECKSUM_AGG(CHECKSUM(z.DKNMa, z.TGNghi, z.Sophut, z.MaLyDo)) AS Agg
        FROM HRM.dbo.tblDangKyNghiTheoGio AS z
        WHERE z.MaNV = e.NVMa
          AND CAST(z.Ngay AS date) = d.WorkDate
    ) AS eh
    -- Company holiday calendar.
    CROSS APPLY
    (
        SELECT COUNT_BIG(*) AS Cnt,
               CHECKSUM_AGG(CHECKSUM(g.NNLMa, g.NNLLoai, ISNULL(g.DLocked, 0))) AS Agg
        FROM HRM.dbo.tblNgayNghiLe AS g
        WHERE CAST(g.NNLNgay AS date) = d.WorkDate
    ) AS gh
);
GO

/* Phase 1 - capture the CURRENT HRM fingerprint as "pending" before the calculation starts. */
CREATE OR ALTER PROCEDURE dbo.usp_CaptureHrmAttendanceSourceStamp
    @WorkDate date,
    @CaptureId uniqueidentifier,
    @EmployeeCode nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @now datetime2(3) = SYSUTCDATETIME();

    CREATE TABLE #Src (EmployeeCode nvarchar(50) NOT NULL PRIMARY KEY, SourceStamp nvarchar(200) NULL);

    INSERT INTO #Src (EmployeeCode, SourceStamp)
    SELECT e.EmployeeCode, s.SourceStamp
    FROM
    (
        SELECT RTRIM(nv.NVMaNV) AS EmployeeCode
        FROM HRM.dbo.tblNhanVien AS nv
        WHERE ISNULL(nv.DLocked, 0) = 0
          AND nv.NVMaNV IS NOT NULL
          AND (@EmployeeCode IS NULL OR RTRIM(nv.NVMaNV) = @EmployeeCode)
        GROUP BY RTRIM(nv.NVMaNV)
    ) AS e
    CROSS APPLY dbo.fn_HrmAttendanceSourceStamp(e.EmployeeCode, @WorkDate, @WorkDate) AS s;

    UPDATE t
    SET PendingStamp = s.SourceStamp, PendingCaptureId = @CaptureId, PendingAt = @now
    FROM dbo.F03HrmAttendanceSourceStamp AS t
    INNER JOIN #Src AS s ON s.EmployeeCode = t.EmployeeCode
    WHERE t.WorkDate = @WorkDate;

    INSERT INTO dbo.F03HrmAttendanceSourceStamp (EmployeeCode, WorkDate, PendingStamp, PendingCaptureId, PendingAt)
    SELECT s.EmployeeCode, @WorkDate, s.SourceStamp, @CaptureId, @now
    FROM #Src AS s
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.F03HrmAttendanceSourceStamp AS t
        WHERE t.EmployeeCode = s.EmployeeCode AND t.WorkDate = @WorkDate
    );
END;
GO

/* Phase 2 - promote pending -> committed, only for employees the successful batch really calculated. */
CREATE OR ALTER PROCEDURE dbo.usp_CommitHrmAttendanceSourceStamp
    @WorkDate date,
    @CaptureId uniqueidentifier,
    @CalculationBatchId uniqueidentifier = NULL,
    @EmployeeCode nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE s
    SET SourceStamp = s.PendingStamp,
        CalculatedAt = SYSUTCDATETIME(),
        CalculationBatchId = @CalculationBatchId,
        PendingStamp = NULL,
        PendingCaptureId = NULL,
        PendingAt = NULL
    FROM dbo.F03HrmAttendanceSourceStamp AS s
    WHERE s.WorkDate = @WorkDate
      AND s.PendingCaptureId = @CaptureId
      AND s.PendingStamp IS NOT NULL
      AND
      (
          -- Employee-scoped job: the job itself succeeded for exactly this employee.
          (@EmployeeCode IS NOT NULL AND s.EmployeeCode = @EmployeeCode)
          -- Department/company job: only employees present in the successful batch.
          OR (@EmployeeCode IS NULL AND @CalculationBatchId IS NOT NULL AND EXISTS
              (
                  SELECT 1 FROM dbo.F03HrmAttendanceCalculated AS a
                  WHERE a.CalculationBatchId = @CalculationBatchId
                    AND a.WorkDate = @WorkDate
                    AND RTRIM(a.EmployeeCode) = s.EmployeeCode
              ))
      );
END;
GO
