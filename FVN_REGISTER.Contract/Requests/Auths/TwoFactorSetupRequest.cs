using System.ComponentModel.DataAnnotations;

namespace FVN_REGISTER.Contract.Requests.Auths;

public sealed class TwoFactorSetupRequest
{
    [Required]
    public string ChallengeToken { get; set; } = string.Empty;
}
