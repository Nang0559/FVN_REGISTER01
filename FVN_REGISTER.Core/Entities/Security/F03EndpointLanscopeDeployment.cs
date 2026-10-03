using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FVN_REGISTER.Core.Entities.Security;

[Table("F03EndpointDeployments")]
public sealed class F03EndpointDeployment
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string DeploymentCode { get; set; } = string.Empty;
    [StringLength(50)] public string? PackageVersion { get; set; }
    [StringLength(30)] public string Status { get; set; } = "Draft";
    public int CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

[Table("F03EndpointDeploymentTargets")]
public sealed class F03EndpointDeploymentTarget
{
    public int Id { get; set; }
    public int DeploymentId { get; set; }
    [Required, StringLength(100)] public string TargetKey { get; set; } = string.Empty;
    [StringLength(100)] public string? ClientId { get; set; }
    [StringLength(255)] public string? ComputerName { get; set; }
    [StringLength(100)] public string? IpAddress { get; set; }
    [StringLength(100)] public string? MacAddress { get; set; }
    [StringLength(255)] public string? SerialNumber { get; set; }
    [StringLength(255)] public string? WindowsUser { get; set; }
    [StringLength(255)] public string? DomainName { get; set; }
    [StringLength(500)] public string? OrganizationalUnit { get; set; }
    [StringLength(255)] public string? LanscopeGroup { get; set; }
    [StringLength(255)] public string? OsName { get; set; }
    [StringLength(255)] public string? Manufacturer { get; set; }
    [StringLength(255)] public string? Model { get; set; }
    [StringLength(30)] public string Status { get; set; } = "Pending";
    public long? EndpointDeviceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? EnrolledAtUtc { get; set; }
}

[Table("F03EndpointEnrollmentTokens")]
public sealed class F03EndpointEnrollmentToken
{
    public long Id { get; set; }
    public int DeploymentId { get; set; }
    public int TargetId { get; set; }
    [Required, StringLength(128)] public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}
