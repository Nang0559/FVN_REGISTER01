/*
  70_FixPublicFormViewCodeCollision.sql

  Problem
  -------
  FunctionCode 2810 was used by two different capabilities:
    * SQL/37  : 2810 = PublicForm.View   (Personal/Own)
    * SQL/53  : 2810 = Payroll.Approve   (overwrites the row above)
  After SQL/53 the row 2810 is "Payroll.Approve". /public-forms calls
  HasPersonalAsync(PublicForm.View) and API returns 403, and the Security Registry
  reconcile then retired the row (IsActive = 0) because the key was not discoverable.

  Fix
  ---
  * PublicForm.View moves to its own FunctionCode 2819 (code: SecurityFunctionCodes.PublicFormView).
  * 2810 stays Payroll.Approve (code: ApprovalSecurityFunctionCodes.PayrollApprove, now declared with
    [SecurityFunctionDefinition] so reconcile no longer retires it).
  * Personal/Own grants that were attached to 2810 for the public forms are moved to 2819.
  * The PublicForm.* functions that were retired are re-enabled.

  Idempotent: safe to run more than once. Review the result sets, then it commits.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRAN;

/* 0. Guard: the key PublicForm.View must not be owned by a different code. */
IF EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionKey = N'PublicForm.View' AND FunctionCode <> 2819)
BEGIN
    DECLARE @owner INT = (SELECT TOP 1 FunctionCode FROM dbo.F03Functions WHERE FunctionKey = N'PublicForm.View' AND FunctionCode <> 2819);
    DECLARE @msg NVARCHAR(300) = CONCAT(N'FunctionKey PublicForm.View is still owned by FunctionCode ', @owner, N'. Rename that key first (Payroll.Approve for 2810).');
    THROW 51000, @msg, 1;
END;

/* 1. 2810 must carry the Payroll.Approve key (what SQL/53 intended). */
UPDATE dbo.F03Functions
SET FunctionKey = N'Payroll.Approve'
WHERE FunctionCode = 2810 AND FunctionKey <> N'Payroll.Approve';

/* 2. Create / repair PublicForm.View on its own code 2819. */
IF NOT EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionCode = 2819)
BEGIN
    INSERT dbo.F03Functions
        (FunctionKey, IsActive, CreatedBy, LastModifiedSource, CreatedAt, FunctionCode, FunctionName, Detail,
         ModuleCode, ActionCode, ScopeCode, DisplayOrder, LifecycleStatus)
    VALUES
        (N'PublicForm.View', 1, 0, N'SYSTEM_FIX_2810_COLLISION', GETDATE(), 2819, N'PublicForm.View',
         N'Xem biểu mẫu được chỉ định cho bản thân', N'PublicForm', N'View', N'Own', 2819, N'Active');
END
ELSE
BEGIN
    UPDATE dbo.F03Functions
    SET FunctionKey = N'PublicForm.View', FunctionName = N'PublicForm.View',
        Detail = N'Xem biểu mẫu được chỉ định cho bản thân',
        ModuleCode = N'PublicForm', ActionCode = N'View', ScopeCode = N'Own',
        IsActive = 1, LifecycleStatus = N'Active'
    WHERE FunctionCode = 2819;
END;

/* 3. Grants: roles 3,4,5 get Personal/Own (same as SQL/37 intended). */
INSERT dbo.F03RoleFunctions (IdRole, IdFunction, ScopeCode, AccessMode)
SELECT r.Id, f.Id, N'Own', N'Personal'
FROM dbo.F03Roles r
CROSS JOIN dbo.F03Functions f
WHERE r.RoleCode IN (3, 4, 5) AND f.FunctionCode = 2819
  AND NOT EXISTS (SELECT 1 FROM dbo.F03RoleFunctions x WHERE x.IdRole = r.Id AND x.IdFunction = f.Id);

