/*
===============================================================================
79 - Thống nhất mã module Overtime: ModuleCode 'OT' -> 'Overtime'

Phạm vi (module Execution / Work Calendar / Business rule):
  - Mọi bảng có cột ModuleCode (execution reconciliations/policies, calendar
    module definitions/policies, action policies/items...) TRỪ nhóm khóa quyền
    (F03Functions, F03SecurityFunctionRegistry): FunctionKey 'OT.View', ModuleCode 'OT'
    của nhóm này là định danh quyền, KHÔNG thuộc phạm vi script này.
  - dbo.F03BusinessRules.Module

Cách dùng:
  1) Backup DB (hoặc backup các bảng liệt kê ở result set 1).
  2) Chạy với @DryRun = 1: xem bảng/cột/số dòng sẽ đổi.
  3) Chạy với @DryRun = 0: ghi thật trong MỘT transaction (lỗi -> rollback toàn bộ).
     Nếu một bảng có unique index và đã tồn tại dòng 'Overtime' trùng khóa thì script
     dừng và báo bảng nào; xử lý dòng trùng rồi chạy lại.
===============================================================================
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun bit = 1;   -- <<< 0 = ghi thật

IF OBJECT_ID('tempdb..#Targets') IS NOT NULL DROP TABLE #Targets;
CREATE TABLE #Targets
(
    SchemaName sysname NOT NULL,
    TableName  sysname NOT NULL,
    ColumnName sysname NOT NULL,
    RowsToChange int   NULL
);

INSERT #Targets(SchemaName, TableName, ColumnName)
SELECT s.name, t.name, c.name
FROM sys.columns c
JOIN sys.tables  t ON t.object_id = c.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.types   ty ON ty.user_type_id = c.user_type_id
WHERE c.name = N'ModuleCode'
  AND ty.name IN (N'nvarchar', N'varchar')
  AND t.name NOT IN (N'F03Functions', N'F03SecurityFunctionRegistry')
  AND t.name NOT LIKE N'%bak%';

IF OBJECT_ID(N'dbo.F03BusinessRules', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.F03BusinessRules', N'Module') IS NOT NULL
    INSERT #Targets(SchemaName, TableName, ColumnName) VALUES (N'dbo', N'F03BusinessRules', N'Module');

DECLARE @sql nvarchar(max), @s sysname, @t sysname, @c sysname, @n int;

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT SchemaName, TableName, ColumnName FROM #Targets;
OPEN cur;
FETCH NEXT FROM cur INTO @s, @t, @c;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'SELECT @n = COUNT(*) FROM ' + QUOTENAME(@s) + N'.' + QUOTENAME(@t)
             + N' WHERE LTRIM(RTRIM(' + QUOTENAME(@c) + N')) IN (N''OT'', N''OVERTIME'')'
             + N' AND ' + QUOTENAME(@c) + N' COLLATE Latin1_General_BIN2 <> N''Overtime'';';
    EXEC sp_executesql @sql, N'@n int OUTPUT', @n = @n OUTPUT;
    UPDATE #Targets SET RowsToChange = @n WHERE SchemaName = @s AND TableName = @t AND ColumnName = @c;
    FETCH NEXT FROM cur INTO @s, @t, @c;
END
CLOSE cur; DEALLOCATE cur;

-- Result set 1: những gì sẽ đổi
SELECT SchemaName, TableName, ColumnName, RowsToChange
FROM #Targets
ORDER BY RowsToChange DESC, TableName;

IF @DryRun = 1
BEGIN
    PRINT N'@DryRun = 1: CHUA ghi gi. Dat @DryRun = 0 de ghi that.';
END
ELSE
BEGIN
    BEGIN TRANSACTION;

    DECLARE cur2 CURSOR LOCAL FAST_FORWARD FOR
        SELECT SchemaName, TableName, ColumnName FROM #Targets WHERE RowsToChange > 0;
    OPEN cur2;
    FETCH NEXT FROM cur2 INTO @s, @t, @c;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            SET @sql = N'UPDATE ' + QUOTENAME(@s) + N'.' + QUOTENAME(@t)
                     + N' SET ' + QUOTENAME(@c) + N' = N''Overtime'''
                     + N' WHERE LTRIM(RTRIM(' + QUOTENAME(@c) + N')) IN (N''OT'', N''OVERTIME'')'
                     + N' AND ' + QUOTENAME(@c) + N' COLLATE Latin1_General_BIN2 <> N''Overtime'';';
            EXEC sp_executesql @sql;
            PRINT CONCAT(N'OK   ', @s, N'.', @t, N'.', @c, N' : ', @@ROWCOUNT, N' dong');
        END TRY
        BEGIN CATCH
            PRINT CONCAT(N'LOI  ', @s, N'.', @t, N'.', @c, N' : ', ERROR_MESSAGE());
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
            CLOSE cur2; DEALLOCATE cur2;
            THROW;
        END CATCH
        FETCH NEXT FROM cur2 INTO @s, @t, @c;
    END
    CLOSE cur2; DEALLOCATE cur2;

    COMMIT TRANSACTION;
    PRINT N'Da chuan hoa xong. Chay lai script voi @DryRun = 1 de kiem tra: RowsToChange phai bang 0.';
END

DROP TABLE #Targets;
GO
