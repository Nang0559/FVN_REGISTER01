using FVN_REGISTER.Application.Interfaces.Notifications;
using FVN_REGISTER.Application.Services.Common;
using FVN_REGISTER.Contract.Dtos.Notifications;
using FVN_REGISTER.Core.Repositories;
using FVN_REGISTER.Application.Maps;
using FVN_REGISTER.Application.Policies;
using FVN_REGISTER.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FVN_REGISTER.Infrastructure.Utils;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Extensions;
using FVN_REGISTER.Core.Entities.Common;


namespace FVN_REGISTER.Infrastructure.Services.Notifications
{
    public class NotificationService : BaseService<NotificationService>, INotificationService
    {
        private readonly IUnitOfWork _uow;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly IServiceScopeFactory _scopeFactory;

        public NotificationService(
            IUnitOfWork uow,
            IHubContext<NotificationHub> hub,
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationService> logger,
            IOptionsMonitor<AuthDebugOptions> options)
            : base(logger, options)
        {
            _uow = uow;
            _hub = hub;
            _scopeFactory = scopeFactory;
        }

        public async Task<NotificationDto> CreateAsync(CreateNotificationDto dto, CancellationToken ct = default)
        {
            if (dto.ActionId.HasValue && !string.IsNullOrWhiteSpace(dto.NotificationType))
            {
                var existing = await _uow.Repository<F03AppNotification>().Query()
                    .AsNoTracking()
                    .Where(x => x.IsActive != false
                        && x.UserId == dto.UserId
                        && x.ActionId == dto.ActionId
                        && x.NotificationType == dto.NotificationType
                        && !x.IsRead)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync(ct);

                if (existing != null && NotificationDeduplicationPolicy.IsDuplicate(existing, dto))
                    return NotificationMapper.ToDto(existing);
            }

            var entity = new F03AppNotification
            {
                UserId = dto.UserId,
                EmployeeCode = dto.EmployeeCode,
                RequestModule = dto.Module,
                Action = dto.Action,
                Title = dto.Title,
                Body = dto.Body,
                ActionUrl = dto.ActionUrl,
                ApprovalLevel = dto.ApprovalLevel,
                RelatedLeaveId = dto.Module == RequestModule.Leave ? dto.RelatedRequestId : null,
                RelatedOTId = dto.Module == RequestModule.Overtime ? dto.RelatedRequestId : null,
                IsHighPriority = dto.IsHighPriority,
                Metadata = dto.Metadata,
                ActionId = dto.ActionId,
                NotificationType = dto.NotificationType,
                IsRead = false
            };

            await _uow.Repository<F03AppNotification>().AddAsync(entity, ct);
            await _uow.SaveChangesAsync(ct);

            // SỬA: map sang DTO trước khi dùng tiếp — 1 nguồn map duy nhất, dùng lại cho cả return và push
            var resultDto = NotificationMapper.ToDto(entity);

            await PushRealtimeAsync(dto.UserId, resultDto, ct);
            await QueueWebPushAsync(dto.UserId, entity.Title, entity.Body, entity.ActionUrl, ct);

            Logger.LogDebugIf(Debug, "[NOTIFY] Created for UserId={UserId} | {Title}", dto.UserId, dto.Title);

            return resultDto;   // SỬA: trả DTO, không trả entity
        }

        public async Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default)
        {
            return await _uow.Repository<F03AppNotification>()
                .Query().AsNoTracking()
                .CountAsync(x => x.UserId == userId && !x.IsRead, ct);
        }

