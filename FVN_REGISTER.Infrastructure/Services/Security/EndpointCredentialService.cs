using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Contract.Dtos.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FVN_REGISTER.Infrastructure.Services.Security;

public sealed class EndpointCredentialService : IEndpointCredentialService
{
    private static readonly TimeSpan RotationGrace = TimeSpan.FromMinutes(10);
    private readonly FVNWEBAPPContext _db;
    public EndpointCredentialService(FVNWEBAPPContext db) => _db = db;

    public async Task<EndpointCredentialStatusDto> GetStatusAsync(string deviceKey, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeDeviceKey(deviceKey);
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TOP (1) d.Id, c.ExpiresAtUtc FROM dbo.F03EndpointDevices d OUTER APPLY (SELECT TOP (1) ExpiresAtUtc FROM dbo.F03EndpointCredentials WHERE EndpointDeviceId=d.Id AND RevokedAtUtc IS NULL AND (GraceExpiresAtUtc IS NULL OR GraceExpiresAtUtc>SYSUTCDATETIME()) ORDER BY CASE WHEN GraceExpiresAtUtc IS NULL THEN 0 ELSE 1 END, CreatedAtUtc DESC) c WHERE d.DeviceKey=@deviceKey;";
        AddParameter(command, "@deviceKey", normalized);
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new EndpointCredentialStatusDto(normalized, false, false, null);
        var expires = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
        return new EndpointCredentialStatusDto(normalized, true, expires.HasValue && expires.Value > DateTime.UtcNow, expires.HasValue ? new DateTimeOffset(expires.Value, TimeSpan.Zero) : null);
    }

    public async Task<EndpointCredentialProvisionResult> ProvisionAsync(EndpointCredentialProvisionDto request, int actorUserId, CancellationToken cancellationToken = default)
    {
        var deviceKey = NormalizeDeviceKey(request.DeviceKey);
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var dbTransaction = tx.GetDbTransaction();
        var endpointId = await FindEndpointIdAsync(deviceKey, dbTransaction, cancellationToken);
        if (endpointId == null)
        {
            if (request.EquipmentAssetId.HasValue && !await _db.EquipmentAssets.AsNoTracking().AnyAsync(x => x.Id == request.EquipmentAssetId.Value, cancellationToken))
                throw new ArgumentException("EquipmentAssetId không tồn tại.", nameof(request));
            var connection = _db.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.Transaction = dbTransaction;
            command.CommandText = "INSERT INTO dbo.F03EndpointDevices (DeviceKey,ComputerName,EquipmentAssetId,Status,Source,CreatedAt,UpdatedAt) OUTPUT INSERTED.Id VALUES (@deviceKey,@computerName,@equipmentAssetId,'PendingRegistration','FVNAdmin',SYSUTCDATETIME(),SYSUTCDATETIME());";
            AddParameter(command, "@deviceKey", deviceKey); AddParameter(command, "@computerName", request.ComputerName); AddParameter(command, "@equipmentAssetId", request.EquipmentAssetId);
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
            endpointId = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        }
        else if (request.EquipmentAssetId.HasValue)
        {
            if (!await _db.EquipmentAssets.AsNoTracking().AnyAsync(x => x.Id == request.EquipmentAssetId.Value, cancellationToken))
                throw new ArgumentException("EquipmentAssetId không tồn tại.", nameof(request));
            var device = await _db.EndpointDevices.FirstAsync(x => x.Id == endpointId.Value, cancellationToken);
            device.EquipmentAssetId = request.EquipmentAssetId;
            if (!string.IsNullOrWhiteSpace(request.ComputerName)) device.ComputerName = request.ComputerName.Trim();
            device.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        var result = await RotateForEndpointAsync(endpointId.Value, deviceKey, actorUserId, "PROVISION", dbTransaction, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await WriteAuditAsync(actorUserId, "EndpointCredential.Provision", $"Provision credential for endpoint {deviceKey}.", cancellationToken);
        return result;
    }

    public async Task<EndpointCredentialProvisionResult?> RotateWithCurrentApiKeyAsync(string currentApiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentApiKey)) return null;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(currentApiKey));
        var connection = _db.Database.GetDbConnection();
        await using var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT TOP (1) d.Id,d.DeviceKey FROM dbo.F03EndpointCredentials c INNER JOIN dbo.F03EndpointDevices d ON d.Id=c.EndpointDeviceId WHERE c.SecretHash=@hash AND c.RevokedAtUtc IS NULL AND (c.GraceExpiresAtUtc IS NULL OR c.GraceExpiresAtUtc>SYSUTCDATETIME()) AND c.ExpiresAtUtc>SYSUTCDATETIME() ORDER BY CASE WHEN c.GraceExpiresAtUtc IS NULL THEN 0 ELSE 1 END, c.CreatedAtUtc DESC;";
        AddParameter(lookup, "@hash", hash);
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var endpointId = reader.GetInt64(0); var deviceKey = reader.GetString(1);
        await reader.DisposeAsync();
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var dbTransaction = tx.GetDbTransaction();
        var result = await RotateForEndpointAsync(endpointId, deviceKey, null, "AUTO_ROTATE", dbTransaction, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await WriteAuditAsync(null, "EndpointCredential.Rotate", $"Automatic credential rotation for endpoint {deviceKey}.", cancellationToken);
        return result;
    }

