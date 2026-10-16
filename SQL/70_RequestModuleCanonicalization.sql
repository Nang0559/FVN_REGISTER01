/*
===============================================================================
FVN_REGISTER - REQUEST MODULE CANONICALIZATION
===============================================================================
Canonical RequestModule persistence values are the enum names:
  Leave, Overtime, Trip, Equipment, Attendance, Payroll, AccessChange, Endpoint

Older databases may contain short codes such as 'OT' / 'LEAVE'. Normalize
those values once so the database and EF converter use the same value.
===============================================================================
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.F03Approvers', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.F03Approvers', N'RequestType') IS NOT NULL
BEGIN
    UPDATE dbo.F03Approvers
    SET RequestType = CASE UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), RequestType))))
        WHEN N'LEAVE' THEN N'Leave'
        WHEN N'LEAVE_REQUEST' THEN N'Leave'
        WHEN N'OT' THEN N'Overtime'
        WHEN N'OVERTIME' THEN N'Overtime'
        WHEN N'TRIP' THEN N'Trip'
        WHEN N'TRIP_REQUEST' THEN N'Trip'
        WHEN N'EQUIPMENT' THEN N'Equipment'
        WHEN N'ATTENDANCE' THEN N'Attendance'
        WHEN N'PAYROLL' THEN N'Payroll'
        WHEN N'ACCESSCHANGE' THEN N'AccessChange'
        WHEN N'ACCESS_CHANGE' THEN N'AccessChange'
        WHEN N'ENDPOINT' THEN N'Endpoint'
        ELSE RequestType
    END
    WHERE UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), RequestType)))) IN
    (
        N'LEAVE', N'LEAVE_REQUEST', N'OT', N'OVERTIME', N'TRIP', N'TRIP_REQUEST',
        N'EQUIPMENT', N'ATTENDANCE', N'PAYROLL', N'ACCESSCHANGE', N'ACCESS_CHANGE', N'ENDPOINT'
    );
END;
GO

IF OBJECT_ID(N'dbo.F03ApprovalPolicies', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.F03ApprovalPolicies', N'RequestType') IS NOT NULL
BEGIN
    UPDATE dbo.F03ApprovalPolicies
    SET RequestType = CASE UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), RequestType))))
        WHEN N'LEAVE' THEN N'Leave'
        WHEN N'LEAVE_REQUEST' THEN N'Leave'
        WHEN N'OT' THEN N'Overtime'
        WHEN N'OVERTIME' THEN N'Overtime'
        WHEN N'TRIP' THEN N'Trip'
        WHEN N'TRIP_REQUEST' THEN N'Trip'
        WHEN N'EQUIPMENT' THEN N'Equipment'
        WHEN N'ATTENDANCE' THEN N'Attendance'
        WHEN N'PAYROLL' THEN N'Payroll'
        WHEN N'ACCESSCHANGE' THEN N'AccessChange'
        WHEN N'ACCESS_CHANGE' THEN N'AccessChange'
        WHEN N'ENDPOINT' THEN N'Endpoint'
        ELSE RequestType
    END
    WHERE UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), RequestType)))) IN
    (
        N'LEAVE', N'LEAVE_REQUEST', N'OT', N'OVERTIME', N'TRIP', N'TRIP_REQUEST',
        N'EQUIPMENT', N'ATTENDANCE', N'PAYROLL', N'ACCESSCHANGE', N'ACCESS_CHANGE', N'ENDPOINT'
    );
END;
GO

/* Fail-fast diagnostic: values outside the canonical set must be fixed before
   they reach an EF RequestModule value converter. */
IF OBJECT_ID(N'dbo.F03Approvers', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.F03Approvers', N'RequestType') IS NOT NULL
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM dbo.F03Approvers
        WHERE UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), RequestType)))) NOT IN
              (N'LEAVE', N'OVERTIME', N'TRIP', N'EQUIPMENT', N'ATTENDANCE', N'PAYROLL', N'ACCESSCHANGE', N'ENDPOINT')
    )
        THROW 57001, N'F03Approvers contains an unsupported RequestType value.', 1;
END;

IF OBJECT_ID(N'dbo.F03ApprovalPolicies', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.F03ApprovalPolicies', N'RequestType') IS NOT NULL
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM dbo.F03ApprovalPolicies
        WHERE UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), RequestType)))) NOT IN
              (N'LEAVE', N'OVERTIME', N'TRIP', N'EQUIPMENT', N'ATTENDANCE', N'PAYROLL', N'ACCESSCHANGE', N'ENDPOINT')
    )
        THROW 57002, N'F03ApprovalPolicies contains an unsupported RequestType value.', 1;
END;
GO

PRINT N'RequestModule canonicalization completed.';
GO
