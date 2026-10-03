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


## 3. Equipment-first credential lifecycle

Credential lifecycle được khởi tạo từ Equipment Asset:

Equipment Asset → Endpoint Agent eligible → Provision → one-time secret → Agent installation → Inventory.

Secret chỉ trả về một lần cho người có capability provision/rotate. Server lưu hash; credential active được gắn với Endpoint Device/DeviceKey. Credential không cấp user permission.

### LANSCOPE bulk enrollment

The LANSCOPE flow is separate from manual Equipment credential provisioning. A Golden Package never contains a long-lived endpoint credential. Each deployment target receives a one-time bootstrap token; the Agent exchanges it over HTTPS for a newly issued DeviceKey and endpoint credential. The plaintext credential is returned only by the enrollment operation, then protected locally with DPAPI LocalMachine. The bootstrap file is deleted after successful enrollment.

### Rotate/Revoke
- Rotate thay credential active và phát secret mới.
- Revoke vô hiệu hóa credential; lịch sử vẫn giữ.
- Không tạo credential dùng chung cho nhiều máy.
- Không cho người dùng tự nhập DeviceKey để mở rộng scope.

### Audit
Provision/rotate/revoke phải ghi actor, endpoint/equipment, thời điểm và kết quả; tuyệt đối không ghi plaintext secret.
