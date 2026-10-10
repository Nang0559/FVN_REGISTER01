/*
===============================================================================
FVN_REGISTER - REQUEST MODULE CANONICALIZATION
===============================================================================
Canonical RequestModule persistence codes are:
  LEAVE, OT, TRIP, EQUIPMENT, ATTENDANCE, PAYROLL, ACCESSCHANGE, ENDPOINT

Older databases may contain enum/display names such as 'Overtime'. Normalize
those values once so the database and EF converter use the same canonical code.
===============================================================================
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.F03Approvers', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.F03Approvers', N'RequestType') IS NOT NULL
BEGIN
    UPDATE dbo.F03Approvers
    SET RequestType = CASE UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), RequestType))))
        WHEN N'LEAVE' THEN N'LEAVE'
        WHEN N'LEAVE_REQUEST' THEN N'LEAVE'
        WHEN N'OT' THEN N'OT'
        WHEN N'OVERTIME' THEN N'OT'
        WHEN N'TRIP' THEN N'TRIP'
        WHEN N'TRIP_REQUEST' THEN N'TRIP'
        WHEN N'EQUIPMENT' THEN N'EQUIPMENT'
        WHEN N'ATTENDANCE' THEN N'ATTENDANCE'
        WHEN N'PAYROLL' THEN N'PAYROLL'
        WHEN N'ACCESSCHANGE' THEN N'ACCESSCHANGE'
        WHEN N'ACCESS_CHANGE' THEN N'ACCESSCHANGE'
        WHEN N'ENDPOINT' THEN N'ENDPOINT'
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
        WHEN N'LEAVE' THEN N'LEAVE'
        WHEN N'LEAVE_REQUEST' THEN N'LEAVE'
        WHEN N'OT' THEN N'OT'
        WHEN N'OVERTIME' THEN N'OT'
        WHEN N'TRIP' THEN N'TRIP'
        WHEN N'TRIP_REQUEST' THEN N'TRIP'
        WHEN N'EQUIPMENT' THEN N'EQUIPMENT'
        WHEN N'ATTENDANCE' THEN N'ATTENDANCE'
        WHEN N'PAYROLL' THEN N'PAYROLL'
        WHEN N'ACCESSCHANGE' THEN N'ACCESSCHANGE'
        WHEN N'ACCESS_CHANGE' THEN N'ACCESSCHANGE'
        WHEN N'ENDPOINT' THEN N'ENDPOINT'
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
              (N'LEAVE', N'OT', N'TRIP', N'EQUIPMENT', N'ATTENDANCE', N'PAYROLL', N'ACCESSCHANGE', N'ENDPOINT')
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
              (N'LEAVE', N'OT', N'TRIP', N'EQUIPMENT', N'ATTENDANCE', N'PAYROLL', N'ACCESSCHANGE', N'ENDPOINT')
    )
        THROW 57002, N'F03ApprovalPolicies contains an unsupported RequestType value.', 1;
END;
GO

PRINT N'RequestModule canonicalization completed.';
GO
