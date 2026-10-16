USE [FVN_REGISTER];
GO
SET NOCOUNT ON;

/*
   Canonical approval email templates for the complete RequestModule enum.
   40_EmailCenter.sql seeds the original Leave/OT/Equipment templates.
   This compatibility migration adds the newer Payroll/AccessChange/Endpoint
   templates and their dispatch policies so existing databases converge too.
*/

IF OBJECT_ID(N'dbo.F03EmailTemplates', N'U') IS NULL
    THROW 51061, 'F03EmailTemplates is required before 61_RequestModuleEmailTemplates.sql.', 1;

IF OBJECT_ID(N'dbo.F03EmailDispatchPolicies', N'U') IS NULL
    THROW 51062, 'F03EmailDispatchPolicies is required before 61_RequestModuleEmailTemplates.sql.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.F03EmailTemplates WHERE Code = N'PAYROLL_APPROVAL_REQUEST')
    INSERT dbo.F03EmailTemplates(IsActive, CreatedBy, CreatedAt, Code, Subject, Body, Description)
    VALUES (1,0,GETDATE(),N'PAYROLL_APPROVAL_REQUEST',N'[FCC Smart Portal] Yêu cầu phê duyệt tính lương',N'<p>Có yêu cầu tính lương đang chờ phê duyệt.</p><p>Vui lòng đăng nhập FCC Smart Portal để xử lý.</p>',N'Yêu cầu phê duyệt tính lương');

IF NOT EXISTS (SELECT 1 FROM dbo.F03EmailTemplates WHERE Code = N'ACCESSCHANGE_APPROVAL_REQUEST')
    INSERT dbo.F03EmailTemplates(IsActive, CreatedBy, CreatedAt, Code, Subject, Body, Description)
    VALUES (1,0,GETDATE(),N'ACCESSCHANGE_APPROVAL_REQUEST',N'[FCC Smart Portal] Yêu cầu thay đổi quyền truy cập',N'Có yêu cầu thay đổi quyền truy cập đang chờ phê duyệt. Vui lòng đăng nhập FCC Smart Portal để xử lý.',N'Yêu cầu phê duyệt thay đổi quyền truy cập');

IF NOT EXISTS (SELECT 1 FROM dbo.F03EmailTemplates WHERE Code = N'ENDPOINT_APPROVAL_REQUEST')
    INSERT dbo.F03EmailTemplates(IsActive, CreatedBy, CreatedAt, Code, Subject, Body, Description)
    VALUES (1,0,GETDATE(),N'ENDPOINT_APPROVAL_REQUEST',N'[FCC Smart Portal] Yêu cầu quản lý thiết bị đầu cuối',N'Có yêu cầu quản lý thiết bị đầu cuối đang chờ phê duyệt. Vui lòng đăng nhập FCC Smart Portal để xử lý.',N'Yêu cầu phê duyệt Endpoint Governance');

IF NOT EXISTS (SELECT 1 FROM dbo.F03EmailDispatchPolicies WHERE TemplateCode = N'PAYROLL_APPROVAL_REQUEST' AND Priority = 100)
    INSERT dbo.F03EmailDispatchPolicies(IsActive,CreatedBy,CreatedAt,TemplateCode,EmailProfileCode,DispatchMode,Priority,Description)
    VALUES (1,0,GETDATE(),N'PAYROLL_APPROVAL_REQUEST',N'SYSTEMSMTP',N'AutoSend',100,N'Yêu cầu duyệt tính lương');

IF NOT EXISTS (SELECT 1 FROM dbo.F03EmailDispatchPolicies WHERE TemplateCode = N'ACCESSCHANGE_APPROVAL_REQUEST' AND Priority = 100)
    INSERT dbo.F03EmailDispatchPolicies(IsActive,CreatedBy,CreatedAt,TemplateCode,EmailProfileCode,DispatchMode,Priority,Description)
    VALUES (1,0,GETDATE(),N'ACCESSCHANGE_APPROVAL_REQUEST',N'SYSTEMSMTP',N'AutoSend',100,N'Yêu cầu duyệt thay đổi quyền truy cập');

IF NOT EXISTS (SELECT 1 FROM dbo.F03EmailDispatchPolicies WHERE TemplateCode = N'ENDPOINT_APPROVAL_REQUEST' AND Priority = 100)
    INSERT dbo.F03EmailDispatchPolicies(IsActive,CreatedBy,CreatedAt,TemplateCode,EmailProfileCode,DispatchMode,Priority,Description)
    VALUES (1,0,GETDATE(),N'ENDPOINT_APPROVAL_REQUEST',N'SYSTEMSMTP',N'AutoSend',100,N'Yêu cầu duyệt Endpoint Governance');

PRINT N'RequestModule approval email templates/policies synchronized.';
GO
