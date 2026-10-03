using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Core.Entities.Security;
using FVN_REGISTER.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FVN_REGISTER.Infrastructure.Services.Security;

public sealed class EndpointInventoryService : IEndpointInventoryService
{
    private readonly FVNWEBAPPContext _db;
    private readonly IEndpointComplianceService _compliance;
    public EndpointInventoryService(FVNWEBAPPContext db, IEndpointComplianceService compliance) { _db = db; _compliance = compliance; }

    public async Task<EndpointInventorySummaryDto> UpsertInventoryAsync(EndpointInventoryRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceKey)) throw new ArgumentException("DeviceKey is required.", nameof(request));
        if (request.DeviceKey.Length > 100) throw new ArgumentException("DeviceKey is too long.", nameof(request));
        if (request.Software.Count > 5000 || request.Services.Count > 2000 || request.Antivirus.Count > 100) throw new ArgumentException("Inventory payload exceeds the supported limit.");
        var now = DateTime.UtcNow; var deviceKey = request.DeviceKey.Trim(); var inventoryHash = ComputeInventoryHash(request);
        var device = await _db.EndpointDevices.SingleOrDefaultAsync(x => x.DeviceKey == deviceKey, cancellationToken);
        var isNew = device == null; var inventoryChanged = isNew || !Same(device!.LastInventoryHash, inventoryHash);
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var dbTransaction = tx.GetDbTransaction();
        try
        {
            if (device == null) { device = new F03EndpointDevice { DeviceKey = deviceKey, CreatedAt = now, IdentityStatus = "PendingReview" }; _db.EndpointDevices.Add(device); }
            var newComputerName = Trim(request.ComputerName, 255); var newSerialNumber = Trim(request.SerialNumber, 255); var newHardwareUuid = Trim(request.HardwareUuid, 255); var newAgentInstallationId = Trim(request.AgentInstallationId, 100);
            if (!isNew)
            {
                var nameChanged = !Same(device.ComputerName, newComputerName) && device.ComputerName != null && newComputerName != null;
                var hardwareChanged = !Same(device.HardwareUuid, newHardwareUuid) && device.HardwareUuid != null && newHardwareUuid != null;
                var agentReinstalled = !Same(device.AgentInstallationId, newAgentInstallationId) && device.AgentInstallationId != null && newAgentInstallationId != null;
                if (nameChanged || hardwareChanged || agentReinstalled)
                {
                    await AddIdentityHistoryAsync(device.Id, device.ComputerName, newComputerName, device.SerialNumber, newSerialNumber, device.HardwareUuid, newHardwareUuid, newAgentInstallationId, hardwareChanged ? "HardwareIdentityChanged" : agentReinstalled ? "AgentInstallationChanged" : "ComputerNameChanged", dbTransaction, cancellationToken);
                    device.IdentityStatus = hardwareChanged ? "PendingReview" : device.IdentityStatus == "PendingReview" ? "PendingReview" : "Verified";
                    if (nameChanged) await UpsertAlertAsync(device.Id, "DEVICE_NAME_CHANGED", "Medium", "Tên thiết bị Windows đã thay đổi.", $"Tên cũ: {device.ComputerName}; tên mới: {newComputerName}", null, dbTransaction, cancellationToken);
                    if (hardwareChanged) await UpsertAlertAsync(device.Id, "HARDWARE_IDENTITY_CHANGED", "High", "Định danh phần cứng của endpoint đã thay đổi.", $"Serial cũ: {device.SerialNumber}; Serial mới: {newSerialNumber}; Hardware UUID cũ: {device.HardwareUuid}; UUID mới: {newHardwareUuid}", null, dbTransaction, cancellationToken);
                }
            }
            device.ComputerName = newComputerName; device.SerialNumber = newSerialNumber; device.HardwareUuid = newHardwareUuid; device.AgentInstallationId = newAgentInstallationId;
            device.OsName = Trim(request.OsName, 255); device.OsVersion = Trim(request.OsVersion, 100); device.AgentVersion = Trim(request.AgentVersion, 50); device.LastSeenUtc = now; device.Status = "Online"; device.Source = "FVNAgent"; device.UpdatedAt = now;
            if (isNew && request.EquipmentAssetId.HasValue && await _db.EquipmentAssets.AsNoTracking().AnyAsync(x => x.Id == request.EquipmentAssetId.Value, cancellationToken)) device.EquipmentAssetId = request.EquipmentAssetId;
            if (isNew) device.IdentityStatus = "PendingReview"; device.LastInventoryHash = inventoryHash;
            await _db.SaveChangesAsync(cancellationToken);
            if (inventoryChanged)
            {
                var oldSoftware = await _db.EndpointSoftwareInventory.Where(x => x.EndpointDeviceId == device.Id).ToListAsync(cancellationToken); var oldServices = await _db.EndpointServiceInventory.Where(x => x.EndpointDeviceId == device.Id).ToListAsync(cancellationToken); var antivirusSet = _db.Set<F03EndpointAntivirusInventory>(); var oldAntivirus = await antivirusSet.Where(x => x.EndpointDeviceId == device.Id).ToListAsync(cancellationToken);
                _db.EndpointSoftwareInventory.RemoveRange(oldSoftware); _db.EndpointServiceInventory.RemoveRange(oldServices); antivirusSet.RemoveRange(oldAntivirus);
                foreach (var item in request.Software.Where(x => !string.IsNullOrWhiteSpace(x.Name)).GroupBy(BuildSoftwareKey, StringComparer.OrdinalIgnoreCase).Select(x => x.First())) _db.EndpointSoftwareInventory.Add(new F03EndpointSoftwareInventory { EndpointDeviceId = device.Id, NormalizedName = Normalize(item.Name), DisplayName = Trim(item.DisplayName ?? item.Name, 255), Publisher = Trim(item.Publisher, 255), Version = Trim(item.Version, 100), Architecture = Trim(item.Architecture, 30), InstallDate = item.InstallDate, InstallLocation = Trim(item.InstallLocation, 1000), DetectedAtUtc = now, Source = "FVNAgent" });
                foreach (var item in request.Services.Where(x => !string.IsNullOrWhiteSpace(x.ServiceName)).GroupBy(x => x.ServiceName.Trim(), StringComparer.OrdinalIgnoreCase).Select(x => x.First())) _db.EndpointServiceInventory.Add(new F03EndpointServiceInventory { EndpointDeviceId = device.Id, ServiceName = Trim(item.ServiceName, 255)!, DisplayName = Trim(item.DisplayName, 255), State = Trim(item.State, 30), StartMode = Trim(item.StartMode, 30), BinaryPathHash = Trim(item.BinaryPathHash, 128), DetectedAtUtc = now, Source = "FVNAgent" });
                foreach (var item in request.Antivirus.Where(x => !string.IsNullOrWhiteSpace(x.ProductName)).GroupBy(x => x.ProductName.Trim(), StringComparer.OrdinalIgnoreCase).Select(x => x.First())) antivirusSet.Add(new F03EndpointAntivirusInventory { EndpointDeviceId = device.Id, ProductName = Trim(item.ProductName, 255)!, ProductVersion = Trim(item.ProductVersion, 100), EngineVersion = Trim(item.EngineVersion, 100), DefinitionVersion = Trim(item.DefinitionVersion, 100), DefinitionUpdatedAtUtc = item.DefinitionUpdatedAtUtc, AntivirusEnabled = item.AntivirusEnabled, RealTimeProtectionEnabled = item.RealTimeProtectionEnabled, ProtectionStatus = Trim(item.ProtectionStatus, 50), RunningMode = Trim(item.RunningMode, 50), Source = Trim(item.Source, 30) ?? "FVNAgent", DetectedAtUtc = now });
                await _db.SaveChangesAsync(cancellationToken);
            }
            await tx.CommitAsync(cancellationToken);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); throw; }
        if (inventoryChanged) await _compliance.EvaluateAsync(device.Id, cancellationToken);
        return await ToSummaryAsync(device.Id, cancellationToken);
    }

    public async Task<EndpointInventorySummaryDto?> GetAsync(string deviceKey, CancellationToken cancellationToken = default) { var device = await _db.EndpointDevices.AsNoTracking().SingleOrDefaultAsync(x => x.DeviceKey == deviceKey.Trim(), cancellationToken); return device == null ? null : await ToSummaryAsync(device.Id, cancellationToken); }
    public async Task<IReadOnlyList<EndpointInventorySummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var devices = await _db.EndpointDevices.AsNoTracking().OrderBy(x => x.ComputerName).ThenBy(x => x.DeviceKey).ToListAsync(cancellationToken); var ids = devices.Select(x => x.Id).ToArray(); var software = await _db.EndpointSoftwareInventory.AsNoTracking().Where(x => ids.Contains(x.EndpointDeviceId)).GroupBy(x => x.EndpointDeviceId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, cancellationToken); var services = await _db.EndpointServiceInventory.AsNoTracking().Where(x => ids.Contains(x.EndpointDeviceId)).GroupBy(x => x.EndpointDeviceId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, cancellationToken); var antivirus = await _db.Set<F03EndpointAntivirusInventory>().AsNoTracking().Where(x => ids.Contains(x.EndpointDeviceId)).GroupBy(x => x.EndpointDeviceId).Select(x => new { Id = x.Key, Count = x.Count(), Healthy = x.Count(v => v.AntivirusEnabled == true && v.RealTimeProtectionEnabled == true && v.ProtectionStatus == "Protected"), Latest = x.Max(v => v.DefinitionUpdatedAtUtc) }).ToDictionaryAsync(x => x.Id, cancellationToken); var equipmentIds = devices.Where(x => x.EquipmentAssetId.HasValue).Select(x => x.EquipmentAssetId!.Value).Distinct().ToArray(); var equipment = await _db.EquipmentAssets.AsNoTracking().Where(x => equipmentIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken); return devices.Select(x => { equipment.TryGetValue(x.EquipmentAssetId ?? 0, out var asset); antivirus.TryGetValue(x.Id, out var av); return new EndpointInventorySummaryDto(x.Id, x.DeviceKey, x.ComputerName, x.SerialNumber, x.Status, x.LastSeenUtc, software.GetValueOrDefault(x.Id)?.Count ?? 0, services.GetValueOrDefault(x.Id)?.Count ?? 0, x.EquipmentAssetId, asset?.EquipmentCode, asset?.EquipmentName, av?.Count ?? 0, av?.Healthy ?? 0, av?.Latest); }).ToArray();
    }

    private async Task AddIdentityHistoryAsync(long deviceId, string? oldName, string? newName, string? oldSerial, string? newSerial, string? oldHardwareUuid, string? newHardwareUuid, string? agentInstallationId, string changeType, DbTransaction dbTransaction, CancellationToken ct)
    {
        var connection = _db.Database.GetDbConnection(); await using var command = connection.CreateCommand(); command.Transaction = dbTransaction; command.CommandText = "INSERT INTO dbo.F03EndpointIdentityHistory (EndpointDeviceId, OldComputerName, NewComputerName, OldSerialNumber, NewSerialNumber, OldHardwareUuid, NewHardwareUuid, AgentInstallationId, ChangeType) VALUES (@deviceId, @oldName, @newName, @oldSerial, @newSerial, @oldHardware, @newHardware, @agent, @changeType);"; AddParameter(command, "@deviceId", deviceId); AddParameter(command, "@oldName", oldName); AddParameter(command, "@newName", newName); AddParameter(command, "@oldSerial", oldSerial); AddParameter(command, "@newSerial", newSerial); AddParameter(command, "@oldHardware", oldHardwareUuid); AddParameter(command, "@newHardware", newHardwareUuid); AddParameter(command, "@agent", agentInstallationId); AddParameter(command, "@changeType", changeType); if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct); await command.ExecuteNonQueryAsync(ct);
    }
    private async Task UpsertAlertAsync(long deviceId, string type, string severity, string title, string details, int? policyId, DbTransaction dbTransaction, CancellationToken ct)
    {
        var connection = _db.Database.GetDbConnection(); await using var command = connection.CreateCommand(); command.Transaction = dbTransaction; command.CommandText = "UPDATE dbo.F03EndpointAlerts SET LastDetectedAtUtc = SYSUTCDATETIME(), Details = @details, Status = 'Open', ResolvedAtUtc = NULL WHERE EndpointDeviceId = @deviceId AND AlertType = @type AND Status = 'Open'; IF @@ROWCOUNT = 0 INSERT INTO dbo.F03EndpointAlerts (EndpointDeviceId, AlertType, Severity, Title, Details, RelatedPolicyId) VALUES (@deviceId, @type, @severity, @title, @details, @policyId);"; AddParameter(command, "@deviceId", deviceId); AddParameter(command, "@type", type); AddParameter(command, "@severity", severity); AddParameter(command, "@title", title); AddParameter(command, "@details", details); AddParameter(command, "@policyId", policyId); if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct); await command.ExecuteNonQueryAsync(ct);
    }
    private async Task<EndpointInventorySummaryDto> ToSummaryAsync(long id, CancellationToken cancellationToken)
    { var device = await _db.EndpointDevices.AsNoTracking().SingleAsync(x => x.Id == id, cancellationToken); var softwareCount = await _db.EndpointSoftwareInventory.AsNoTracking().CountAsync(x => x.EndpointDeviceId == id, cancellationToken); var serviceCount = await _db.EndpointServiceInventory.AsNoTracking().CountAsync(x => x.EndpointDeviceId == id, cancellationToken); var avRows = await _db.Set<F03EndpointAntivirusInventory>().AsNoTracking().Where(x => x.EndpointDeviceId == id).ToListAsync(cancellationToken); var asset = device.EquipmentAssetId.HasValue ? await _db.EquipmentAssets.AsNoTracking().Where(x => x.Id == device.EquipmentAssetId.Value).Select(x => new { x.EquipmentCode, x.EquipmentName }).FirstOrDefaultAsync(cancellationToken) : null; return new EndpointInventorySummaryDto(device.Id, device.DeviceKey, device.ComputerName, device.SerialNumber, device.Status, device.LastSeenUtc, softwareCount, serviceCount, device.EquipmentAssetId, asset?.EquipmentCode, asset?.EquipmentName, avRows.Count, avRows.Count(x => x.AntivirusEnabled == true && x.RealTimeProtectionEnabled == true && x.ProtectionStatus == "Protected"), avRows.Count == 0 ? null : avRows.Max(x => x.DefinitionUpdatedAtUtc)); }
    private static string ComputeInventoryHash(EndpointInventoryRequestDto request) { var software = request.Software.Where(x => !string.IsNullOrWhiteSpace(x.Name)).Select(x => string.Join("|", Normalize(x.Name), x.Publisher?.Trim(), x.Version?.Trim(), x.Architecture?.Trim())).OrderBy(x => x, StringComparer.OrdinalIgnoreCase); var services = request.Services.Where(x => !string.IsNullOrWhiteSpace(x.ServiceName)).Select(x => string.Join("|", x.ServiceName.Trim(), x.State?.Trim(), x.StartMode?.Trim(), x.BinaryPathHash?.Trim())).OrderBy(x => x, StringComparer.OrdinalIgnoreCase); var antivirus = request.Antivirus.Where(x => !string.IsNullOrWhiteSpace(x.ProductName)).Select(x => string.Join("|", x.ProductName.Trim(), x.ProductVersion?.Trim(), x.DefinitionVersion?.Trim(), x.DefinitionUpdatedAtUtc?.ToString("O"), x.AntivirusEnabled, x.RealTimeProtectionEnabled, x.ProtectionStatus)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase); return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", software.Concat(services).Concat(antivirus))))); }
    private static string BuildSoftwareKey(EndpointSoftwareInventoryDto item) => string.Join("|", Normalize(item.Name), item.Publisher?.Trim(), item.Version?.Trim(), item.Architecture?.Trim());
    private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static string? Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static void AddParameter(DbCommand command, string name, object? value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter); }
}
