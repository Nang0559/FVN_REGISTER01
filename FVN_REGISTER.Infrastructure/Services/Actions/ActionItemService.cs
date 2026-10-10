using FVN_REGISTER.Application.Interfaces.Actions;
using FVN_REGISTER.Application.Interfaces.Notifications;
using FVN_REGISTER.Application.Services.Execution;
using FVN_REGISTER.Contract.Dtos.Notifications;
using FVN_REGISTER.Core.Entities.Common;
using FVN_REGISTER.Contract.Dtos.Actions;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.Security;
using FVN_REGISTER.Core.Entities.WorkCalendar;
using FVN_REGISTER.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Actions;

public sealed class ActionItemService : IActionItemService
{
    private readonly FVNWEBAPPContext _db;
    private readonly INotificationService _notifications;

    public ActionItemService(FVNWEBAPPContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<IReadOnlyList<ActionItemDto>> GetMineAsync(
        string employeeCode,
        int userId,
        bool includeCompleted = false,
        CancellationToken cancellationToken = default)
    {
        var employeeId = await ResolveEmployeeIdOrNullAsync(employeeCode, cancellationToken);
        // Administrative/break-glass accounts created outside HRM may not have an
        // active F03Employee record. They simply have no employee-scoped actions;
        // this must not make the whole Dashboard fail.
        if (!employeeId.HasValue)
            return Array.Empty<ActionItemDto>();

        await EnsureAssignedExecutionReviewActionsAsync(employeeCode, userId, employeeId.Value, cancellationToken);
        await EnsureNotificationsForOpenActionsAsync(userId, employeeId.Value, cancellationToken);
        var query = _db.ActionItems
            .AsNoTracking()
            .Where(x => x.IsActive != false
                && x.AssignedToEmployeeId == employeeId
                && (x.AssignedToUserId == null || x.AssignedToUserId == userId));

        if (!includeCompleted)
            query = query.Where(x => x.Status == ActionItemStatus.Open
                || x.Status == ActionItemStatus.InProgress);

        return await query
            .OrderBy(x => x.Status == ActionItemStatus.Open ? 0 : 1)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.DueAt)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new ActionItemDto
            {
                ActionId = x.ActionId,
                ModuleCode = x.ModuleCode,
                SourceType = x.SourceType,
                SourceId = x.SourceId,
                ParticipantId = x.ParticipantId,
                EmployeeId = x.EmployeeId,
                AssignedToEmployeeId = x.AssignedToEmployeeId,
                WorkDate = x.WorkDate,
                ActionType = x.ActionType,
                Title = x.Title,
                Summary = x.Summary,
                Severity = x.Severity,
                Priority = x.Priority,
                Status = (byte)x.Status,
                DueAt = x.DueAt,
                DetailRoute = x.DetailRoute,
                ReferenceNo = x.ReferenceNo,
                CreatedAt = x.CreatedAt,
                CompletedAt = x.CompletedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ActionCountDto> GetCountAsync(
        string employeeCode,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var employeeId = await ResolveEmployeeIdOrNullAsync(employeeCode, cancellationToken);
        // Dashboard action counts are optional for accounts without an active HRM
        // employee identity (for example manually-created SuperAdmin accounts).
        if (!employeeId.HasValue)
            return new ActionCountDto();

        await EnsureAssignedExecutionReviewActionsAsync(employeeCode, userId, employeeId.Value, cancellationToken);
        await EnsureNotificationsForOpenActionsAsync(userId, employeeId.Value, cancellationToken);
        var counts = await _db.ActionItems
            .AsNoTracking()
            .Where(x => x.IsActive != false
                && x.AssignedToEmployeeId == employeeId
                && (x.AssignedToUserId == null || x.AssignedToUserId == userId)
                && (x.Status == ActionItemStatus.Open || x.Status == ActionItemStatus.InProgress))
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() })
            .ToListAsync(cancellationToken);

        return new ActionCountDto
        {
            OpenCount = counts.FirstOrDefault(x => x.Status == ActionItemStatus.Open)?.Count ?? 0,
            InProgressCount = counts.FirstOrDefault(x => x.Status == ActionItemStatus.InProgress)?.Count ?? 0
        };
    }

