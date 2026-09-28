/*
===============================================================================
FVN_REGISTER - DepartmentCode compatibility patch
===============================================================================
Canonical contract:
  DeptCode  = department code used by FVN/HRM, textual value.
              Never convert @DeptCode to int.
  HrmDeptId = numeric HRM department identifier when HRM exposes one.
              For the current HRM attendance source this is NVMaBP/BCMaBP.

This patch rewrites the already-created attendance calculation procedure at
SQL deployment time. It intentionally does not change the HRM source schema.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.usp_CalculateHrmAttendance', N'P') IS NULL
    THROW 51331, N'usp_CalculateHrmAttendance must exist before DepartmentCode compatibility patch.', 1;

DECLARE @Definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_CalculateHrmAttendance', N'P'));

IF @Definition IS NULL
    THROW 51332, N'Cannot read definition of dbo.usp_CalculateHrmAttendance.', 1;

/* Replace the unsafe DeptCode -> int conversion with a normalized textual code. */
SET @Definition = REPLACE(
    @Definition,
    N'DECLARE @BatchId uniqueidentifier=NEWID(),@HrmDeptId int=TRY_CONVERT(int,NULLIF(@DeptCode,N''''));',
    N'DECLARE @BatchId uniqueidentifier=NEWID(),@NormalizedDeptCode nvarchar(20)=NULLIF(LTRIM(RTRIM(@DeptCode)),N'''');'
);

/* Current-state attendance scope is keyed by the textual DeptCode. */
SET @Definition = REPLACE(
    @Definition,
    N'(@HrmDeptId IS NULL OR HrmDeptId=@HrmDeptId)',
    N'(@NormalizedDeptCode IS NULL OR DeptCode=@NormalizedDeptCode)'
);

/* HRM source NVMaBP is the numeric HRM department identifier; compare it to
   the textual DeptCode without converting DeptCode to an integer. */
SET @Definition = REPLACE(
    @Definition,
    N'(@HrmDeptId IS NULL OR nv.NVMaBP=@HrmDeptId)',
    N'(@NormalizedDeptCode IS NULL OR CONVERT(nvarchar(20),nv.NVMaBP)=@NormalizedDeptCode)'
);
SET @Definition = REPLACE(
    @Definition,
    N'(@HrmDeptId IS NULL OR NVMaBP=@HrmDeptId)',
    N'(@NormalizedDeptCode IS NULL OR CONVERT(nvarchar(20),NVMaBP)=@NormalizedDeptCode)'
);

IF CHARINDEX(N'TRY_CONVERT(int,NULLIF(@DeptCode', @Definition) > 0
    THROW 51333, N'DepartmentCode patch failed: unsafe DeptCode-to-int conversion remains.', 1;

IF CHARINDEX(N'@HrmDeptId', @Definition) > 0
    THROW 51334, N'DepartmentCode patch failed: obsolete @HrmDeptId scope variable remains.', 1;

EXEC sys.sp_executesql @Definition;

PRINT N'DepartmentCode compatibility patch applied: DeptCode=text, HrmDeptId=HRM numeric ID.';
GO
