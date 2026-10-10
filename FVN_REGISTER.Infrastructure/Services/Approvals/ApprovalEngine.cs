using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Application.Maps;
using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Contract.Dtos.ApprovelSnapshotDto;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.Approvers;
using FVN_REGISTER.Core.Entities.Common;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Approvals;

public class ApprovalEngine<TSubject> : IApprovalEngine<TSubject>
    where TSubject : IApprovalSubject
{
    private readonly IUnitOfWork _uow;
    private readonly IApprovalProvider<TSubject> _provider;
    private readonly IApprovalNotificationService _notification;

    public ApprovalEngine(
        IUnitOfWork uow,
        IApprovalProvider<TSubject> provider,
        IApprovalNotificationService notification)
    {
        _uow = uow;
        _provider = provider;
        _notification = notification;
    }

    private RequestModule Module => _provider.RequestType;

    public async Task InitializeStepsAsync(
        int requestId, ApprovalSnapshotDto snapshot, CancellationToken ct)
    {
        var existing = await _uow.Repository<F03ApprovalSnapshot>()
            .Query()
            .FirstOrDefaultAsync(
                s => s.RequestId == requestId && s.RequestType == Module, ct);

        if (existing != null)
        {
            // Approval initialization is idempotent. A request must never receive a
            // second live snapshot because a retry was triggered after a timeout.
            return;
        }

        var entity = new F03ApprovalSnapshot
        {
            RequestId = requestId,
            RequestType = Module,
            RequesterEmployeeCode = snapshot.RequesterEmployeeCode,
            CreatedAt = snapshot.CapturedAt,
            Steps = snapshot.Steps
                .OrderBy(s => s.Level)
                .Select(s => new F03ApprovalStepSnapshot
                {
                    Level = s.Level,
                    ApproverCode = s.ApproverCode ?? string.Empty,
                    ApproverName = s.ApproverName ?? string.Empty,
                    ApproverEmail = s.ApproverEmail ?? string.Empty,
                    RoleName = s.RoleName,
                    IsRequired = s.IsRequired
                })
                .ToList()
        };

        await _uow.Repository<F03ApprovalSnapshot>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<ApprovalActionResult> ProcessDecisionAsync(
        ApprovalActionDto action, CancellationToken ct)
    {
        var builder = new ApprovalActionResult.Builder().SetTotal(action.RequestIds.Count);
        var processedIds = new List<int>();

        foreach (var requestId in action.RequestIds)
        {
            var snapshot = await _uow.Repository<F03ApprovalSnapshot>()
                .Query()
                .Include(s => s.Steps)
                .FirstOrDefaultAsync(
                    s => s.RequestId == requestId && s.RequestType == Module, ct);

            var step = snapshot?.Steps.FirstOrDefault(s => s.Level == action.Level);
            if (step == null)
            {
                builder.AddError(requestId, "Không tìm thấy bước duyệt tương ứng.");
                continue;
            }

            var already = await _uow.Repository<F03ApprovalHistory>()
                .Query()
                .AnyAsync(h =>
                    h.RequestId == requestId &&
                    h.RequestType == Module &&
                    h.StepId == step.Id, ct);
            if (already)
            {
                builder.AddError(requestId, "Đơn đã được xử lý trước đó.");
                continue;
            }

            var isOverride = !string.Equals(
                action.ApproverCode, step.ApproverCode, StringComparison.OrdinalIgnoreCase);

            if (isOverride && !action.ApproverPermission.IsAdmin())
            {
                builder.AddError(requestId, "Bạn không có quyền duyệt thay người khác.");
                continue;
            }

            var history = new F03ApprovalHistory
            {
                RequestType = Module,
                RequestId = requestId,
                StepId = step.Id,
                ApproverCode = action.ApproverCode,
                ApproverName = step.ApproverName,
                Decision = action.IsReject ? DecisionType.Rejected : DecisionType.Approved,
                Comment = action.Comment,
                ActionAt = DateTime.Now,
                IsOverriddenByAdmin = isOverride
            };

            await _uow.Repository<F03ApprovalHistory>().AddAsync(history, ct);
            builder.AddSuccess(requestId);
            processedIds.Add(requestId);
        }

        await _uow.SaveChangesAsync(ct);

        foreach (var requestId in processedIds)
        {
            var stepsFull = await GetStepsAsync(requestId, ct);
            await _provider.ApplyOverallStatusAsync(requestId, stepsFull, ct);

            // The request moved on: the approvers' pending notifications for it are now stale.
            // Resolve them BEFORE notifying the next approver so only the new one stays unread.
            try { await _notification.ResolveApproverNotificationsAsync(Module, requestId, ct); }
            catch (OperationCanceledException) { throw; }
            catch { /* notification housekeeping is secondary; the next inbox load self-heals */ }

            var subject = await _provider.GetSubjectAsync(requestId, ct);
            var completedStep = stepsFull.FirstOrDefault(s => s.Level == action.Level);

            if (subject != null && completedStep != null)
            {
                var isFullyApproved = subject.OverallStatus == ApprovalStatus.Approved;
                await _provider.NotifyStepCompletedAsync(
                    subject, completedStep, isFullyApproved, ct);

                if (!action.IsReject && !isFullyApproved)
                    await NotifyNextApproverAsync(requestId, subject, ct);
            }
        }

        return builder.Build();
    }

    private async Task NotifyNextApproverAsync(
        int requestId,
        TSubject subject,
        CancellationToken ct)
    {
        var next = await GetNextStepAsync(requestId, ct);
        if (next == null) return;

        if (!string.IsNullOrWhiteSpace(next.ApproverCode) && !string.IsNullOrWhiteSpace(next.ApproverEmail))
        {
            await _notification.NotifyNewRequestAsync(
                next.ApproverCode!,
                next.ApproverEmail!,
                next.ApproverName ?? string.Empty,
                requestId,
                Module,
                subject.EmployeeName ?? string.Empty,
                next.Level,
                ct);
        }

        if (string.IsNullOrWhiteSpace(next.ApproverCode))
            return;

        await _notification.NotifyApproverInAppAsync(
            next.ApproverCode,
            requestId,
            Module,
            subject.EmployeeName ?? string.Empty,
            next.Level,
            ct);
    }

    /// <param name="approver">
    /// EmployeeCode của người duyệt (ưu tiên, khớp với thông báo vốn gửi theo ApproverCode)
    /// hoặc email (tương thích ngược).
    /// </param>
    public async Task<List<PendingApprovalItemDto>> GetPendingForApproverAsync(
        string approver, CancellationToken ct)
    {
        var key = approver?.Trim();
        if (string.IsNullOrEmpty(key)) return new();

        var approverCode = await _uow.Repository<F03Employee>()
            .Query()
            .Where(e => e.EmployeeCode == key)
            .Select(e => e.EmployeeCode)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrEmpty(approverCode))
        {
            approverCode = await _uow.Repository<F03Employee>()
                .Query()
                .Where(e => e.EmailAddress == key)
                .Select(e => e.EmployeeCode)
                .FirstOrDefaultAsync(ct);
        }

        if (string.IsNullOrEmpty(approverCode)) return new();

        var snapshots = await _uow.Repository<F03ApprovalSnapshot>()
            .Query()
            .Include(s => s.Steps)
            .Where(s => s.RequestType == Module &&
                        s.Steps.Any(step => step.ApproverCode == approverCode))
            .ToListAsync(ct);

        var result = new List<PendingApprovalItemDto>();

        foreach (var snapshot in snapshots)
        {
            var calculated = await GetStepsAsync(snapshot.RequestId, ct);

            // Một người có thể giữ nhiều cấp trong cùng một đơn: lấy cấp đang thực sự
            // chờ họ (Pending và các cấp bắt buộc trước đó đã duyệt), không phải cấp đầu tiên.
            var myStep = snapshot.Steps
                .Where(s => s.ApproverCode == approverCode)
                .OrderBy(s => s.Level)
                .FirstOrDefault(s =>
                {
                    var calc = calculated.FirstOrDefault(c => c.Level == s.Level);
                    return calc != null
                        && calc.Decision == DecisionType.Pending
                        && calculated
                            .Where(c => c.Level < s.Level && c.IsRequired)
                            .All(c => c.Decision == DecisionType.Approved);
                });
            if (myStep == null) continue;

            var subject = await _provider.GetSubjectAsync(snapshot.RequestId, ct);
            if (subject == null) continue;

            if (subject.OverallStatus is not
                (ApprovalStatus.Pending
                or ApprovalStatus.InProgress
                or ApprovalStatus.Escalated
                or ApprovalStatus.NeedsRevision))
                continue;

            var item = await _provider.ToPendingItemAsync(
                subject, calculated, canApprove: true, ct);
            result.Add(item);
        }

        return result;
    }

    public async Task<List<ApprovalStepDto>> GetStepsAsync(
        int requestId, CancellationToken ct)
    {
        var snapshot = await _uow.Repository<F03ApprovalSnapshot>()
            .Query()
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(
                s => s.RequestId == requestId && s.RequestType == Module, ct);

        if (snapshot == null) return new List<ApprovalStepDto>();

        var histories = await _uow.Repository<F03ApprovalHistory>()
            .Query()
            .Where(h => h.RequestId == requestId && h.RequestType == Module)
            .ToListAsync(ct);

        return ApprovalStepMapper.MapToList(snapshot.Steps, histories);
    }

    public async Task<ApprovalStepDto?> GetNextStepAsync(
        int requestId, CancellationToken ct)
    {
        var snapshot = await _uow.Repository<F03ApprovalSnapshot>()
            .Query()
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(
                s => s.RequestId == requestId && s.RequestType == Module, ct);

        if (snapshot == null) return null;

        var histories = await _uow.Repository<F03ApprovalHistory>()
            .Query()
            .Where(h => h.RequestId == requestId && h.RequestType == Module)
            .ToListAsync(ct);

        var steps = ApprovalStepMapper.MapToList(snapshot.Steps, histories);
        return steps
            .Where(s => s.IsRequired && s.Decision == DecisionType.Pending)
            .OrderBy(s => s.Level)
            .FirstOrDefault(s => steps
                .Where(p => p.Level < s.Level && p.IsRequired)
                .All(p => p.Decision == DecisionType.Approved));
    }
}
