USE [FVN_REGISTER];
GO
SET NOCOUNT ON;

/*
  Mục đích:
  - Bảo đảm danh mục phát hiện chức năng không phụ thuộc vào F03Functions.
  - Nếu F03Functions bị xóa toàn bộ, Function Registry vẫn giữ các mã đã phát hiện để SuperAdmin đăng ký lại từ giao diện.
  - Không tự cấp quyền cho Role/User.
  - Không tự tạo lại F03Functions; việc đăng ký lại phải do SuperAdmin xác nhận trên giao diện.
*/

IF OBJECT_ID(N'dbo.F03SecurityFunctionRegistry', N'U') IS NULL
    THROW 51480, N'Danh mục phát hiện chức năng chưa tồn tại. Hãy chạy 46_SecurityFunctionRegistry.sql trước.', 1;

IF COL_LENGTH(N'dbo.F03SecurityFunctionRegistry', N'IsIgnored') IS NULL
    ALTER TABLE dbo.F03SecurityFunctionRegistry ADD IsIgnored bit NOT NULL CONSTRAINT DF_F03SecurityFunctionRegistry_IsIgnored_Recovery DEFAULT 0;

/*
  Discovery có thể tạo candidate chưa được đăng ký, trong đó FunctionCode = 0.
  Đây là trạng thái "chưa ánh xạ", không phải lỗi schema/deploy và không được phép
  làm dừng toàn bộ deployment. Giữ nguyên record để SuperAdmin có thể xử lý,
  nhưng đánh dấu IsIgnored để nó không được coi là capability hợp lệ.
*/
IF EXISTS
(
    SELECT 1
    FROM dbo.F03SecurityFunctionRegistry
    WHERE FunctionCode <= 0
      AND ISNULL(IsIgnored, 0) = 0
)
BEGIN
    UPDATE dbo.F03SecurityFunctionRegistry
    SET IsIgnored = 1,
        LifecycleStatus = CASE
            WHEN LifecycleStatus IN (N'Retired', N'Replaced') THEN LifecycleStatus
            ELSE N'Unmapped'
        END,
        LastSeenAt = ISNULL(LastSeenAt, CreatedAt)
    WHERE FunctionCode <= 0
      AND ISNULL(IsIgnored, 0) = 0;

    PRINT N'WARNING: Function Registry contains unregistered discovery candidates (FunctionCode <= 0); they were retained and marked IsIgnored=1.';
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.F03SecurityFunctionRegistry
    WHERE FunctionCode <= 0
      AND ISNULL(IsIgnored, 0) = 0
)
    THROW 51481, N'Danh mục phát hiện chức năng vẫn còn mã chức năng không hợp lệ chưa được xử lý.', 1;

IF EXISTS
(
    SELECT 1
    FROM dbo.F03SecurityFunctionRegistry
    WHERE FunctionCode > 0
    GROUP BY FunctionKey
    HAVING COUNT(*) > 1
)
    THROW 51482, N'Danh mục phát hiện chức năng có mã định danh trùng.', 1;

PRINT N'=== SECURITY FUNCTION REGISTRY RECOVERY CONTRACT OK ===';
GO
