using FVN_REGISTER.Contract.Dtos.Notifications;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Contract.Utils;


namespace FVN_REGISTER.Application.Interfaces.Notifications
{
    public interface INotificationService
    {
        Task<NotificationDto> CreateAsync(CreateNotificationDto dto, CancellationToken ct = default);

        Task<List<NotificationDto>> GetByUserAsync(
            int userId,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default);

        Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default);

        Task<ServiceResult> MarkReadAsync(int notificationId, int userId, CancellationToken ct = default);

        Task<ServiceResult> MarkAllReadAsync(int userId, CancellationToken ct = default);

        /// <summary>
        /// Marks every unread approver notification (Pending/PendingNextLevel/Escalated/Reminder) of
        /// one request as read, for all users. Call when the request moves on (approved/rejected).
        /// </summary>
        Task<int> ResolveForRequestAsync(RequestModule module, int requestId, CancellationToken ct = default);

        /// <summary>
        /// Self-heal for one approver: unread approver notifications whose request is NOT in the
        /// user's currently actionable inbox set are marked read, so bell == inbox.
        /// </summary>
        Task<int> ResolveStaleApproverAsync(
            int userId,
            IReadOnlyCollection<(RequestModule Module, int RequestId)> actionable,
            CancellationToken ct = default);

        /// <summary>Pushes the current unread count to the user's open clients (bell + app-icon badge).</summary>
        Task RefreshBadgeAsync(int userId, CancellationToken ct = default);
    }
}
