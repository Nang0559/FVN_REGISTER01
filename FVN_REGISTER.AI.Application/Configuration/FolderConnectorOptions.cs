namespace FVN_REGISTER.AI.Application.Configuration;

public sealed class FolderConnectorOptions
{
    public string Name { get; set; } = "";
    public string RootPath { get; set; } = "";
    public string TenantCode { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string? ModuleCode { get; set; }
    public string? SchemaCode { get; set; }
    public int RetentionDays { get; set; } = 365;
    public bool IncludeSubdirectories { get; set; }
    public bool Enabled { get; set; } = true;
}
