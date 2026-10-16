namespace FVN_REGISTER.Application.Configuration
{
    /// <summary>
    /// Section "WebPush". Keep PrivateKey out of source control
    /// (environment variable WebPush__PrivateKey, user-secrets, or web.config).
    /// </summary>
    public sealed class WebPushOptions
    {
        /// <summary>VAPID subject: "mailto:it@company.com" or an https URL.</summary>
        public string Subject { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;

        /// <summary>false (default): push shows only a generic "N unread" text. true: show notification title/body.</summary>
        public bool ShowContent { get; set; }

        public int TimeoutSeconds { get; set; } = 8;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Subject)
            && !string.IsNullOrWhiteSpace(PublicKey)
            && !string.IsNullOrWhiteSpace(PrivateKey);
    }
}