        public async Task<List<NotificationDto>> GetByUserAsync(
    int userId, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            return await _uow.Repository<F03AppNotification>()
                .Query().AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new NotificationDto
                {
                    Id = x.Id,
                    Module = x.RequestModule,
                    Action = x.Action,
                    Title = x.Title,
                    Body = x.Body ?? string.Empty,
                    ApprovalLevel = x.ApprovalLevel,
                    ActionUrl = x.ActionUrl ?? string.Empty,
                    IsRead = x.IsRead,
                    CreatedAt = x.CreatedAt,
                    ActionId = x.ActionId
                })
                .ToListAsync(ct);
        }

        public async Task<ServiceResult> MarkReadAsync(int notificationId, int userId, CancellationToken ct = default)
        {
            var entity = await _uow.Repository<F03AppNotification>()
                .Query()
                .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, ct);

            if (entity == null)
                return ServiceResult.Fail("Không tìm thấy thông báo.");

            if (!entity.IsRead)
            {
                entity.IsRead = true;
                entity.ReadAt = DateTime.Now;
                _uow.Repository<F03AppNotification>().Update(entity);
                await _uow.SaveChangesAsync(ct);
            }

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> MarkAllReadAsync(int userId, CancellationToken ct = default)
        {
            var unread = await _uow.Repository<F03AppNotification>()
                .Query()
                .Where(x => x.UserId == userId && !x.IsRead)
                .ToListAsync(ct);

            if (unread.Count == 0)
                return ServiceResult.Ok("Không có thông báo nào cần đánh dấu.");

            var now = DateTime.Now;
            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }

            await _uow.SaveChangesAsync(ct);
            return ServiceResult.Ok();
        }

        private static readonly NotificationAction[] ApproverActions =
        {
            NotificationAction.Pending,
            NotificationAction.PendingNextLevel,
            NotificationAction.Escalated,
            NotificationAction.Reminder
        };

        private static readonly RequestModule[] InboxModules =
        {
            RequestModule.Leave,
            RequestModule.Overtime,
            RequestModule.Trip,
            RequestModule.Equipment,
            RequestModule.Payroll
        };

        public async Task<int> ResolveForRequestAsync(RequestModule module, int requestId, CancellationToken ct = default)
        {
            string detail;
            try { detail = $"{module.ToDetailPath()}/{requestId}"; }
            catch (ArgumentOutOfRangeException) { return 0; }

            // Approval notifications carry no NotificationType; Execution/Equipment-inspection ones do.
            var rows = await _uow.Repository<F03AppNotification>().Query()
                .Where(x => !x.IsRead
                    && x.RequestModule == module
                    && x.NotificationType == null
                    && x.ActionUrl == detail
                    && ApproverActions.Contains(x.Action))
                .ToListAsync(ct);

            return await MarkResolvedAsync(rows, ct);
        }

        public async Task<int> ResolveStaleApproverAsync(
            int userId,
            IReadOnlyCollection<(RequestModule Module, int RequestId)> actionable,
            CancellationToken ct = default)
        {
            if (userId <= 0) return 0;

            var rows = await _uow.Repository<F03AppNotification>().Query()
                .Where(x => x.UserId == userId
                    && !x.IsRead
                    && x.NotificationType == null
                    && InboxModules.Contains(x.RequestModule)
                    && ApproverActions.Contains(x.Action))
                .ToListAsync(ct);

            var stale = new List<F03AppNotification>();
            foreach (var n in rows)
            {
                if (!TryParseRequestId(n, out var requestId)) continue;
                if (!actionable.Contains((n.RequestModule, requestId)))
                    stale.Add(n);
            }

            return await MarkResolvedAsync(stale, ct);
        }

        public async Task RefreshBadgeAsync(int userId, CancellationToken ct = default)
        {
            var unread = await GetUnreadCountAsync(userId, ct);
            await _hub.Clients.Group(NotificationHubGroups.ForUser(userId))
                .SendAsync("BadgeUpdated", unread, ct);
        }

        private static bool TryParseRequestId(F03AppNotification n, out int requestId)
        {
            requestId = 0;
            if (string.IsNullOrWhiteSpace(n.ActionUrl)) return false;

            string prefix;
            try { prefix = n.RequestModule.ToDetailPath() + "/"; }
            catch (ArgumentOutOfRangeException) { return false; }

            if (!n.ActionUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            var tail = n.ActionUrl.Substring(prefix.Length);
            var cut = tail.IndexOfAny(new[] { '?', '#', '/' });
            if (cut >= 0) tail = tail.Substring(0, cut);
            return int.TryParse(tail, out requestId);
        }

        private async Task<int> MarkResolvedAsync(List<F03AppNotification> rows, CancellationToken ct)
        {
            if (rows.Count == 0) return 0;

            var now = DateTime.Now;
            foreach (var n in rows)
            {
                n.IsRead = true;
                n.ReadAt = now;
                _uow.Repository<F03AppNotification>().Update(n);
            }
            await _uow.SaveChangesAsync(ct);

            foreach (var userId in rows.Select(x => x.UserId).Distinct())
            {
                try { await RefreshBadgeAsync(userId, ct); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[NOTIFY] Badge refresh failed. UserId={UserId}", userId);
                }
            }

            return rows.Count;
        }

        // Web Push (app-icon badge). Best effort and off the request path: it runs in its own DI scope
        // because the request-scoped DbContext is disposed when the request ends.
        private async Task QueueWebPushAsync(int userId, string? title, string? body, string? url, CancellationToken ct)
        {
            int unread;
            try { unread = await GetUnreadCountAsync(userId, ct); }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[NOTIFY] Unread count for Web Push failed. UserId={UserId}", userId);
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var push = scope.ServiceProvider.GetRequiredService<IWebPushService>();
                    await push.NotifyNewAsync(userId, unread, title, body, url, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[NOTIFY] Web Push dispatch failed. UserId={UserId}", userId);
                }
            });
        }

        // private helper — không lộ ra interface
        private async Task PushRealtimeAsync(int userId, NotificationDto dto, CancellationToken ct)
        {
            await _hub.Clients.Group(NotificationHubGroups.ForUser(userId))
                .SendAsync("ReceiveNotification", dto, ct);
        }

    }
}
