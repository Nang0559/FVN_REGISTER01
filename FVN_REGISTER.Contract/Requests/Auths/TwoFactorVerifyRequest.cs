using System.ComponentModel.DataAnnotations;

namespace FVN_REGISTER.Contract.Requests.Auths;

public sealed class TwoFactorVerifyRequest
{
    [Required]
    public string ChallengeToken { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
