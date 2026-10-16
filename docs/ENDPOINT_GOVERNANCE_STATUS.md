# Endpoint Governance – implementation status

Branch: feature/i18n-vi-ja

## LANSCOPE bulk flow

LANSCOPE CSV -> Import-LanscopeTargets.ps1 -> FVN deployment + target records -> one bootstrap JSON / target -> LANSCOPE distribution -> install-agent.ps1 (LocalSystem) -> HTTPS enrollment -> DeviceKey + credential -> inventory.

## Operational rules

- LanscopeDeployment:PublicBaseUrl bắt buộc cấu hình HTTPS; API không suy luận public URL từ Host/forwarded headers.
- LanscopeDeployment:EnrollmentTokenMinutes mặc định 60 phút và có thể cấu hình 5–1440 phút.
- TargetId trùng trong batch hoặc đã tồn tại sẽ trả 409; không còn continue âm thầm.
- CreateTargets dùng transaction cho toàn bộ batch và chỉ SaveChanges theo batch, không SaveChanges từng target.
- Token reissue chỉ áp dụng target Pending chưa enroll; target đã enroll không được cấp bootstrap mới.
- Target không có serial hoặc serial không khớp sẽ vào PendingReview; không mặc định Verified.
- Inventory không ghi đè metadata LANSCOPE hiện có bằng NULL/empty.
- Agent lấy ClientId từ bootstrap và lưu local.
- Agent lấy WindowsUser từ Win32_ComputerSystem.UserName; không dùng identity của LocalSystem service.
- Installer dừng service trước khi copy EXE, bật sc failureflag, chờ enrollment thật sự và trả non-zero nếu enrollment không hoàn tất.
- Bootstrap/config được ACL cho SYSTEM + Administrators và bootstrap source được xóa sau enrollment thành công.
- Service binary hash cache theo mtime + size.

## SQL/deployment

LANSCOPE schema nằm ở SQL/69_Endpoint_LanscopeDeployment.sql. Bộ deploy hiện tại phải bao gồm script 69; không dùng checklist cũ chỉ tới 54–56.

## Bulk preparation

scripts/lanscope/Import-LanscopeTargets.ps1 nhận CSV LANSCOPE với các trường ClientId, ComputerName, IP, MAC, SerialNumber, WindowsUser, Domain, OU, Group, OS, Manufacturer, Model; gọi API deployment/targets theo batch và xuất bootstrap JSON + manifest.

## Credential security

Một endpoint chỉ có một credential current. Khi rotate, credential cũ có grace window ngắn để tránh mất credential trong lúc agent ghi secret mới; secret plaintext không được ghi log/database.

## Pilot gate

Pilot 2–3 PC trước mass rollout và kiểm tra bootstrap replay, token expiry + reissue, missing/mismatched serial -> PendingReview, ClientId/OU/Group không mất sau inventory đầu tiên, WindowsUser không còn là SYSTEM khi có console user, upgrade khi service đang chạy, bootstrap source cleanup, service failure restart, revoke/expiry chặn inventory, PublicBaseUrl HTTPS, và distinct DeviceKey/credential cho từng endpoint.
