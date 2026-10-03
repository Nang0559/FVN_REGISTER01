/*
===============================================================================
16A - Equipment capability seed
Canonical SQL deployment owner migrated from Database/Equipment/004.
Runs immediately after 16_EquipmentFlexibleImport.sql.
===============================================================================
*/

IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2301)
    INSERT INTO dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2301,N'Equipment.View',N'Quyền sử dụng Sổ quản lý thiết bị và tra cứu QR.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2302)
    INSERT INTO dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2302,N'Equipment.Create',N'Đăng ký thiết bị mới.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2303)
    INSERT INTO dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2303,N'Equipment.Edit',N'Chỉnh sửa/gửi đăng ký thiết bị.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2304)
    INSERT INTO dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2304,N'Equipment.Repair',N'Tạo và gửi yêu cầu sửa chữa thiết bị.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2305)
    INSERT INTO dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2305,N'Equipment.Approve',N'Phê duyệt yêu cầu thiết bị.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2306)
    INSERT INTO dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2306,N'Equipment.Import',N'Import Excel thiết bị theo schema của từng phòng ban.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2307)
    INSERT INTO dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2307,N'Equipment.Export',N'Xuất dữ liệu thiết bị.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=2308)
    INSERT INTO dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(2308,N'Equipment.Cancel',N'Hủy yêu cầu thiết bị.',1,GETDATE(),1);
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode=900)
    INSERT INTO dbo.F03Functions(FunctionCode,FunctionName,Detail,CreatedBy,CreatedAt,IsActive)
    VALUES(900,N'EquipmentModule',N'Legacy marker: quyền sử dụng Sổ quản lý thiết bị.',1,GETDATE(),1);
GO
