/*
    Run once by a SQL administrator.

    Replace FVN_REGISTER_APP with the SQL Server login used by FVN_REGISTER.API.
    VIEW SERVER STATE is required for cross-session DMV diagnostics.
    ALTER ANY CONNECTION is required only for the UI Force terminate action (KILL).

    If Force terminate should remain unavailable to the application account,
    grant only VIEW SERVER STATE and the UI will still monitor/recover stale jobs
    while the Force terminate endpoint will fail safely with a permission error.
*/

USE master;
GO

DECLARE @Login sysname = N'FVN_REGISTER_APP';

IF SUSER_ID(@Login) IS NULL
    THROW 51000, 'Không tìm thấy SQL login FVN_REGISTER_APP. Hãy sửa @Login trong script trước khi chạy.', 1;

DECLARE @sql nvarchar(max);
SET @sql = N'GRANT VIEW SERVER STATE TO ' + QUOTENAME(@Login) + N';';
EXEC sys.sp_executesql @sql;

/* KILL requires ALTER ANY CONNECTION on SQL Server. */
SET @sql = N'GRANT ALTER ANY CONNECTION TO ' + QUOTENAME(@Login) + N';';
EXEC sys.sp_executesql @sql;
GO
