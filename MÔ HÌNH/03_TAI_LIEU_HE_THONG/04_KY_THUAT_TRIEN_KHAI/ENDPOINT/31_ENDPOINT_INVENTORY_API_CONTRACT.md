# Endpoint Inventory API Contract

## Agent authentication

Agent uses a dedicated device credential. The server must bind the credential to `DeviceKey`; the client cannot submit inventory for another device.

## POST /api/security/endpoints/heartbeat

Payload: DeviceKey, ComputerName, SerialNumber, OsName, OsVersion, EmployeeCode, EquipmentAssetId, AgentVersion.

Purpose: upsert endpoint and update LastSeenUtc.

## POST /api/security/endpoints/inventory

Payload: DeviceKey, computer metadata, Software[], Services[].

Rules:
1. Authenticate agent.
2. Resolve DeviceKey from credential.
3. Upsert endpoint.
4. Replace current software/service snapshot transactionally, or upsert with collection timestamp and retire entries missing from the latest complete snapshot.
5. Never execute commands received in inventory payload.
6. Run compliance after a complete inventory snapshot.
7. Create/update alerts idempotently.

## Policy / compliance

`GET /api/security/endpoints/{deviceKey}/policy` returns only effective policies applicable to the endpoint scope and no arbitrary commands.

`GET /api/security/endpoints/{deviceKey}/compliance` returns current compliance summary and open alerts.

## Security

HTTPS only; credential rotation/revocation; replay protection where supported; payload size limits; rate limiting; audit of registration, credential rotation, policy changes and exception approval. EmployeeCode and EquipmentAssetId are server-resolved when possible; never trust client scope claims.
