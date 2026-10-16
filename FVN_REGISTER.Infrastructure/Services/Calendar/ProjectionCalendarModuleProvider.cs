using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Models.Calendar;
using FVN_REGISTER.Application.Policies;
using FVN_REGISTER.Contract.Dtos.Calendar;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Calendar;

public sealed class ProjectionCalendarModuleProvider : ICalendarModuleProvider
{
    private readonly FVNWEBAPPContext _db;

    public ProjectionCalendarModuleProvider(FVNWEBAPPContext db, string moduleCode)
    {
        _db = db;
        ModuleCode = moduleCode;
    }

    public string ModuleCode { get; }

    public async Task<IReadOnlyList<CalendarItemDto>> GetItemsAsync(
        CalendarContext context,
        CancellationToken cancellationToken = default)
    {
        var projections = await _db.CalendarProjections
            .AsNoTracking()
            .Where(x => x.IsActive != false
                && x.EmployeeId == context.EmployeeId
                && x.ModuleCode == ModuleCode
                && x.WorkDate >= context.From
                && x.WorkDate <= context.To)
            .OrderBy(x => x.WorkDate)
            .ThenByDescending(x => x.RequiresAction)
            .ThenByDescending(x => x.Severity)
            .ToListAsync(cancellationToken);

        if (projections.Count == 0)
            return Array.Empty<CalendarItemDto>();

        // Phản hồi của nhân viên + ý kiến HR nằm ở F03ExecutionConfirmation, không nằm trong projection.
        // Đọc trực tiếp ở đây để lịch OT/Trip/Leave đổi "?" -> Pending / HR request / Complete / Rejected
        // giống lịch chấm công, kể cả khi projection chưa được ghi lại sau lần HR review.
        var reconciliations = await _db.ExecutionReconciliations
            .AsNoTracking()
            .Where(x => x.IsActive != false
                && x.ModuleCode == ModuleCode
                && x.EmployeeId == context.EmployeeId
                && x.WorkDate >= context.From
                && x.WorkDate <= context.To)
            .Select(x => new ReconciliationRow(
                x.Id, x.WorkDate, x.SourceType, x.SourceId, x.ParticipantId, x.ReconciliationStatus))
            .ToListAsync(cancellationToken);

        var reconciliationIds = reconciliations.Select(x => x.Id).ToList();
        var confirmationByReconciliation = reconciliationIds.Count == 0
            ? new Dictionary<long, ConfirmationRow>()
            : (await _db.ExecutionConfirmations
                .AsNoTracking()
                .Where(x => x.IsActive != false && reconciliationIds.Contains(x.ReconciliationId))
                .Select(x => new ConfirmationRow(x.Id, x.ReconciliationId, x.Status, x.SubmittedAt, x.ReviewedAt))
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.ReconciliationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var reconciliationByKey = reconciliations
            .GroupBy(x => Key(x.WorkDate, x.SourceType, x.SourceId, x.ParticipantId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        return projections.Select(x =>
        {
            ExecutionFeedbackStatus? feedback = null;
            if (reconciliationByKey.TryGetValue(Key(x.WorkDate, x.SourceType, x.SourceId, x.ParticipantId), out var reconciliation)
                && confirmationByReconciliation.TryGetValue(reconciliation.Id, out var confirmation))
            {
                feedback = ExecutionFeedbackStatusPolicy.Resolve(
                    reconciliation.Status,
                    confirmation.Status,
                    confirmation.SubmittedAt,
                    confirmation.ReviewedAt);
            }

            var requiresAction = x.RequiresAction;
            var severity = x.Severity;
            var marker = x.Marker;
            var summary = x.Summary;

            if (feedback is { } fb)
            {
                severity = fb.Severity;
                marker = fb.Marker;
                // Pending / Complete: nhân viên không còn việc phải làm; HR request / Rejected: vẫn cần nhân viên xử lý.
                requiresAction = x.RequiresAction && fb.RequiresEmployeeAction;
                summary = string.IsNullOrWhiteSpace(summary) ? fb.Label : $"{fb.Label} · {summary}";
            }

            return new CalendarItemDto
            {
                WorkDate = x.WorkDate,
                ModuleCode = x.ModuleCode,
                StatusCode = x.StatusCode,
                Marker = marker,
                Summary = summary,
                Severity = severity,
                RequiresAction = requiresAction,
                InteractionType = requiresAction && x.ActionId != null ? "CONFIRMATION" : x.DetailRoute != null ? "DETAIL" : "INFO",
                ActionId = x.ActionId,
                DetailRoute = x.DetailRoute,
                SourceId = x.SourceId,
                FeedbackTone = feedback?.Tone,
                FeedbackMarker = feedback?.Marker,
                FeedbackLabel = feedback?.Label
            };
        }).ToArray();
    }

    private static string Key(DateOnly workDate, string? sourceType, string? sourceId, string? participantId)
        => $"{workDate:yyyyMMdd}|{sourceType}|{sourceId}|{participantId}";

    private sealed record ReconciliationRow(
        long Id,
        DateOnly WorkDate,
        string? SourceType,
        string? SourceId,
        string? ParticipantId,
        string? Status);

    private sealed record ConfirmationRow(
        long Id,
        long ReconciliationId,
        string Status,
        DateTime? SubmittedAt,
        DateTime? ReviewedAt);
}
