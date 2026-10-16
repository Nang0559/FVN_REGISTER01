USE [FVN_REGISTER];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

/*
  82_FixPublicFormViewCodeCollision.sql

  Van de
  ------
  FunctionCode 2810 bi dung cho hai quyen khac nhau:
    * SQL/37 : 2810 = PublicForm.View   (Personal/Own)
    * SQL/53 : 2810 = Payroll.Approve   (UPDATE de len dong tren)
  Sau SQL/53 dong 2810 la "Payroll.Approve", trong khi API /api/public-forms goi
  HasPersonalAsync(SecurityFunctionCodes.PublicFormView) => 403 cho nguoi dung thuong.
  Ngoai ra Security Registry (ReconcileAsync) khong thay key Payroll.Approve duoc khai bao trong code
  nen dua dong 2810 vao PendingRetirement / IsActive = 0.

  Cach sua (code + SQL)
  ---------------------
  * SecurityFunctionCodes.PublicFormView  : 2810 -> 2819.
  * ApprovalSecurityFunctionCodes.PayrollApprove (2810) duoc khai bao [SecurityFunctionDefinition]
    de reconcile nhan ra "Payroll.Approve".
  * Script nay: tao PublicForm.View o ma 2819, chuyen grant Personal/Own tu 2810 sang 2819,
    go grant Personal/Own sai tren Payroll.Approve, tra lai trang thai cho cac dong bi reconcile retire.

  Idempotent: chay nhieu lan an toan. Toan bo nam trong 1 transaction; loi => rollback.
  Hai result set cuoi la bao cao de kiem tra.
*/
BEGIN TRY
    BEGIN TRAN;

    /* 0. Guards: khong tu y ghi de neu key dang thuoc ma khac. */
    IF EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionKey = N'PublicForm.View' AND FunctionCode NOT IN (2810, 2819))
        THROW 51470, N'FunctionKey PublicForm.View dang thuoc FunctionCode khac 2810/2819. Doi key do truoc khi chay script nay.', 1;
    IF EXISTS (SELECT 1 FROM dbo.F03Functions WHERE FunctionKey = N'Payroll.Approve' AND FunctionCode <> 2810)
        THROW 51471, N'FunctionKey Payroll.Approve dang thuoc FunctionCode khac 2810. Xu ly truoc khi chay script nay.', 1;

    /* 1. Truong hop dao nguoc (SQL/46 chay lai gan key PublicForm.View cho 2810): tra 2810 ve Payroll.Approve. */
    UPDATE dbo.F03Functions
    SET FunctionKey = N'Payroll.Approve',
        FunctionName = N'Phê duyệt bảng công/bảng lương',
        ModuleCode = N'Payroll',
        ActionCode = N'Approve',
        ScopeCode = N'Department'
    WHERE FunctionCode = 2810 AND FunctionKey = N'PublicForm.View';

    /* 2. Tao / sua PublicForm.View tai ma rieng 2819. */
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
        SET FunctionKey = N'PublicForm.View',
            FunctionName = N'PublicForm.View',
            Detail = N'Xem biểu mẫu được chỉ định cho bản thân',
            ModuleCode = N'PublicForm', ActionCode = N'View', ScopeCode = N'Own',
            IsActive = 1, LifecycleStatus = N'Active'
        WHERE FunctionCode = 2819;
    END;

    /* 3. Grants: role 3,4,5 nhan Personal/Own tren 2819 (dung y dinh cua SQL/37). */
    INSERT dbo.F03RoleFunctions (IdRole, IdFunction, ScopeCode, AccessMode)
    SELECT r.Id, f.Id, N'Own', N'Personal'
    FROM dbo.F03Roles r
    CROSS JOIN dbo.F03Functions f
    WHERE r.RoleCode IN (3, 4, 5) AND f.FunctionCode = 2819
      AND NOT EXISTS (SELECT 1 FROM dbo.F03RoleFunctions x WHERE x.IdRole = r.Id AND x.IdFunction = f.Id);

    /* 3b. Role khac tung duoc cap Personal/Own tren 2810 (grant cua PublicForm.View cu) giu quyen do tren 2819. */
    INSERT dbo.F03RoleFunctions (IdRole, IdFunction, ScopeCode, AccessMode)
    SELECT DISTINCT rf.IdRole, n.Id, N'Own', N'Personal'
    FROM dbo.F03RoleFunctions rf
    JOIN dbo.F03Functions o ON o.Id = rf.IdFunction AND o.FunctionCode = 2810
    CROSS JOIN (SELECT Id FROM dbo.F03Functions WHERE FunctionCode = 2819) n
    WHERE rf.IsActive = 1
      AND (rf.AccessMode = N'Personal' OR (rf.AccessMode IS NULL AND rf.ScopeCode = N'Own'))
      AND NOT EXISTS (SELECT 1 FROM dbo.F03RoleFunctions x WHERE x.IdRole = rf.IdRole AND x.IdFunction = n.Id);

    /* 4. Payroll.Approve la quyen Department/Management. Grant Personal/Own tren no la ban sao sai cua PublicForm.View
          (nguoi dung thuong co the vo tinh co quyen duyet bang cong/luong). Liet ke truoc roi vo hieu hoa. */
    SELECT N'Payroll.Approve - grant Personal/Own se bi vo hieu hoa' AS Action, r.RoleCode, rf.AccessMode, rf.ScopeCode
    FROM dbo.F03RoleFunctions rf
    JOIN dbo.F03Functions f ON f.Id = rf.IdFunction AND f.FunctionCode = 2810
    JOIN dbo.F03Roles r ON r.Id = rf.IdRole
    WHERE rf.IsActive = 1
      AND (rf.AccessMode = N'Personal' OR (rf.AccessMode IS NULL AND rf.ScopeCode = N'Own'));

    UPDATE rf SET rf.IsActive = 0, rf.ModifiedAt = GETDATE()
    FROM dbo.F03RoleFunctions rf
    JOIN dbo.F03Functions f ON f.Id = rf.IdFunction AND f.FunctionCode = 2810
    WHERE rf.IsActive = 1
      AND (rf.AccessMode = N'Personal' OR (rf.AccessMode IS NULL AND rf.ScopeCode = N'Own'));

    /* 5. Tra lai cac dong bi ReconcileAsync dua vao PendingRetirement (khong dong vao Retired/Replaced do admin chu dong). */
    UPDATE dbo.F03Functions
    SET IsActive = 1, LifecycleStatus = N'Active'
    WHERE FunctionCode IN (2807, 2808, 2809, 2811, 2812, 2813, 2814, 2815, 2816, 2817, 2818)
      AND FunctionKey LIKE N'PublicForm.%'
      AND LifecycleStatus = N'PendingRetirement';

    UPDATE dbo.F03Functions
    SET IsActive = 1, LifecycleStatus = N'Active'
    WHERE FunctionCode = 2810 AND FunctionKey = N'Payroll.Approve'
      AND LifecycleStatus = N'PendingRetirement';

    /* 6. Registry: bo trang thai Conflict do hai key cung tro vao 2810. */
    IF OBJECT_ID(N'dbo.F03SecurityFunctionRegistry', N'U') IS NOT NULL
    BEGIN
        UPDATE dbo.F03SecurityFunctionRegistry
        SET FunctionCode = 2819, LifecycleStatus = N'Active', ResolvedAt = GETDATE()
        WHERE FunctionKey = N'PublicForm.View';

        UPDATE dbo.F03SecurityFunctionRegistry
        SET FunctionCode = 2810, LifecycleStatus = N'Active', ResolvedAt = GETDATE()
        WHERE FunctionKey = N'Payroll.Approve';
    END;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    THROW;
