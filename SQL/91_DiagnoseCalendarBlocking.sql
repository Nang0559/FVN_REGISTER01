/*
91 - CHẨN ĐOÁN CALENDAR ATTENDANCE BỊ TIMEOUT (CHỈ ĐỌC, KHÔNG SỬA DỮ LIỆU)
Chạy KHI đang bị timeout. Cho biết có phiên nào đang chặn truy vấn lịch (F03HrmAttendanceCalculated/History) không.
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;

-- 1. Phiên đang bị chặn + phiên chặn
SELECT r.session_id, r.blocking_session_id, r.status, r.wait_type, r.wait_time AS wait_ms, r.cpu_time,
       DB_NAME(r.database_id) AS DbName,
       SUBSTRING(t.text, (r.statement_start_offset/2)+1,
                 ((CASE r.statement_end_offset WHEN -1 THEN DATALENGTH(t.text) ELSE r.statement_end_offset END - r.statement_start_offset)/2)+1) AS RunningStatement
FROM sys.dm_exec_requests AS r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE r.session_id <> @@SPID
  AND (r.blocking_session_id <> 0
       OR r.session_id IN (SELECT blocking_session_id FROM sys.dm_exec_requests WHERE blocking_session_id <> 0))
ORDER BY r.blocking_session_id, r.wait_time DESC;

-- 2. Câu lệnh đang chạy lâu chạm 2 bảng chấm công
SELECT r.session_id, r.status, r.total_elapsed_time/1000 AS elapsed_s, r.wait_type,
       LEFT(t.text, 400) AS SqlText
FROM sys.dm_exec_requests AS r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE r.session_id <> @@SPID
  AND (t.text LIKE N'%F03HrmAttendanceCalculated%' OR t.text LIKE N'%F03HrmAttendanceHistory%')
ORDER BY r.total_elapsed_time DESC;

-- 3. Khoá đang giữ trên 2 bảng
SELECT l.request_session_id, OBJECT_NAME(p.object_id) AS TableName, l.resource_type, l.request_mode, l.request_status, COUNT(*) AS Locks
FROM sys.dm_tran_locks AS l
JOIN sys.partitions AS p ON p.hobt_id = l.resource_associated_entity_id
WHERE l.resource_database_id = DB_ID()
  AND OBJECT_NAME(p.object_id) IN (N'F03HrmAttendanceCalculated', N'F03HrmAttendanceHistory')
GROUP BY l.request_session_id, OBJECT_NAME(p.object_id), l.resource_type, l.request_mode, l.request_status
ORDER BY Locks DESC;

-- 4. Index phục vụ lịch phải tồn tại (EmployeeCode, WorkDate) trên cả 2 bảng
SELECT OBJECT_NAME(i.object_id) AS TableName, i.name, i.type_desc, i.is_disabled
FROM sys.indexes AS i
WHERE i.object_id IN (OBJECT_ID(N'dbo.F03HrmAttendanceCalculated'), OBJECT_ID(N'dbo.F03HrmAttendanceHistory'))
  AND i.name LIKE N'IX[_]%EmployeeCode[_]WorkDate%';

/* Tuỳ chọn (cần DBA, tự cân nhắc): READ_COMMITTED_SNAPSHOT để đọc lịch không bị ghi chặn.
   ALTER DATABASE [FVN_REGISTER] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE; */
GO
