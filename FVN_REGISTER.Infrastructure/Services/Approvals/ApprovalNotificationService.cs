
using FVN_REGISTER.Application.Factories;
using FVN_REGISTER.Application.Interfaces.Emails;
using FVN_REGISTER.Application.Interfaces.Notifications;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Rules;
using FVN_REGISTER.Application.Services.Common;
using FVN_REGISTER.Core.Extensions;
using FVN_REGISTER.Core.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.Infrastructure.Services.Approvals
{
    public class ApprovalNotificationService
      : BaseApplicationService<ApprovalNotificationService>, IApprovalNotificationService
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationFactory _factory;
        private readonly INotificationService _notification;
        private readonly IEmailService _email;
        private readonly IEmployeeUserResolver _userResolver;

        public ApprovalNotificationService(
            IUnitOfWork uow,
            INotificationFactory factory,
            INotificationService notification,
            IEmailService email,
            IEmployeeUserResolver userResolver,
            ILogger<ApprovalNotificationService> logger,
            IOptionsMonitor<AuthDebugOptions> options)
            : base(logger, options)
        {
            _uow = uow;
            _factory = factory;
            _notification = notification;
            _email = email;
            _userResolver = userResolver;
        }

        public async Task NotifyNewRequestAsync(string approverCode,
            string approverEmail, string approverName, int requestId,
            RequestModule requestType, string creatorName, int level,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(approverEmail))
            {
                Logger.LogWarnIf(Debug,
                    "[APPROVAL-NOTIFY] Bỏ qua email: approverEmail rỗng | RequestId={Id}", requestId);
                return;
            }

            var dto = _factory.CreateApprovalNotification(
                userId: 0,
                employeeCode: null,
                module: requestType,
                requestId: requestId,
                action: NotificationAction.Pending,
                level: level);

            // Must match the canonical Email Center seed convention:
            // {MODULE}_APPROVAL_REQUEST.
            var templateCode = $"{requestType.ToCode()}_APPROVAL_REQUEST";

            await _email.QueueEmail(approverEmail, templateCode, new
            {
                ApproverName = approverName,
                CreatorName = creatorName,
                dto.Title,
                dto.Body,
                dto.ActionUrl
            }, ct);

            Logger.LogInfoIf(Debug,
                "[APPROVAL-NOTIFY] Queued email {Template} to {Email} | RequestId={Id}",
                templateCode, approverEmail, requestId);
        }

        public async Task NotifyApproverInAppAsync(
            string approverEmployeeCode, int requestId,
            RequestModule requestType, string creatorName, int level,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(approverEmployeeCode))
            {
                Logger.LogWarnIf(Debug,
                    "[APPROVAL-NOTIFY] Bỏ qua in-app: approverEmployeeCode rỗng | RequestId={Id}", requestId);
                return;
            }

            var approverUserId = await _userResolver.ResolveUserIdAsync(approverEmployeeCode, ct);
            if (approverUserId is null or <= 0)
            {
                Logger.LogWarnIf(Debug,
                    "[APPROVAL-NOTIFY] Bỏ qua in-app: không tìm thấy UserId cho EmployeeCode={Code} | RequestId={Id}",
                    approverEmployeeCode, requestId);
                return;
            }

            var dto = _factory.CreateApprovalNotification(
                userId: approverUserId.Value,
                employeeCode: approverEmployeeCode,
                module: requestType,
                requestId: requestId,
                action: level > 1 ? NotificationAction.PendingNextLevel : NotificationAction.Pending,
                level: level);

            await _notification.CreateAsync(dto, ct);
        }

        public async Task NotifyCreatorInAppAsync(
            int creatorUserId, string creatorEmployeeCode, string status,
            int requestId, RequestModule requestType,
            CancellationToken ct = default)
        {
            if (creatorUserId <= 0)
            {
                Logger.LogWarnIf(Debug,
                    "[APPROVAL-NOTIFY] Bỏ qua in-app: creatorUserId không hợp lệ | RequestId={Id}", requestId);
                return;
            }

            var action = NotificationActionRules.FromApprovalStatus(status);

            var dto = _factory.CreateApprovalNotification(
                userId: creatorUserId,
                employeeCode: creatorEmployeeCode,
                module: requestType,
                requestId: requestId,
                action: action);

            await _notification.CreateAsync(dto, ct);
        }

        public async Task NotifyEscalatedInAppAsync(
            int oldApproverUserId, int newApproverUserId, string newApproverEmployeeCode,
            int requestId, RequestModule requestType,
            CancellationToken ct = default)
        {
            if (newApproverUserId > 0)
            {
                var dto = _factory.CreateApprovalNotification(
                    userId: newApproverUserId,
                    employeeCode: newApproverEmployeeCode,
                    module: requestType,
                    requestId: requestId,
                    action: NotificationAction.Escalated);

                await _notification.CreateAsync(dto, ct);
            }

            Logger.LogInfoIf(Debug,
                "[APPROVAL-NOTIFY] Escalated RequestId={Id} | {Old} -> {New}",
                requestId, oldApproverUserId, newApproverUserId);
        }
    }
}