namespace FVN_REGISTER.EndpointAgent;

public sealed class EndpointAgentOptions
{
    public string ApiBaseUrl { get; set; } = "https://localhost:5001";
    public string DeviceKey { get; set; } = string.Empty;
    public string ApiKeyProtected { get; set; } = string.Empty;
    public string BootstrapPath { get; set; } = string.Empty;
    public int IntervalMinutes { get; set; } = 30;
    public int CredentialRotationLeadDays { get; set; } = 30;
}
