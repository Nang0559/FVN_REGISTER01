/* Dashboard attendance KPI indexes. Run on FVN_REGISTER after attendance schema deployment. */
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'dbo.F03HrmAttendanceCalculated', N'U') IS NULL THROW 51860, N'Missing F03HrmAttendanceCalculated.', 1;
IF OBJECT_ID(N'dbo.F03Employees', N'U') IS NULL THROW 51861, N'Missing F03Employees.', 1;
IF COL_LENGTH(N'dbo.F03HrmAttendanceCalculated', N'WorkDate') IS NULL OR COL_LENGTH(N'dbo.F03HrmAttendanceCalculated', N'EmployeeCode') IS NULL OR COL_LENGTH(N'dbo.F03HrmAttendanceCalculated', N'CheckInTime') IS NULL THROW 51862, N'Missing attendance KPI columns.', 1;
IF COL_LENGTH(N'dbo.F03Employees', N'EmployeeCode') IS NULL OR COL_LENGTH(N'dbo.F03Employees', N'IsActive') IS NULL OR COL_LENGTH(N'dbo.F03Employees', N'DeptCode') IS NULL OR COL_LENGTH(N'dbo.F03Employees', N'Id') IS NULL THROW 51863, N'Missing employee KPI columns.', 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.F03HrmAttendanceCalculated') AND name = N'IX_F03HrmAttendanceCalculated_DashboardPresent')
    CREATE NONCLUSTERED INDEX IX_F03HrmAttendanceCalculated_DashboardPresent ON dbo.F03HrmAttendanceCalculated(WorkDate, EmployeeCode) WHERE CheckInTime IS NOT NULL AND EmployeeCode IS NOT NULL WITH (DATA_COMPRESSION = PAGE);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.F03Employees') AND name = N'IX_F03Employees_DashboardActiveEmployeeCode')
    CREATE NONCLUSTERED INDEX IX_F03Employees_DashboardActiveEmployeeCode ON dbo.F03Employees(EmployeeCode) INCLUDE (Id, DeptCode) WHERE IsActive = 1 WITH (DATA_COMPRESSION = PAGE);
GO
SELECT OBJECT_NAME(i.object_id) AS TableName, i.name AS IndexName, i.is_disabled, i.has_filter, i.filter_definition FROM sys.indexes AS i WHERE i.object_id IN (OBJECT_ID(N'dbo.F03HrmAttendanceCalculated'), OBJECT_ID(N'dbo.F03Employees')) AND i.name IN (N'IX_F03HrmAttendanceCalculated_DashboardPresent', N'IX_F03Employees_DashboardActiveEmployeeCode');
GO
PRINT N'Dashboard attendance KPI indexes are ready.';
GO
