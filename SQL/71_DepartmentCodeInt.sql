/*
===============================================================================
71_DepartmentCodeInt.sql  -  FINAL VERIFICATION of the DepartmentCode INT contract
===============================================================================
The conversion itself is done by 05A_DepartmentCodeInt.sql (early in the chain).
This script only verifies that, after every migration has run, the department
identity is INT everywhere and consistent with HRM (NVMaBP / BPMa = INT):

  * no department-code column in dbo.F03* is still textual
  * dbo.usp_CalculateHrmAttendance takes @DeptCode int
  * F03Departments.DeptCode is unique and F03Employees.DeptCode / F03Departments.DeptCode
    have the same type
  * department 57 (IT) exists in F03Departments

It changes nothing; it THROWs (clear message) when the contract is broken.
===============================================================================
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;

DECLARE @Textual nvarchar(max) = N'';
SELECT @Textual = @Textual + t.name + N'.' + c.name + N' (' + ty.name + N'); '
FROM sys.tables  AS t
JOIN sys.schemas AS s  ON s.schema_id = t.schema_id
JOIN sys.columns AS c  ON c.object_id = t.object_id
JOIN sys.types   AS ty ON ty.user_type_id = c.user_type_id
WHERE s.name = N'dbo'
  AND t.name LIKE N'F03%'
  AND t.name NOT IN (N'F03EquipmentImportBatches', N'F03EquipmentFieldDefinitions')
  AND c.name IN (N'DeptCode', N'ParentDeptCode', N'SubDepartmentCode',
                 N'OldDeptCode', N'NewDeptCode',
                 N'CurrentApproveForDeptCode', N'SuggestedApproveForDeptCode',
                 N'OperatingResponsibleDeptCode', N'RepairResponsibleDeptCode',
                 N'ResponsibleDeptCode', N'ApproverDeptCode', N'ApproveForDeptCode')
  AND ty.name <> N'int'
ORDER BY t.name, c.name;

IF @Textual <> N''
BEGIN
    DECLARE @TextualMessage nvarchar(2048) =
        LEFT(N'Department-code columns must be INT but are not: ' + @Textual, 2048);
    THROW 51410, @TextualMessage, 1;
END;
GO

IF OBJECT_ID(N'dbo.usp_CalculateHrmAttendance', N'P') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.parameters AS p
       WHERE p.object_id = OBJECT_ID(N'dbo.usp_CalculateHrmAttendance', N'P')
         AND p.name = N'@DeptCode'
         AND p.system_type_id = TYPE_ID(N'int')
   )
    THROW 51411, N'dbo.usp_CalculateHrmAttendance must declare @DeptCode int.', 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.F03Departments')
                 AND name = N'UX_F03Departments_DeptCode' AND is_unique = 1)
    THROW 51412, N'Unique index UX_F03Departments_DeptCode is missing on dbo.F03Departments(DeptCode).', 1;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.F03Departments WHERE DeptCode = 57)
    THROW 51413, N'Department 57 (IT) is missing from dbo.F03Departments.', 1;
GO

PRINT N'DepartmentCode INT contract verified.';
GO
