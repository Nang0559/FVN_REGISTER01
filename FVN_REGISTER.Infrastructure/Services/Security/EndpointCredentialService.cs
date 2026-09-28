using System.Security.Cryptography;
using System.Text;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Core.Entities.Security;
using FVN_REGISTER.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Security;

public sealed class EndpointCredentialService : IEndpointCredentialService
{
    private readonly FVNWEBAPPContext _db;
    public EndpointCredentialService(FVNWEBAPPContext db) => _db = db;

    public async Task<EndpointCredentialStatusDto> GetStatusAsync(string deviceKey, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeDeviceKey(deviceKey);
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1)
                d.Id,
                c.ExpiresAtUtc
            FROM dbo.F03EndpointDevices d
            OUTER APPLY (
                SELECT TOP (1) ExpiresAtUtc
                FROM dbo.F03EndpointCredentials
                WHERE EndpointDeviceId = d.Id
                  AND RevokedAtUtc IS NULL
                ORDER BY CreatedAtUtc DESC
            ) c
            WHERE d.DeviceKey = @deviceKey;
            """;
        AddParameter(command, "@deviceKey", normalized);
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new EndpointCredentialStatusDto(normalized, false, false, null);

        var expires = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
        var active = expires.HasValue && expires.Value > DateTime.UtcNow;
        return new EndpointCredentialStatusDto(normalized, true, active, expires.HasValue ? new DateTimeOffset(expires.Value, TimeSpan.Zero) : null);
    }

    public async Task<EndpointCredentialProvisionResult> ProvisionAsync(EndpointCredentialProvisionDto request, int actorUserId, CancellationToken cancellationToken = default)
    {
        var deviceKey = NormalizeDeviceKey(request.DeviceKey);
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var endpointId = await FindEndpointIdAsync(deviceKey, cancellationToken);
        if (endpointId == null)
        {
            if (request.EquipmentAssetId.HasValue && !await _db.EquipmentAssets.AsNoTracking().AnyAsync(x => x.Id == request.EquipmentAssetId.Value, cancellationToken))
                throw new ArgumentException("EquipmentAssetId không tồn tại.", nameof(request));
            var connection = _db.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO dbo.F03EndpointDevices (DeviceKey, ComputerName, EquipmentAssetId, Status, Source, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.Id
                VALUES (@deviceKey, @computerName, @equipmentAssetId, 'PendingRegistration', 'FVNAdmin', SYSUTCDATETIME(), SYSUTCDATETIME());
                """;
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

        var result = await RotateForEndpointAsync(endpointId.Value, deviceKey, actorUserId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<EndpointCredentialProvisionResult?> RotateWithCurrentApiKeyAsync(string currentApiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentApiKey)) return null;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(currentApiKey));
        var connection = _db.Database.GetDbConnection();
        await using var lookup = connection.CreateCommand();
        lookup.CommandText = """
            SELECT TOP (1) d.Id, d.DeviceKey
            FROM dbo.F03EndpointCredentials c
            INNER JOIN dbo.F03EndpointDevices d ON d.Id = c.EndpointDeviceId
            WHERE c.SecretHash = @hash
              AND c.RevokedAtUtc IS NULL
              AND c.ExpiresAtUtc > SYSUTCDATETIME();
            """;
        AddParameter(lookup, "@hash", hash);
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var endpointId = reader.GetInt64(0);
        var deviceKey = reader.GetString(1);
        await reader.DisposeAsync();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var result = await RotateForEndpointAsync(endpointId, deviceKey, 0, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<bool> RevokeAsync(string deviceKey, int actorUserId, CancellationToken cancellationToken = default)
    {
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE c SET RevokedAtUtc = SYSUTCDATETIME()
            FROM dbo.F03EndpointCredentials c
            INNER JOIN dbo.F03EndpointDevices d ON d.Id = c.EndpointDeviceId
            WHERE d.DeviceKey = @deviceKey AND c.RevokedAtUtc IS NULL;
            """;
        AddParameter(command, "@deviceKey", NormalizeDeviceKey(deviceKey));
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task<EndpointCredentialProvisionResult> RotateForEndpointAsync(long endpointId, string deviceKey, int actorUserId, CancellationToken cancellationToken)
    {
        var secret = GenerateSecret();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        var expires = DateTime.UtcNow.AddYears(1);
        var connection = _db.Database.GetDbConnection();
        await using var revoke = connection.CreateCommand();
        revoke.CommandText = "UPDATE dbo.F03EndpointCredentials SET RevokedAtUtc = SYSUTCDATETIME() WHERE EndpointDeviceId = @endpointId AND RevokedAtUtc IS NULL;";
        AddParameter(revoke, "@endpointId", endpointId);
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await revoke.ExecuteNonQueryAsync(cancellationToken);
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO dbo.F03EndpointCredentials (EndpointDeviceId, SecretHash, CreatedAtUtc, ExpiresAtUtc, CreatedBy)
            VALUES (@endpointId, @secretHash, SYSUTCDATETIME(), @expiresAtUtc, @createdBy);
            """;
        AddParameter(insert, "@endpointId", endpointId); AddParameter(insert, "@secretHash", hash); AddParameter(insert, "@expiresAtUtc", expires); AddParameter(insert, "@createdBy", actorUserId);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return new EndpointCredentialProvisionResult(deviceKey, secret, new DateTimeOffset(expires, TimeSpan.Zero));
    }

    private async Task<long?> FindEndpointIdAsync(string deviceKey, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TOP (1) Id FROM dbo.F03EndpointDevices WHERE DeviceKey = @deviceKey;";
        AddParameter(command, "@deviceKey", deviceKey);
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value == null || value == DBNull.Value ? null : Convert.ToInt64(value);
    }

    private static string GenerateSecret(){Span<byte> bytes=stackalloc byte[48];RandomNumberGenerator.Fill(bytes);return Convert.ToBase64String(bytes).Replace("+","-",StringComparison.Ordinal).Replace("/","_",StringComparison.Ordinal).TrimEnd('=');}
    private static string NormalizeDeviceKey(string value){var normalized=value?.Trim()??string.Empty;if(normalized.Length is <3 or >100)throw new ArgumentException("DeviceKey phải có từ 3 đến 100 ký tự.",nameof(value));return normalized;}
    private static void AddParameter(System.Data.Common.DbCommand command,string name,object? value){var parameter=command.CreateParameter();parameter.ParameterName=name;parameter.Value=value??DBNull.Value;command.Parameters.Add(parameter);}
}
