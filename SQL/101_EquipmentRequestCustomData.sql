/*
===============================================================================
101 - F03EquipmentRequests.CustomDataJson

Vì sao:
  Bản nháp thiết bị (đăng ký mới / thay đổi tài sản, kể cả từ import Excel) cần lưu
  giá trị các trường tùy chỉnh theo schema (field không có TargetProperty). Khi duyệt,
  giá trị này được chép sang F03EquipmentAssets.CustomDataJson.

Idempotent: chạy lại nhiều lần an toàn.
===============================================================================
*/
USE [FVN_REGISTER];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.F03EquipmentRequests', N'CustomDataJson') IS NULL
BEGIN
    ALTER TABLE dbo.F03EquipmentRequests ADD CustomDataJson NVARCHAR(MAX) NULL;
    PRINT N'Đã thêm cột F03EquipmentRequests.CustomDataJson';
END
ELSE
    PRINT N'Cột F03EquipmentRequests.CustomDataJson đã tồn tại - bỏ qua';
GO
