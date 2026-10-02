using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FVN_REGISTER.Core.Entities.Common
{
    /// <summary>Web Push subscription of one browser/device, bound to the user who last signed in on it.</summary>
    [Table("F03PushSubscriptions")]
    public sealed class F03PushSubscription : BaseAuditEntity
    {
        public int UserId { get; set; }

        /// <summary>SHA-256 hex (64 chars) of <see cref="Endpoint"/>; unique per device.</summary>
        [Required, StringLength(64)]
        public string EndpointHash { get; set; } = string.Empty;

        [Required, StringLength(1000)]
        public string Endpoint { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string P256dh { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Auth { get; set; } = string.Empty;

        [StringLength(300)]
        public string? UserAgent { get; set; }

        public DateTime LastUsedAt { get; set; } = DateTime.Now;
    }
}
