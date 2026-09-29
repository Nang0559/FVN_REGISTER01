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
SET @Definition = REPLACE(
    @Definition,
    N'DECLARE @BatchId uniqueidentifier = NEWID(), @HrmDeptId int = TRY_CONVERT(int, NULLIF(@DeptCode, N''''));',
    N'DECLARE @BatchId uniqueidentifier = NEWID(), @NormalizedDeptCode nvarchar(20) = NULLIF(LTRIM(RTRIM(@DeptCode)), N'''');'
);
SET @Definition = REPLACE(
    @Definition,
    N'DECLARE @HrmDeptId int = TRY_CONVERT(int, NULLIF(@DeptCode, N''''));',
    N'DECLARE @NormalizedDeptCode nvarchar(20) = NULLIF(LTRIM(RTRIM(@DeptCode)), N'''');'
);

/* Current-state attendance scope is keyed by the textual DeptCode. */
SET @Definition = REPLACE(
    @Definition,
    N'(@HrmDeptId IS NULL OR HrmDeptId=@HrmDeptId)',
    N'(@NormalizedDeptCode IS NULL OR DeptCode=@NormalizedDeptCode)'
);
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

/* Replace the multiline OT-actual cleanup predicate as well. */
SET @Definition = REPLACE(
    @Definition,
    N'(
        @HrmDeptId IS NULL
        OR EXISTS
        (
            SELECT 1
            FROM HRM.dbo.tblNhanVien nv
            WHERE nv.NVMa=oa.HrmEmployeeId
              AND nv.NVMaBP=@HrmDeptId
        )
    )',
    N'(
        @NormalizedDeptCode IS NULL
        OR EXISTS
        (
            SELECT 1
            FROM HRM.dbo.tblNhanVien nv
            WHERE nv.NVMa=oa.HrmEmployeeId
              AND CONVERT(nvarchar(20),nv.NVMaBP)=@NormalizedDeptCode
        )
    )'
);

/*
   OBJECT_DEFINITION returns the stored module text, and SQL Server preserves
   the original whitespace/comments. Therefore do not depend on one exact
   spelling such as "CREATE PROCEDURE". Find the actual CREATE token and the
   following PROCEDURE/PROC token and replace the complete header keyword.
   This handles CREATE PROCEDURE, CREATE   PROCEDURE, CREATE + newline,
   CREATE OR ALTER PROCEDURE, and equivalent PROC forms.
*/
DECLARE @CreatePos int = CHARINDEX(N'CREATE', UPPER(@Definition));
IF @CreatePos = 0
    THROW 51338, N'DepartmentCode patch failed: CREATE keyword not found in existing procedure definition.', 1;

DECLARE @ProcedurePos int = CHARINDEX(N'PROCEDURE', UPPER(@Definition), @CreatePos + 6);
DECLARE @ProcPos int = CHARINDEX(N'PROC', UPPER(@Definition), @CreatePos + 6);

/* Prefer PROCEDURE when it is the first valid module keyword after CREATE. */
IF @ProcedurePos > 0 AND (@ProcPos = 0 OR @ProcedurePos <= @ProcPos)
BEGIN
    /* Replace everything from CREATE through PROCEDURE with ALTER PROCEDURE. */
    SET @Definition = STUFF(@Definition, @CreatePos, (@ProcedurePos + LEN(N'PROCEDURE')) - @CreatePos, N'ALTER PROCEDURE');
END
ELSE IF @ProcPos > 0
BEGIN
    SET @Definition = STUFF(@Definition, @CreatePos, (@ProcPos + LEN(N'PROC')) - @CreatePos, N'ALTER PROC');
END
ELSE
    THROW 51338, N'DepartmentCode patch failed: could not locate PROCEDURE/PROC after CREATE in existing procedure definition.', 1;

/* Fail closed if the resulting module is not an ALTER definition. */
DECLARE @Header nvarchar(400) = LTRIM(SUBSTRING(@Definition, 1, 400));
IF @Header NOT LIKE N'ALTER PROCEDURE%' AND @Header NOT LIKE N'ALTER PROC%'
    THROW 51338, N'DepartmentCode patch failed: normalized definition does not begin with ALTER PROCEDURE/ALTER PROC.', 1;

IF CHARINDEX(N'TRY_CONVERT(int,NULLIF(@DeptCode', @Definition) > 0
    THROW 51333, N'DepartmentCode patch failed: unsafe DeptCode-to-int conversion remains.', 1;
IF CHARINDEX(N'TRY_CONVERT(int, NULLIF(@DeptCode', @Definition) > 0
    THROW 51333, N'DepartmentCode patch failed: unsafe DeptCode-to-int conversion remains.', 1;
IF CHARINDEX(N'@HrmDeptId', @Definition) > 0
    THROW 51334, N'DepartmentCode patch failed: obsolete @HrmDeptId scope variable remains.', 1;

EXEC sys.sp_executesql @Definition;

/* Verify the installed procedure, not only the in-memory definition. */
DECLARE @InstalledDefinition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_CalculateHrmAttendance', N'P'));
IF @InstalledDefinition IS NULL
    THROW 51335, N'DepartmentCode patch failed: procedure definition cannot be verified after ALTER.', 1;

IF CHARINDEX(N'@HrmDeptId', @InstalledDefinition) > 0
    THROW 51336, N'DepartmentCode patch failed: installed procedure still contains @HrmDeptId.', 1;
IF CHARINDEX(N'TRY_CONVERT(int,NULLIF(@DeptCode', @InstalledDefinition) > 0
    THROW 51337, N'DepartmentCode patch failed: installed procedure still converts DeptCode to int.', 1;
IF CHARINDEX(N'TRY_CONVERT(int, NULLIF(@DeptCode', @InstalledDefinition) > 0
    THROW 51337, N'DepartmentCode patch failed: installed procedure still converts DeptCode to int.', 1;

PRINT N'DepartmentCode compatibility patch applied and verified: DeptCode=text; no DeptCode-to-int conversion; no @HrmDeptId scope variable remains.';
GO
