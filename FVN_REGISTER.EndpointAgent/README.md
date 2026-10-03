# FVN Register Endpoint Agent

## Credential provisioning

The API returns the plaintext endpoint API key only at provisioning/rotation time. The endpoint must protect that secret locally because `WindowsSecretStore` uses DPAPI `DataProtectionScope.LocalMachine`.

Preferred installation flow:

1. In **Sổ thiết bị → Endpoint Agent**, select the Equipment and choose **Cấp credential**.
2. Copy the returned secret immediately; it is shown only once.
3. On the target Windows machine, run the installer without passing the secret on the command line:

```powershell
.\install-agent.ps1 `
  -InstallPath 'C:\Program Files\FVN Register\EndpointAgent' `
  -ApiBaseUrl 'https://fvn-api.example' `
  -DeviceKey '<DeviceKey returned for the Equipment>'
```

The installer securely prompts for the one-time secret and protects it locally with DPAPI before writing `appsettings.json`.

The script pipes the plaintext key to:

```powershell
FVN_REGISTER.EndpointAgent.exe --protect-secret-stdin
```

The command prints only the DPAPI-protected Base64 blob. The blob is machine-bound and must not be copied to another Windows machine.

If a protected blob was already generated on the same target machine, `-ApiKeyProtected` can be used instead of `-ApiKey`.

## Credential lifetime

Credentials are issued for one year. The agent reads the expiry header returned by inventory ingestion and rotates 30 days before expiry. After a single HTTP 401 it attempts the same self-rotation endpoint and retries the inventory once.

If the current key is already expired or revoked, self-rotation is intentionally rejected. IT/SuperAdmin must provision a new credential in Security Center.

## Hardware identity

- Serial: `Win32_BIOS.SerialNumber`
- Hardware UUID: `Win32_ComputerSystemProduct.UUID`
- Manufacturer/model is never used as a UUID.
- Empty/placeholder UUIDs are reported as unknown rather than converted into a synthetic identity.

The server never accepts EmployeeCode or EquipmentAssetId from the trusted inventory payload; those are FVN-side administrative facts.

## Inventory rules

- Machine-wide uninstall registry plus loaded user hives are collected.
- Windows update/hotfix/system-component entries are filtered.
- Software identity keeps name + publisher + version + architecture.
- Service `BinaryPathHash` is SHA-256 of the executable file content.
- Defender and Windows Security Center antivirus providers are collected. Non-Defender providers without detailed protection flags are reported as detected/unknown, not falsely disabled.
- Server-side `LastInventoryHash` prevents rewriting unchanged inventory every 30 minutes.
