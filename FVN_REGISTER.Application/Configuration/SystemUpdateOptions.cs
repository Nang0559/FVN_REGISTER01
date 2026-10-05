namespace FVN_REGISTER.Application.Configuration
{
    /// <summary>Configuration for the signed, server-side release updater.</summary>
    public sealed class SystemUpdateOptions
    {
        public bool Enabled { get; set; }
        public string InboxPath { get; set; } = string.Empty;
        public string StatusPath { get; set; } = string.Empty;
        public string PublicKeyPath { get; set; } = string.Empty;
        public int MaxPackageMb { get; set; } = 300;

        public bool IsConfigured => Enabled
            && !string.IsNullOrWhiteSpace(InboxPath)
            && !string.IsNullOrWhiteSpace(StatusPath)
            && !string.IsNullOrWhiteSpace(PublicKeyPath);
    }
}