/* 3b. Any other role that had the old Personal/Own grant on 2810 keeps it, on 2819. */
INSERT dbo.F03RoleFunctions (IdRole, IdFunction, ScopeCode, AccessMode)
SELECT DISTINCT rf.IdRole, n.Id, N'Own', N'Personal'
FROM dbo.F03RoleFunctions rf
JOIN dbo.F03Functions o ON o.Id = rf.IdFunction AND o.FunctionCode = 2810
CROSS JOIN (SELECT Id FROM dbo.F03Functions WHERE FunctionCode = 2819) n
WHERE rf.IsActive = 1 AND (rf.AccessMode = N'Personal' OR rf.ScopeCode = N'Own')
  AND NOT EXISTS (SELECT 1 FROM dbo.F03RoleFunctions x WHERE x.IdRole = rf.IdRole AND x.IdFunction = n.Id);

/* 3c. Re-activate a Personal/Own grant on 2819 that may exist but be inactive. */
UPDATE rf SET rf.IsActive = 1, rf.ScopeCode = N'Own', rf.AccessMode = N'Personal'
FROM dbo.F03RoleFunctions rf
JOIN dbo.F03Functions f ON f.Id = rf.IdFunction AND f.FunctionCode = 2819
JOIN dbo.F03Roles r ON r.Id = rf.IdRole AND r.RoleCode IN (3, 4, 5)
WHERE rf.IsActive = 0;

/* 4. Remove the stray PublicForm Personal/Own grant from the Payroll.Approve function.
      Payroll.Approve is a Department/Management capability; Personal/Own on it is never valid. */
UPDATE rf SET rf.IsActive = 0
FROM dbo.F03RoleFunctions rf
JOIN dbo.F03Functions f ON f.Id = rf.IdFunction AND f.FunctionCode = 2810
WHERE rf.IsActive = 1 AND (rf.AccessMode = N'Personal' OR rf.ScopeCode = N'Own');

/* 5. Re-enable the PublicForm module functions that reconcile retired.
      Retired/Replaced rows are skipped by reconcile forever, so LifecycleStatus is reset too. */
UPDATE dbo.F03Functions
SET IsActive = 1, LifecycleStatus = N'Active'
WHERE FunctionCode IN (2807, 2808, 2809, 2811, 2812, 2813, 2814, 2815, 2816, 2817, 2818)
  AND FunctionKey LIKE N'PublicForm.%'
  AND (IsActive = 0 OR LifecycleStatus <> N'Active');

/* 6. Payroll.Approve was retired by the same reconcile. It is declared in code now, so enable it again.
      Comment this block out if payroll approval must stay disabled on this environment. */
UPDATE dbo.F03Functions
SET IsActive = 1, LifecycleStatus = N'Active'
WHERE FunctionCode = 2810 AND FunctionKey = N'Payroll.Approve'
  AND (IsActive = 0 OR LifecycleStatus <> N'Active');

/* 7. Registry rows: drop the "Conflict" state caused by two keys sharing 2810. */
IF OBJECT_ID(N'dbo.F03SecurityFunctionRegistry', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.F03SecurityFunctionRegistry
    SET FunctionCode = 2819, LifecycleStatus = N'Active', ResolvedAt = GETDATE()
    WHERE FunctionKey = N'PublicForm.View';

    UPDATE dbo.F03SecurityFunctionRegistry
    SET FunctionCode = 2810, LifecycleStatus = N'Active', ResolvedAt = GETDATE()
    WHERE FunctionKey = N'Payroll.Approve';
END;

/* Review */
SELECT FunctionCode, FunctionKey, FunctionName, IsActive, LifecycleStatus
FROM dbo.F03Functions
WHERE FunctionCode BETWEEN 2807 AND 2819
ORDER BY FunctionCode;

SELECT r.RoleCode, f.FunctionCode, rf.AccessMode, rf.ScopeCode, rf.IsActive AS RfActive
FROM dbo.F03RoleFunctions rf
JOIN dbo.F03Roles r ON r.Id = rf.IdRole
JOIN dbo.F03Functions f ON f.Id = rf.IdFunction
WHERE f.FunctionCode IN (2810, 2819)
ORDER BY f.FunctionCode, r.RoleCode;

COMMIT TRAN;
GO
