# FVN Register Endpoint Agent

## LANSCOPE Cat bulk rollout

LANSCOPE là distribution/execution engine; FVN REGISTER là security/control plane.

Golden Package dùng chung cho tất cả Windows client và không chứa DeviceKey, API key hoặc bootstrap token dùng lại. Mỗi target nhận một bootstrap JSON riêng.

Installer chạy dưới LocalSystem, dừng service trước khi nâng cấp, thay binary sau khi service đã dừng, bật sc failureflag, chờ enrollment thành công và trả exit code khác 0 nếu enrollment thất bại. Bootstrap/config được ACL cho SYSTEM + Administrators; bootstrap được xóa sau khi enrollment thành công.

Bootstrap token có TTL cấu hình server-side bằng LanscopeDeployment:EnrollmentTokenMinutes (mặc định 60 phút), chỉ dùng một lần. Target đang Pending có thể được cấp lại token bằng endpoint reissue mà không đổi TargetKey.

Sau enrollment, server cấp DeviceKey + credential riêng; Agent lưu DeviceKey và LANSCOPE ClientId, bảo vệ API key bằng DPAPI LocalMachine, xóa bootstrap và gửi inventory định kỳ.

## Inventory identity

- LanscopeClientId lấy từ bootstrap, không phụ thuộc biến môi trường.
- Server dùng COALESCE để không ghi đè metadata LANSCOPE bằng NULL/empty từ agent.
- WindowsUser lấy từ Win32_ComputerSystem.UserName, thay vì Environment.UserName của LocalSystem.
- Serial: Win32_BIOS.SerialNumber.
- Hardware UUID: Win32_ComputerSystemProduct.UUID.
- ComputerName chỉ là thuộc tính; DeviceKey mới là technical identity.

## Credential lifecycle

Credential được cấp/rotate/revoke theo endpoint. Rotate giữ credential cũ trong grace window ngắn để agent có thể ghi secret mới mà không làm mất đường xác thực nếu local persistence gặp lỗi. Chỉ credential current mới được xem là active.

## Inventory collector

Service binary hash vẫn là SHA-256 nhưng được cache theo file path + size + last-write-time; file không thay đổi sẽ không bị hash lại ở mỗi chu kỳ.

## Bulk preparation

Dùng scripts/lanscope/Import-LanscopeTargets.ps1 để đọc CSV LANSCOPE, tạo deployment, gửi target theo batch, nhận bootstrap JSON cho từng target và xuất manifest.csv. LANSCOPE sau đó phân phối Golden Package + bootstrap tương ứng và thực thi installer.

## Legacy/manual credential installation

Flow provision secret thủ công vẫn tồn tại cho vận hành ngoài LANSCOPE. Không dùng flow này để tạo Golden Package hàng loạt.
