namespace FVN_REGISTER.Contract.Dtos.Security;

public sealed record EndpointSoftwareInventoryDto(
    string Name,
    string? DisplayName,
    string? Publisher,
    string? Version,
    string? Architecture,
    DateTime? InstallDate,
    string? InstallLocation);

public sealed record EndpointServiceInventoryDto(
    string ServiceName,
    string? DisplayName,
    string? State,
    string? StartMode,
    string? BinaryPathHash);

public sealed record EndpointAntivirusInventoryDto(
    string ProductName,
    string? ProductVersion,
    string? EngineVersion,
    string? DefinitionVersion,
    DateTime? DefinitionUpdatedAtUtc,
    bool? AntivirusEnabled,
    bool? RealTimeProtectionEnabled,
    string? ProtectionStatus,
    string? RunningMode,
    string Source);

public sealed record EndpointInventoryRequestDto(
    string DeviceKey,
    string? ComputerName,
    string? SerialNumber,
    string? HardwareUuid,
    string? AgentInstallationId,
    string? OsName,
    string? OsVersion,
    string? EmployeeCode,
    int? EquipmentAssetId,
    string? AgentVersion,
    IReadOnlyList<EndpointSoftwareInventoryDto> Software,
    IReadOnlyList<EndpointServiceInventoryDto> Services,
    IReadOnlyList<EndpointAntivirusInventoryDto> Antivirus,
    string? LanscopeClientId = null,
    string? IpAddress = null,
    string? MacAddress = null,
    string? WindowsUser = null,
    string? DomainName = null,
    string? OrganizationalUnit = null,
    string? LanscopeGroup = null,
    string? Manufacturer = null,
    string? Model = null);

public sealed record EndpointInventorySummaryDto(
    long EndpointId,
    string DeviceKey,
    string? ComputerName,
    string? SerialNumber,
    string Status,
    DateTime? LastSeenUtc,
    int SoftwareCount,
    int ServiceCount,
    int? EquipmentAssetId,
    string? EquipmentCode,
    string? EquipmentName,
    int AntivirusCount = 0,
    int AntivirusHealthyCount = 0,
    DateTime? LatestAntivirusDefinitionUpdatedAtUtc = null);

public sealed record EndpointEquipmentLinkRequest(int EquipmentAssetId);