    public async Task<bool> RevokeAsync(string deviceKey, int actorUserId, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeDeviceKey(deviceKey);
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var dbTransaction = tx.GetDbTransaction();
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = dbTransaction;
        command.CommandText = "UPDATE c SET RevokedAtUtc=SYSUTCDATETIME(),RevokedBy=@actor,GraceExpiresAtUtc=NULL FROM dbo.F03EndpointCredentials c INNER JOIN dbo.F03EndpointDevices d ON d.Id=c.EndpointDeviceId WHERE d.DeviceKey=@deviceKey AND c.RevokedAtUtc IS NULL;";
        AddParameter(command, "@deviceKey", normalized); AddParameter(command, "@actor", actorUserId);
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0) { await tx.RollbackAsync(cancellationToken); return false; }
        await ExecuteAuditRowAsync(normalized, null, "REVOKE", actorUserId, "Credential revoked by Security Center.", dbTransaction, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await WriteAuditAsync(actorUserId, "EndpointCredential.Revoke", $"Revoke credential for endpoint {normalized}.", cancellationToken);
        return true;
    }

    private async Task<EndpointCredentialProvisionResult> RotateForEndpointAsync(long endpointId, string deviceKey, int? actorUserId, string actionCode, DbTransaction dbTransaction, CancellationToken cancellationToken)
    {
        var secret = GenerateSecret(); var hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret)); var expires = DateTime.UtcNow.AddYears(1);
        var connection = _db.Database.GetDbConnection();
        await using var grace = connection.CreateCommand();
        grace.Transaction = dbTransaction;
        grace.CommandText = "UPDATE dbo.F03EndpointCredentials SET GraceExpiresAtUtc=DATEADD(MINUTE,10,SYSUTCDATETIME()) WHERE EndpointDeviceId=@endpointId AND RevokedAtUtc IS NULL AND GraceExpiresAtUtc IS NULL;";
        AddParameter(grace, "@endpointId", endpointId);
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await grace.ExecuteNonQueryAsync(cancellationToken);

        await using var insert = connection.CreateCommand();
        insert.Transaction = dbTransaction;
        insert.CommandText = "INSERT INTO dbo.F03EndpointCredentials (EndpointDeviceId,SecretHash,CreatedAtUtc,ExpiresAtUtc,CreatedBy,RevokedBy,GraceExpiresAtUtc) OUTPUT INSERTED.Id VALUES (@endpointId,@secretHash,SYSUTCDATETIME(),@expiresAtUtc,@createdBy,NULL,NULL);";
        AddParameter(insert, "@endpointId", endpointId); AddParameter(insert, "@secretHash", hash); AddParameter(insert, "@expiresAtUtc", expires); AddParameter(insert, "@createdBy", actorUserId);
        var credentialId = Convert.ToInt64(await insert.ExecuteScalarAsync(cancellationToken));
        await ExecuteAuditRowAsync(deviceKey, credentialId, actionCode, actorUserId, actionCode == "AUTO_ROTATE" ? "Automatic agent rotation with grace period." : "Credential provisioned/rotated by Security Center.", dbTransaction, cancellationToken);
        return new EndpointCredentialProvisionResult(deviceKey, secret, new DateTimeOffset(expires, TimeSpan.Zero));
    }

    private async Task ExecuteAuditRowAsync(string deviceKey, long? credentialId, string actionCode, int? actorUserId, string detail, DbTransaction dbTransaction, CancellationToken ct)
    {
        var connection = _db.Database.GetDbConnection(); await using var command = connection.CreateCommand(); command.Transaction = dbTransaction;
        command.CommandText = "INSERT INTO dbo.F03EndpointCredentialAudit (EndpointCredentialId,EndpointDeviceId,ActionCode,ActorUserId,Detail) SELECT @credentialId,Id,@actionCode,@actorUserId,@detail FROM dbo.F03EndpointDevices WHERE DeviceKey=@deviceKey;";
        AddParameter(command, "@credentialId", credentialId); AddParameter(command, "@deviceKey", deviceKey); AddParameter(command, "@actionCode", actionCode); AddParameter(command, "@actorUserId", actorUserId); AddParameter(command, "@detail", detail);
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct); await command.ExecuteNonQueryAsync(ct);
    }

    private async Task WriteAuditAsync(int? actorUserId, string action, string description, CancellationToken ct)
    {
        var connection = _db.Database.GetDbConnection(); await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO dbo.F03AuditLogs (Action,CreatedAt,CreatedBy,Description,IsActive,LastModifiedSource,ModifiedAt,ModifiedBy,UserName) VALUES (@action,SYSUTCDATETIME(),@createdBy,@description,1,'EndpointCredentialService',SYSUTCDATETIME(),@modifiedBy,@userName);";
        AddParameter(command, "@action", action); AddParameter(command, "@createdBy", actorUserId); AddParameter(command, "@modifiedBy", actorUserId); AddParameter(command, "@description", description); AddParameter(command, "@userName", actorUserId.HasValue ? $"User:{actorUserId.Value}" : "SYSTEM");
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct); await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<long?> FindEndpointIdAsync(string deviceKey, DbTransaction dbTransaction, CancellationToken ct)
    {
        var connection = _db.Database.GetDbConnection(); await using var command = connection.CreateCommand(); command.Transaction = dbTransaction; command.CommandText = "SELECT TOP (1) Id FROM dbo.F03EndpointDevices WHERE DeviceKey=@deviceKey;"; AddParameter(command, "@deviceKey", deviceKey);
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct); var value = await command.ExecuteScalarAsync(ct); return value == null || value == DBNull.Value ? null : Convert.ToInt64(value);
    }

    private static string GenerateSecret() { Span<byte> bytes = stackalloc byte[48]; RandomNumberGenerator.Fill(bytes); return Convert.ToBase64String(bytes).Replace("+", "-", StringComparison.Ordinal).Replace("/", "_", StringComparison.Ordinal).TrimEnd('='); }
    private static string NormalizeDeviceKey(string value) { var normalized = value?.Trim() ?? string.Empty; if (normalized.Length is < 3 or > 100) throw new ArgumentException("DeviceKey phải có từ 3 đến 100 ký tự.", nameof(value)); return normalized; }
    private static void AddParameter(DbCommand command, string name, object? value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter); }
}
