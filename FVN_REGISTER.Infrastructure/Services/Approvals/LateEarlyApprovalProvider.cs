using FVN_REGISTER.Application.Interfaces.Emails;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Models.Subjects;
using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Contract.Dtos.ApprovelSnapshotDto;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Entities.LateEarly;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Extensions;
using FVN_REGISTER.Core.Repositories;
using FVN_REGISTER.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.Infrastructure.Services.Approvals;

/// <summary>
/// Approval adapter for LateEarly. Route construction and approver selection are delegated
/// to the shared approval-policy engine; no separate approver table is introduced.
/// </summary>
public sealed class LateEarlyApprovalProvider
    : BaseApprovalProvider<LateEarlyRequestSubject, LateEarlyApprovalProvider>,
      IApprovalProvider<LateEarlyRequestSubject>
{
    public override RequestModule RequestType => RequestModule.LateEarly;

    public LateEarlyApprovalProvider(
        IUnitOfWork uow,
        IEmailService email,
        IApprovalNotificationService notification,
        IEmployeeUserResolver userResolver,
        IApprovalRouteService routeService,
        IApprovalSelectionService selectionService,
        ILogger<LateEarlyApprovalProvider> logger,
        IOptionsMonitor<AuthDebugOptions> options)
        : base(uow, email, notification, userResolver, routeService, selectionService, logger, options)
    {
    }

    public override async Task<ServiceResult<List<ApprovalStepSnapshotDto>>> BuildHierarchyResultAsync(
        ApprovalBuildContext ctx, CancellationToken ct, bool strict = true)
    {
        var result = await base.BuildHierarchyResultAsync(ctx, ct, strict);
        if (!result.Success || result.Data == null)
            return result;

        var requireLevel2 = ctx.Extra.TryGetValue("RequireLevel2", out var value)
            && value is bool required && required;
        if (!requireLevel2)
            return ServiceResult<List<ApprovalStepSnapshotDto>>.Ok(
                result.Data.Where(x => x.IsRequired).ToList());

        var secondLevel = result.Data
            .Where(x => x.Level >= 2)
            .OrderBy(x => x.Level)
            .FirstOrDefault();
        if (secondLevel == null)
            return ServiceResult<List<ApprovalStepSnapshotDto>>.Fail(
                "Chính sách yêu cầu cấp duyệt thứ hai nhưng chưa có người duyệt cấp 2 được chọn. Hãy cấu hình Approval Policy và chọn người duyệt cấp 2.");

        var promoted = result.Data.Select(x => x.Level == secondLevel.Level
            ? x with { IsRequired = true }
            : x).ToList();
        return ServiceResult<List<ApprovalStepSnapshotDto>>.Ok(promoted);
    }

    public override async Task<LateEarlyRequestSubject?> GetSubjectAsync(int requestId, CancellationToken ct)
    {
        var entity = await _uow.Repository<F03LateEarlyRequest>().Query().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (entity == null) return null;

        var employee = await _uow.Repository<F03Employee>().Query().AsNoTracking()
            .Where(x => x.EmployeeCode == entity.EmployeeCode)
            .Select(x => new { x.EmployeeName, x.PositionCode })
            .FirstOrDefaultAsync(ct);

        return LateEarlyRequestSubject.From(entity, employee?.EmployeeName, employee?.PositionCode);
    }

    public override async Task<List<LateEarlyRequestSubject>> GetSubjectsAsync(List<int> requestIds, CancellationToken ct)
    {
        var entities = await _uow.Repository<F03LateEarlyRequest>().Query().AsNoTracking()
            .Where(x => requestIds.Contains(x.Id))
            .ToListAsync(ct);
        var employeeCodes = entities.Select(x => x.EmployeeCode).Distinct().ToList();
        var employees = await _uow.Repository<F03Employee>().Query().AsNoTracking()
            .Where(x => employeeCodes.Contains(x.EmployeeCode))
            .Select(x => new { x.EmployeeCode, x.EmployeeName, x.PositionCode })
            .ToDictionaryAsync(x => x.EmployeeCode, ct);

        return entities.Select(entity =>
        {
            employees.TryGetValue(entity.EmployeeCode, out var employee);
            return LateEarlyRequestSubject.From(entity, employee?.EmployeeName, employee?.PositionCode);
        }).ToList();
    }

    public override async Task ApplyOverallStatusAsync(
        int requestId, IReadOnlyList<ApprovalStepDto> allSteps, CancellationToken ct)
    {
        var entity = await _uow.Repository<F03LateEarlyRequest>().Query()
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (entity == null) return;

        var required = allSteps.Where(x => x.IsRequired).ToList();
        entity.RequestStatus = required.Any(x => x.Decision == DecisionType.Rejected)
            ? ApprovalStatus.Rejected
            : required.Count > 0 && required.All(x => x.Decision == DecisionType.Approved)
                ? ApprovalStatus.Approved
                : required.Any(x => x.Decision == DecisionType.Approved)
                    ? ApprovalStatus.InProgress
                    : ApprovalStatus.Pending;

        if (entity.RequestStatus == ApprovalStatus.Rejected)
            entity.IsActive = false;

        await _uow.SaveChangesAsync(ct);
    }

    public override async Task NotifyStepCompletedAsync(
        LateEarlyRequestSubject subject,
        ApprovalStepDto completedStep,
        bool isFullyApproved,
        CancellationToken ct)
    {
        var status = isFullyApproved
            ? ApprovalStatus.Approved
            : completedStep.Decision == DecisionType.Rejected
                ? ApprovalStatus.Rejected
                : (ApprovalStatus?)null;
        if (status.HasValue)
            await NotifyEmployeeInAppAsync(subject, status.Value, ct);
    }

    public override async Task<PendingApprovalItemDto> ToPendingItemAsync(
        LateEarlyRequestSubject subject,
        List<ApprovalStepDto> steps,
        bool canApprove,
        CancellationToken ct)
    {
        var deptName = subject.DeptCode.HasValue
            ? await _uow.Repository<F03Department>().Query().AsNoTracking()
                .Where(x => x.DeptCode == subject.DeptCode)
                .Select(x => x.DeptName)
                .FirstOrDefaultAsync(ct)
            : null;

        return new PendingApprovalItemDto
        {
            RequestId = subject.RequestId,
            Kind = subject.Module,
            EmployeeCode = subject.EmployeeCode,
            EmployeeName = subject.EmployeeName ?? string.Empty,
            DeptCode = subject.DeptCode ?? 0,
            DeptName = deptName ?? string.Empty,
            FromDate = subject.WorkDate,
            ToDate = subject.WorkDate,
            TotalUnits = 1,
            ApprovalSteps = steps,
            CanApprove = canApprove
        };
    }
}
