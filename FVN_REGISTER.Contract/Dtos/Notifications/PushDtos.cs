namespace FVN_REGISTER.Contract.Dtos.Notifications
{
    public sealed class PushSubscribeRequestDto
    {
        public string Endpoint { get; set; } = string.Empty;
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
        public string? UserAgent { get; set; }
    }

    public sealed class PushUnsubscribeRequestDto
    {
        public string Endpoint { get; set; } = string.Empty;
    }

    public sealed class PushPublicKeyDto
    {
        public string PublicKey { get; set; } = string.Empty;
    }
}
