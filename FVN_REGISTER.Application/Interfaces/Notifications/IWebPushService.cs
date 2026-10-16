using FVN_REGISTER.Contract.Dtos.Notifications;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.Notifications
{
    public interface IWebPushService
    {
        /// <summary>VAPID public key, or null when Web Push is not configured.</summary>
        string? PublicKey { get; }

        /// <summary>Bind a device subscription to the user (re-binds if the device was used by another user).</summary>
        Task<ServiceResult> SubscribeAsync(int userId, PushSubscribeRequestDto dto, CancellationToken ct = default);

        Task<ServiceResult> UnsubscribeAsync(int userId, string endpoint, CancellationToken ct = default);

        /// <summary>Push "new notification + absolute unread count" to every device of the user. Best effort.</summary>
        Task NotifyNewAsync(int userId, int unreadCount, string? title, string? body, string? url, CancellationToken ct = default);
    }
}
