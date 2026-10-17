/*
===============================================================================
96 - THÊM ResponseNote CHO F03SupervisorFeedbacks
===============================================================================
Note        = nội dung nhân viên gửi cấp trên.
ResponseNote = lý do từ chối / ghi chú chuyển HR / ghi chú đăng ký bổ sung của người xử lý.
Chạy SAU 95_FixOTRegistrationPolicyColumns.sql. Idempotent.
===============================================================================
*/
SET NOCOUNT ON;
GO
IF COL_LENGTH(N'dbo.F03SupervisorFeedbacks', N'ResponseNote') IS NULL
    ALTER TABLE dbo.F03SupervisorFeedbacks ADD ResponseNote NVARCHAR(1000) NULL;
GO