END CATCH;
GO

/* Bao cao kiem tra */
SELECT FunctionCode, FunctionKey, FunctionName, ScopeCode, IsActive, LifecycleStatus
FROM dbo.F03Functions
WHERE FunctionCode BETWEEN 2807 AND 2819
ORDER BY FunctionCode;

SELECT r.RoleCode, f.FunctionCode, f.FunctionKey, rf.AccessMode, rf.ScopeCode, rf.IsActive AS RoleFunctionActive
FROM dbo.F03RoleFunctions rf
JOIN dbo.F03Roles r ON r.Id = rf.IdRole
JOIN dbo.F03Functions f ON f.Id = rf.IdFunction
WHERE f.FunctionCode IN (2810, 2819)
ORDER BY f.FunctionCode, r.RoleCode;

/* Grant theo tung nguoi dung (F03UserFunctions) tren 2810: KHONG tu dong chuyen vi khong the biet do la PublicForm.View hay Payroll.Approve.
   Neu co dong nao la quyen dang ky bieu mau, cap lai quyen PublicForm.View (2819) trong Admin > Security. */
SELECT uf.IdUser, f.FunctionCode, f.FunctionKey, uf.IsActive
FROM dbo.F03UserFunctions uf
JOIN dbo.F03Functions f ON f.Id = uf.IdFunction
WHERE f.FunctionCode = 2810 AND uf.IsActive = 1;
GO
