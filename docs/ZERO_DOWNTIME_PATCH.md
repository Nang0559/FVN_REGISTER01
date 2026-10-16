# Cập nhật hệ thống không cần copy đè thủ công

Cơ chế này cho phép SuperAdmin upload một gói `.zip` đã ký số tại `/admin/system-update`. API chỉ nhận và xác thực gói rồi đặt vào `patch-inbox`; một Windows Scheduled Task chạy quyền cao sẽ xác thực lại, dựng release mới, chuyển IIS sang release mới, health-check và rollback nếu lỗi.

> Đây là **online deployment không cần tự tay tắt IIS/copy đè**. Updater vẫn restart app pool khi switch release để tránh chạy đồng thời các background worker của FVN_REGISTER. Vì vậy có thể có một khoảng gián đoạn ngắn trong lúc app pool khởi động lại; đây không phải active-active zero-downtime tuyệt đối.

## Một lần cài đặt trên server

1. Tạo cặp RSA 3072-bit trên máy build:
   `.\scripts\deploy\New-PatchPackage.ps1 -GenerateKeys -KeyDir C:\secure\fvn-keys`
2. Chạy `Install-Updater.ps1` với quyền Administrator để chuẩn bị thư mục release/inbox/status và public key.
3. Đăng ký `Apply-Patch.ps1` thành Scheduled Task chạy mỗi phút bằng SYSTEM hoặc service account có quyền cần thiết.
4. Cho IIS App Pool của API quyền Modify trên `patch-inbox`, Read trên `patch-status` và `keys`; updater có quyền quản trị IIS.
5. Trong `appsettings.Production.json` của API cấu hình:
   `SystemUpdate:Enabled=true`, `InboxPath=<Root>\patch-inbox`, `StatusPath=<Root>\patch-status`, `PublicKeyPath=<Root>\keys\public.xml`, `MaxPackageMb=300`.
6. `web.config` đã giới hạn riêng endpoint upload ở 400 MB.

## Mỗi lần phát hành

Máy build:
`.\scripts\deploy\New-PatchPackage.ps1 -Version 2026.10.02.1 -PrivateKeyFile C:\secure\fvn-keys\private.xml`

Upload `patch-2026.10.02.1.zip` tại `/admin/system-update`. Updater sẽ xử lý tối đa một gói mỗi lượt.

## Bảo mật

- Chỉ SuperAdmin được upload qua API.
- Server chỉ giữ public key; private key chỉ ở máy phát hành và không commit vào Git.
- Gói phải có `manifest.json` + `manifest.sig`; manifest chứa SHA-256 của từng file.
- Updater kiểm tra path traversal, hash, chữ ký và component trước khi chạm IIS.
- SQL được chạy trước khi switch code; SQL phát hành phải tương thích ngược.
- `appsettings.Production.json` và `web.config` trên server được giữ nguyên khi tạo release.

## Rollback

`.\scripts\deploy\Rollback-Release.ps1 -List`

`.\scripts\deploy\Rollback-Release.ps1 -Component api -Version 2026.10.01`

Lịch sử nằm ở `<Root>\patch-status\history.json`; log updater ở `<Root>\updater\updater.log`.
