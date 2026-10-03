using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FVN_REGISTER.Core.Entities.Security;

[Table("F03EndpointDevices")]
public sealed class F03EndpointDevice
{
    public long Id { get; set; }
    [Required, StringLength(100)] public string DeviceKey { get; set; } = string.Empty;
    [StringLength(100)] public string? LanscopeClientId { get; set; }
    [StringLength(255)] public string? ComputerName { get; set; }
    [StringLength(100)] public string? IpAddress { get; set; }
    [StringLength(100)] public string? MacAddress { get; set; }
    [StringLength(255)] public string? SerialNumber { get; set; }
    [StringLength(255)] public string? HardwareUuid { get; set; }
    [StringLength(100)] public string? AgentInstallationId { get; set; }
    [StringLength(255)] public string? WindowsUser { get; set; }
    [StringLength(255)] public string? DomainName { get; set; }
    [StringLength(500)] public string? OrganizationalUnit { get; set; }
    [StringLength(255)] public string? LanscopeGroup { get; set; }
    [StringLength(255)] public string? OsName { get; set; }
    [StringLength(100)] public string? OsVersion { get; set; }
    [StringLength(255)] public string? Manufacturer { get; set; }
    [StringLength(255)] public string? Model { get; set; }
    [StringLength(50)] public string? EmployeeCode { get; set; }
    public int? EquipmentAssetId { get; set; }
    [StringLength(50)] public string? AgentVersion { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = "Unknown";
    [Required, StringLength(30)] public string IdentityStatus { get; set; } = "Verified";
    [StringLength(128)] public string? LastInventoryHash { get; set; }
    [Required, StringLength(30)] public string Source { get; set; } = "FVNAgent";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<F03EndpointSoftwareInventory> Software { get; set; } = new List<F03EndpointSoftwareInventory>();
    public ICollection<F03EndpointServiceInventory> Services { get; set; } = new List<F03EndpointServiceInventory>();
}

[Table("F03EndpointSoftwareInventory")]
public sealed class F03EndpointSoftwareInventory
{
    public long Id { get; set; }
    public long EndpointDeviceId { get; set; }
    [Required, StringLength(255)] public string NormalizedName { get; set; } = string.Empty;
    [StringLength(255)] public string? DisplayName { get; set; }
    [StringLength(255)] public string? Publisher { get; set; }
    [StringLength(100)] public string? Version { get; set; }
    [StringLength(30)] public string? Architecture { get; set; }
    public DateTime? InstallDate { get; set; }
    [StringLength(1000)] public string? InstallLocation { get; set; }
    public DateTime DetectedAtUtc { get; set; }
    [Required, StringLength(30)] public string Source { get; set; } = "FVNAgent";
}

[Table("F03EndpointServiceInventory")]
public sealed class F03EndpointServiceInventory
{
    public long Id { get; set; }
    public long EndpointDeviceId { get; set; }
    [Required, StringLength(255)] public string ServiceName { get; set; } = string.Empty;
    [StringLength(255)] public string? DisplayName { get; set; }
    [StringLength(30)] public string? State { get; set; }
    [StringLength(30)] public string? StartMode { get; set; }
    [StringLength(128)] public string? BinaryPathHash { get; set; }
    public DateTime DetectedAtUtc { get; set; }
    [Required, StringLength(30)] public string Source { get; set; } = "FVNAgent";
}