    public async Task<ActionItemDto?> GetAsync(
        string employeeCode,
        int userId,
        Guid actionId,
        CancellationToken cancellationToken = default)
    {
        var employeeId = await ResolveEmployeeIdAsync(employeeCode, cancellationToken);
        return await _db.ActionItems
            .AsNoTracking()
            .Where(x => x.ActionId == actionId
                && x.IsActive != false
                && x.AssignedToEmployeeId == employeeId
                && (x.AssignedToUserId == null || x.AssignedToUserId == userId))
            .Select(x => new ActionItemDto
            {
                ActionId = x.ActionId,
                ModuleCode = x.ModuleCode,
                SourceType = x.SourceType,
                SourceId = x.SourceId,
                ParticipantId = x.ParticipantId,
                EmployeeId = x.EmployeeId,
                AssignedToEmployeeId = x.AssignedToEmployeeId,
                WorkDate = x.WorkDate,
                ActionType = x.ActionType,
                Title = x.Title,
                Summary = x.Summary,
                Severity = x.Severity,
                Priority = x.Priority,
                Status = (byte)x.Status,
                DueAt = x.DueAt,
                DetailRoute = x.DetailRoute,
                ReferenceNo = x.ReferenceNo,
                CreatedAt = x.CreatedAt,
                CompletedAt = x.CompletedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> CompleteAsync(string employeeCode, int userId, Guid actionId, CancellationToken cancellationToken = default)
    {
        var employeeId = await ResolveEmployeeIdAsync(employeeCode, cancellationToken);
        var equipmentRepair = await _db.ActionItems.AsNoTracking()
            .Where(x => x.ActionId == actionId && x.ModuleCode == "EQUIPMENT" && x.SourceType == "EQUIPMENT_REPAIR" && x.ActionType == "REPAIR_EXECUTION")
            .Select(x => (Guid?)x.ActionId).FirstOrDefaultAsync(cancellationToken);
        if (equipmentRepair.HasValue)
            throw new InvalidOperationException("Nhiệm vụ sửa chữa thiết bị phải mở chi tiết yêu cầu để nhập kết quả sửa chữa. Không thể đóng trực tiếp từ Action Center.");
        return await SetTerminalStatusAsync(employeeId, userId, actionId, ActionItemStatus.Completed, cancellationToken);
    }

    public async Task<bool> DismissAsync(string employeeCode, int userId, Guid actionId, CancellationToken cancellationToken = default)
    {
        var employeeId = await ResolveEmployeeIdAsync(employeeCode, cancellationToken);
        return await SetTerminalStatusAsync(employeeId, userId, actionId, ActionItemStatus.Dismissed, cancellationToken);
    }

    private async Task<bool> SetTerminalStatusAsync(
        int employeeId,
        int userId,
        Guid actionId,
        ActionItemStatus target,
        CancellationToken cancellationToken)
    {
        var entity = await _db.ActionItems
            .FirstOrDefaultAsync(x => x.ActionId == actionId
                && x.IsActive != false
                && x.AssignedToEmployeeId == employeeId
                && (x.AssignedToUserId == null || x.AssignedToUserId == userId), cancellationToken);

        if (entity is null)
            return false;

        if (entity.Status == target)
            return true;

        if (entity.Status is ActionItemStatus.Completed
            or ActionItemStatus.Dismissed
            or ActionItemStatus.Expired
            or ActionItemStatus.Cancelled)
            return false;

        // Execution actions may only close after their reconciliation lifecycle
        // has reached Resolved. This keeps Action as workflow state, not a
        // bypass around confirmation/reconciliation.
        var reconciliation = await _db.ExecutionReconciliations
            .AsNoTracking()
            .Where(x => x.IsActive != false && x.ActionId == entity.ActionId)
            .Select(x => new { x.ReconciliationStatus })
            .FirstOrDefaultAsync(cancellationToken);

        if (reconciliation is not null
            && !string.Equals(reconciliation.ReconciliationStatus, "Resolved", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Không thể đóng action vì reconciliation đang ở trạng thái '{reconciliation.ReconciliationStatus}'. " +
                "Cần hoàn tất bước xác nhận hoặc HR giải quyết trước khi đóng action.");

        entity.Status = target;

        // Keep Task and Notification in sync: closing the task also clears its unread
        // notification so the bell / app-icon badge do not keep counting a finished task.
        await MarkNotificationsReadForActionAsync(userId, entity.ActionId, cancellationToken);

        // Department-assigned equipment repairs are a shared queue. The first
        // member who completes the repair closes the same request's remaining
        // open execution actions so nobody keeps a stale task.
        if (target == ActionItemStatus.Completed
            && entity.ModuleCode == "EQUIPMENT"
            && entity.ActionType == "REPAIR_EXECUTION"
            && entity.SourceType == "EQUIPMENT_REPAIR")
        {
            var siblings = await _db.ActionItems
                .Where(x => x.ActionId != entity.ActionId
                    && x.IsActive != false
                    && x.ModuleCode == entity.ModuleCode
                    && x.SourceType == entity.SourceType
                    && x.SourceId == entity.SourceId
                    && x.ActionType == entity.ActionType
                    && (x.Status == ActionItemStatus.Open || x.Status == ActionItemStatus.InProgress))
                .ToListAsync(cancellationToken);

            foreach (var sibling in siblings)
            {
                sibling.Status = ActionItemStatus.Cancelled;
                sibling.DismissedAt = DateTime.Now;
            }
        }

        if (target == ActionItemStatus.Completed)
            entity.CompletedAt = DateTime.Now;
        else if (target == ActionItemStatus.Dismissed)
            entity.DismissedAt = DateTime.Now;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureAssignedExecutionReviewActionsAsync(
        string employeeCode,
        int userId,
        int employeeId,
        CancellationToken cancellationToken)
    {
        var assigned = await _db.Set<F03FeatureOperatorAssignment>()
            .AsNoTracking()
            .AnyAsync(x => x.IsActive == true
                && x.FunctionCode == SecurityFunctionCodes.ExecutionReview
                && x.ResourceType == "EXECUTION_REVIEW"
                && x.ResourceId == null
                && x.EmployeeCode == employeeCode,
                cancellationToken);

        if (!assigned)
            return;

        // The employee confirmation is the source of truth for an HR review task.
        // Evidence is optional, so backfill must not depend on ExecutionConfirmationEvidence.
        var pending = await (
            from confirmation in _db.ExecutionConfirmations.AsNoTracking()
            join reconciliation in _db.ExecutionReconciliations.AsNoTracking()
                on confirmation.ReconciliationId equals reconciliation.Id
            join employee in _db.Employees.AsNoTracking()
                on reconciliation.EmployeeId equals employee.Id
            where confirmation.IsActive != false
                && confirmation.Status == "Pending"
                && reconciliation.IsActive != false
                && reconciliation.ReconciliationStatus != "Resolved"
                && _db.ExecutionPolicies.Any(p =>
                    p.IsActive != false
                    && p.ModuleCode == reconciliation.ModuleCode
                    && p.ReviewMode != 0)
            select new
            {
                reconciliation.Id,
                reconciliation.ModuleCode,
                reconciliation.SourceType,
                reconciliation.SourceId,
                reconciliation.ParticipantId,
                reconciliation.EmployeeId,
                reconciliation.WorkDate,
                employee.EmployeeCode,
                employee.EmployeeName,
                ConfirmationId = confirmation.Id,
                ConfirmationCreatedBy = confirmation.CreatedBy
            })
            .OrderByDescending(x => x.ConfirmationId)
            .Take(200)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return;

        var sourceIds = pending.Select(x => x.Id.ToString()).Distinct().ToList();
        var existing = await _db.ActionItems
            .Where(x => x.IsActive != false
                && x.AssignedToEmployeeId == employeeId
                && x.AssignedToUserId == userId
                && x.ActionType == "EXECUTION_EVIDENCE_REVIEW"
                && sourceIds.Contains(x.SourceId)
                && (x.Status == ActionItemStatus.Open
                    || x.Status == ActionItemStatus.InProgress
                    || x.Status == ActionItemStatus.Expired))
            .Select(x => x.SourceId)
            .ToListAsync(cancellationToken);

        var existingSet = existing.ToHashSet(StringComparer.Ordinal);
        foreach (var item in pending.Where(x => !existingSet.Contains(x.Id.ToString())))
        {
            _db.ActionItems.Add(new F03ActionItem
            {
                ActionId = Guid.NewGuid(),
                ModuleCode = item.ModuleCode,
                SourceType = item.SourceType,
                SourceId = item.Id.ToString(),
                ParticipantId = item.ParticipantId,
                EmployeeId = item.EmployeeId,
                AssignedToEmployeeId = employeeId,
                AssignedToUserId = userId,
                WorkDate = item.WorkDate,
                ActionType = "EXECUTION_EVIDENCE_REVIEW",
                Title = $"Phản hồi cần xử lý: {item.ModuleCode}",
                Summary = $"{item.EmployeeCode} - {item.EmployeeName}: phản hồi #{item.ConfirmationId} cần review.",
                Severity = 1,
                Priority = 200,
                Status = ActionItemStatus.Open,
                DetailRoute = $"/execution/hr?reconciliationId={item.Id}",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ReconciliationId = item.Id,
                    ConfirmationId = item.ConfirmationId,
                    EmployeeCode = item.EmployeeCode
                }),
                CreatedBy = item.ConfirmationCreatedBy,
                CreatedAt = DateTime.Now,
                LastModifiedSource = "EXECUTION_OPERATOR_BACKFILL"
            });
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Task/Notification parity. Every Open/InProgress action assigned to the user must have
    /// a notification carrying the same ActionId; otherwise the Work Center shows a task while
    /// the bell and the app-icon badge (both driven by unread notifications) stay at 0.
    /// Idempotent: NotificationService.CreateAsync de-duplicates on (UserId, ActionId, NotificationType).
    /// </summary>
    private async Task EnsureNotificationsForOpenActionsAsync(
        int userId,
        int employeeId,
        CancellationToken cancellationToken)
    {
        var missing = await _db.ActionItems
            .AsNoTracking()
            .Where(x => x.IsActive != false
                && x.AssignedToEmployeeId == employeeId
                && (x.AssignedToUserId == null || x.AssignedToUserId == userId)
                && (x.Status == ActionItemStatus.Open || x.Status == ActionItemStatus.InProgress)
                && !_db.AppNotifications.Any(n => n.UserId == userId && n.ActionId == x.ActionId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new
            {
                x.ActionId,
                x.ModuleCode,
                x.SourceId,
                x.ActionType,
                x.Title,
                x.Summary,
                x.DetailRoute,
                x.Priority
            })
            .ToListAsync(cancellationToken);

        foreach (var item in missing)
        {
            RequestModule module;
            try { module = ExecutionNotificationModuleMapper.ToRequestModule(item.ModuleCode); }
            catch (InvalidOperationException) { continue; } // module without a notification mapping

            try
            {
                await _notifications.CreateAsync(new CreateNotificationDto
                {
                    UserId = userId,
                    EmployeeCode = string.Empty,
                    Module = module,
                    RelatedRequestId = 0,
                    Action = NotificationAction.Pending,
                    Title = item.Title,
                    Body = item.Summary ?? string.Empty,
                    ActionUrl = item.DetailRoute ?? string.Empty,
                    ActionId = item.ActionId,
                    NotificationType = item.ActionType,
                    IsHighPriority = item.Priority >= 200
                }, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // Notification delivery is secondary; the task stays authoritative and the
                // next read retries (the missing-notification query is idempotent).
            }
        }
    }

    private async Task MarkNotificationsReadForActionAsync(
        int userId,
        Guid actionId,
        CancellationToken cancellationToken)
    {
        var unread = await _db.AppNotifications
            .Where(n => n.UserId == userId && n.ActionId == actionId && !n.IsRead)
            .ToListAsync(cancellationToken);

        var now = DateTime.Now;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }
    }

    private async Task<int?> ResolveEmployeeIdOrNullAsync(string employeeCode, CancellationToken cancellationToken)
    {
        return await _db.Employees
            .AsNoTracking()
            .Where(x => x.IsActive != false && x.EmployeeCode == employeeCode)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<int> ResolveEmployeeIdAsync(string employeeCode, CancellationToken cancellationToken)
    {
        return await _db.Employees
            .AsNoTracking()
            .Where(x => x.IsActive != false && x.EmployeeCode == employeeCode)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy nhân viên của tài khoản hiện tại.");
    }
}
