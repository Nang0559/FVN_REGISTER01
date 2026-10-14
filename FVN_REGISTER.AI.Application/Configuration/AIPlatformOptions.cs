namespace FVN_REGISTER.AI.Application.Configuration;

public sealed class AIPlatformOptions
{
    public const string SectionName="AIPlatform";
    public string DeploymentMode { get; set; }="Hybrid";
    public string DefaultProvider { get; set; }="Unconfigured";
    public string? Endpoint { get; set; }
    public string? Model { get; set; }
    public int MaximumFileSizeMb { get; set; }=25;
    public int MaximumConcurrentJobs { get; set; }=2;
    public bool RequireHumanApprovalBeforeBusinessWrite { get; set; }=true;
    public bool EnableExternalProvider { get; set; }
}