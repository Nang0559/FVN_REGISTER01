/*
===============================================================================
FVN_REGISTER - DepartmentCode INT guard (replaces the old text-code patch)
===============================================================================
Canonical contract (see 71_DepartmentCodeInt.sql):
  DeptCode   = INT, identical to HRM BPMa / NVMaBP.
  HrmDeptId  = same numeric value, used when filtering HRM source tables.

The previous revision of this file rewrote usp_CalculateHrmAttendance to treat
DeptCode as TEXT. That contradicts the INT standard, so it is now a pure guard:
it never alters the procedure, it only fails the deployment if the installed
procedure is not INT-canonical.
===============================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.usp_CalculateHrmAttendance', N'P') IS NULL
    THROW 51331, N'usp_CalculateHrmAttendance must exist before the DepartmentCode INT guard.', 1;

DECLARE @Definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_CalculateHrmAttendance', N'P'));

IF @Definition IS NULL
    THROW 51332, N'Cannot read definition of dbo.usp_CalculateHrmAttendance.', 1;

IF CHARINDEX(N'@DeptCode int', @Definition) = 0
   OR CHARINDEX(N'@HrmDeptId int=@DeptCode', @Definition) = 0
    THROW 51339, N'usp_CalculateHrmAttendance is not INT-canonical: expected @DeptCode int and @HrmDeptId int=@DeptCode. Re-run 22_03_CalculateHrmAttendance.sql.', 1;

PRINT N'DepartmentCode INT guard passed: usp_CalculateHrmAttendance uses @DeptCode int (HRM BPMa).';
