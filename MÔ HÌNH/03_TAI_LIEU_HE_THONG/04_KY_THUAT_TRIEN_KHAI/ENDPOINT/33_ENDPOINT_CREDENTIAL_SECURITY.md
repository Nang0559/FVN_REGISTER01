# Endpoint Credential Security

## Mô hình

Mỗi Windows endpoint có một credential riêng. Server lưu SHA-256 hash, không lưu plaintext secret.

```text
Superadmin/IT
  -> provision PC-001
  -> secret chỉ hiển thị một lần
  -> Agent cấu hình secret
  -> API hash(secret) -> lookup credential -> DeviceKey
```

## Rules

- Một endpoint chỉ có tối đa một credential active.
- Rotate tạo secret mới và revoke secret cũ.
- Revoke làm endpoint không thể gửi inventory.
- Credential không quyết định quyền người dùng; nó chỉ xác thực machine identity.
- DeviceKey được lấy từ credential record, không tin DeviceKey trong payload.
- Inventory endpoint phải HTTPS.
- Rate-limit theo credential/device.
- Audit provision/rotate/revoke.
- Không dùng shared secret cho toàn bộ máy.
- Không cho agent thực thi command từ server.
