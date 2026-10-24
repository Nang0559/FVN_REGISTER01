/*
  E2E-only preparation for registration -> approval -> email tests.
  Run AFTER the normal schema migrations and SQL/06_Seed.sql, only against a
  disposable database. Never run this against production.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT LIKE '%TEST%'
   AND DB_NAME() NOT LIKE '%QA%'
   AND DB_NAME() NOT LIKE '%E2E%'
   AND DB_NAME() NOT LIKE '%AUTOMATION%'
    THROW 52001, 'Safety stop: select a disposable Test/QA/E2E/Automation database first.', 1;

IF OBJECT_ID(N'dbo.F03Employees',N'U') IS NULL
 OR OBJECT_ID(N'dbo.F03Users',N'U') IS NULL
 OR OBJECT_ID(N'dbo.F03Genders',N'U') IS NULL
 OR OBJECT_ID(N'dbo.F03Permissions',N'U') IS NULL
 OR OBJECT_ID(N'dbo.F03EmailProfiles',N'U') IS NULL
 OR OBJECT_ID(N'dbo.F03EmailDispatchPolicies',N'U') IS NULL
 OR OBJECT_ID(N'dbo.F03EmailTemplates',N'U') IS NULL
    THROW 52002, 'Required schema is missing. Apply normal migrations and SQL/06_Seed.sql first.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.F03Departments WHERE DeptCode=14)
 OR NOT EXISTS (SELECT 1 FROM dbo.F03Positions WHERE PositionCode=N'0012')
 OR NOT EXISTS (SELECT 1 FROM dbo.F03Permissions WHERE PermissionCode=5)
 OR NOT EXISTS (SELECT 1 FROM dbo.F03Genders WHERE GenderCode=N'M')
    THROW 52003, 'Required seed lookup is missing: department 14, position 0012, PermissionCode 5, or Male gender.', 1;

BEGIN TRANSACTION;

DECLARE @MaleGenderId int=(SELECT TOP(1) Id FROM dbo.F03Genders WHERE GenderCode=N'M' ORDER BY Id);

IF NOT EXISTS (SELECT 1 FROM dbo.F03Employees WHERE EmployeeCode=N'E0099')
BEGIN
    INSERT dbo.F03Employees
        (IsActive,CreatedBy,EmployeeCode,EmployeeName,DeptCode,PositionCode,BirthDate,GenderCode,EmailAddress,PhoneNumber,FirstWorkingDate,TotalLeaveDays,EmployeeNo,LevelApprove)
    VALUES
        (1,0,N'E0099',N'E2E Unauthorized Peer',14,N'0012','1995-01-01',@MaleGenderId,N'e0099@test.local',N'0900000099','2024-01-01',12,99,0);
END
ELSE
BEGIN
    UPDATE dbo.F03Employees
    SET IsActive=1,EmployeeName=N'E2E Unauthorized Peer',DeptCode=14,PositionCode=N'0012',
        GenderCode=@MaleGenderId,EmailAddress=N'e0099@test.local',PhoneNumber=N'0900000099',LevelApprove=0
    WHERE EmployeeCode=N'E0099';
END;

IF NOT EXISTS (SELECT 1 FROM dbo.F03Users WHERE EmployeeCode=N'E0099')
BEGIN
    INSERT dbo.F03Users
        (IsActive,CreatedBy,Password,EmployeeCode,FullName,PermissionCode,LockoutEnable,NumLoginFailed,LevelApprove,DeptCode,Cvcode)
    VALUES
        (1,0,N'f925916e2754e5e03f75dd58a5733251',N'E0099',N'E2E Unauthorized Peer',5,1,0,0,14,N'0012');
END
ELSE
BEGIN
    UPDATE dbo.F03Users
    SET IsActive=1,Password=N'f925916e2754e5e03f75dd58a5733251',
        FullName=N'E2E Unauthorized Peer',PermissionCode=5,LockoutEnable=1,NumLoginFailed=0,
        LevelApprove=0,DeptCode=14,Cvcode=N'0012'
    WHERE EmployeeCode=N'E0099';
END;

-- Ensure the catch-all role maps an unmatched department/position to ordinary User.
IF NOT EXISTS
(
    SELECT 1 FROM dbo.F03HrmUserRoleRules
    WHERE DeptCode IS NULL AND PositionCode IS NULL AND PermissionCode=5
)
BEGIN
    INSERT dbo.F03HrmUserRoleRules
        (IsActive,CreatedBy,LastModifiedSource,DeptCode,PositionCode,PermissionCode,Priority,Note)
    VALUES
        (1,0,N'E2E',NULL,NULL,5,999,N'E2E default ordinary user');
END;

-- The seed's ITSYS profile points at smtp.gmail.com. Disable all other profiles
-- and route active dispatch policies to a local Mailpit/smtp4dev capture only.
UPDATE dbo.F03EmailProfiles SET IsActive=0,IsDefault=0;
UPDATE dbo.F03EmailProfiles
SET IsActive=1,IsDefault=1,EmailServerName=N'localhost',EmailServerType=N'SMTP',
    EmailServerPort=1025,EmailServerEnableSsl=0,SecurityMode=N'None',
    AuthenticationType=N'None',EmailAccountName=N'e2e@localhost',
    EmailAddress=N'e2e@localhost',EmailPassword=NULL,SiteUrl=N'http://localhost'
WHERE Code=N'ITSYS';

IF @@ROWCOUNT=0
    THROW 52004, 'Email profile ITSYS is missing. Run SQL/06_Seed.sql and SQL/40_EmailCenter.sql first.', 1;

UPDATE dbo.F03EmailDispatchPolicies
SET IsActive=1,EmailProfileCode=N'ITSYS',DispatchMode=N'AutoSend'
WHERE IsActive=1;

IF NOT EXISTS (SELECT 1 FROM dbo.F03EmailTemplates WHERE Code=N'LEAVE_APPROVAL_REQUEST' AND IsActive=1)
    THROW 52005, 'Active LEAVE_APPROVAL_REQUEST email template is missing. Apply SQL/40_EmailCenter.sql.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.F03EmailDispatchPolicies WHERE TemplateCode=N'LEAVE_APPROVAL_REQUEST' AND IsActive=1)
BEGIN
    INSERT dbo.F03EmailDispatchPolicies
        (IsActive,CreatedBy,CreatedAt,TemplateCode,EmailProfileCode,DispatchMode,Priority,Description)
    VALUES
        (1,0,GETDATE(),N'LEAVE_APPROVAL_REQUEST',N'ITSYS',N'AutoSend',100,N'E2E local SMTP capture');
END;

COMMIT TRANSACTION;

PRINT N'E2E preparation complete.';
PRINT N'Test-only peer: E0099 / password Test@123; position 0012; PermissionCode 5 (User).';
PRINT N'Active email profile ITSYS now targets localhost:1025. Ensure Mailpit/smtp4dev is running before E2E.';
