using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FVN_REGISTER.Core.Entities.Security;

[Table("F03EndpointAntivirusInventory")]
public sealed class F03EndpointAntivirusInventory
{
    public long Id { get; set; }
    public long EndpointDeviceId { get; set; }
    [Required, StringLength(255)] public string ProductName { get; set; } = string.Empty;
    [StringLength(100)] public string? ProductVersion { get; set; }
    [StringLength(100)] public string? EngineVersion { get; set; }
    [StringLength(100)] public string? DefinitionVersion { get; set; }
    public DateTime? DefinitionUpdatedAtUtc { get; set; }
    public bool? AntivirusEnabled { get; set; }
    public bool? RealTimeProtectionEnabled { get; set; }
    [StringLength(50)] public string? ProtectionStatus { get; set; }
    [StringLength(50)] public string? RunningMode { get; set; }
    [Required, StringLength(30)] public string Source { get; set; } = "FVNAgent";
    public DateTime DetectedAtUtc { get; set; }
}
