using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Application.Interfaces.Emails;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Models.Subjects;
using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Core.Entities.Common;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Entities.Payroll;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Repositories;
using FVN_REGISTER.Core.Utils;
using FVN_REGISTER.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.Infrastructure.Services.Approvals;

public sealed class PayrollApprovalProvider
    : BaseApprovalProvider<PayrollPeriodSubject, PayrollApprovalProvider>, IApprovalProvider<PayrollPeriodSubject>
{
    public override RequestModule RequestType => RequestModule.Payroll;

    public PayrollApprovalProvider(
        IUnitOfWork uow,
        IEmailService email,
        IApprovalNotificationService notification,
        IEmployeeUserResolver userResolver,
        IApprovalRouteService routeService,
        IApprovalSelectionService selectionService,
        ILogger<PayrollApprovalProvider> logger,
        IOptionsMonitor<AuthDebugOptions> options)
        : base(uow, email, notification, userResolver, routeService, selectionService, logger, options) { }

    public override async Task<PayrollPeriodSubject?> GetSubjectAsync(int requestId, CancellationToken ct)
    {
        var period = await _uow.Repository<F03PayrollCalculationPeriod>().Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == requestId && x.IsActive != false, ct);
        if (period == null) return null;

        var snapshotRequester = await _uow.Repository<F03ApprovalSnapshot>().Query()
            .AsNoTracking()
            .Where(x => x.RequestId == requestId && x.RequestType == RequestType)
            .Select(x => x.RequesterEmployeeCode)
            .FirstOrDefaultAsync(ct);

        var employeeCode = snapshotRequester ?? string.Empty;
        var employee = string.IsNullOrWhiteSpace(employeeCode)
            ? null
            : await _uow.Repository<F03Employee>().Query().AsNoTracking()
                .Where(x => x.IsActive == true && x.EmployeeCode == employeeCode)
                .Select(x => new { x.EmployeeCode, x.EmployeeName, x.DeptCode, x.PositionCode })
                .FirstOrDefaultAsync(ct);

        var status = await CalculateStatusAsync(requestId, ct);

        return new PayrollPeriodSubject
        {
            RequestId = period.Id,
            EmployeeCode = employee?.EmployeeCode ?? employeeCode,
            EmployeeName = employee?.EmployeeName,
            DeptCode = employee?.DeptCode,
            PositionCode = employee?.PositionCode,
            OverallStatus = status,
            PeriodCode = period.PeriodCode,
            FromDate = period.FromDate,
            ToDate = period.ToDate,
            PeriodStatus = period.Status
        };
    }

    public override async Task<List<PayrollPeriodSubject>> GetSubjectsAsync(List<int> requestIds, CancellationToken ct)
    {
        var periods = await _uow.Repository<F03PayrollCalculationPeriod>().Query().AsNoTracking()
            .Where(x => requestIds.Contains(x.Id) && x.IsActive != false)
            .ToListAsync(ct);

        var requesterMap = await _uow.Repository<F03ApprovalSnapshot>().Query().AsNoTracking()
            .Where(x => requestIds.Contains(x.RequestId) && x.RequestType == RequestType)
            .Select(x => new { x.RequestId, x.RequesterEmployeeCode })
            .ToDictionaryAsync(x => x.RequestId, x => x.RequesterEmployeeCode ?? string.Empty, ct);

        var employeeCodes = requesterMap.Values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var employees = await _uow.Repository<F03Employee>().Query().AsNoTracking()
            .Where(x => x.IsActive == true && employeeCodes.Contains(x.EmployeeCode))
            .Select(x => new { x.EmployeeCode, x.EmployeeName, x.DeptCode, x.PositionCode })
            .ToDictionaryAsync(x => x.EmployeeCode, x => x, ct);

        var histories = await _uow.Repository<F03ApprovalHistory>().Query().AsNoTracking()
            .Where(x => requestIds.Contains(x.RequestId) && x.RequestType == RequestType)
            .ToListAsync(ct);

        return periods.Select(period =>
        {
            requesterMap.TryGetValue(period.Id, out var code);
            employees.TryGetValue(code ?? string.Empty, out var employee);
            var steps = histories.Where(x => x.RequestId == period.Id).ToList();
            var status = steps.Any(x => x.Decision == DecisionType.Rejected)
                ? ApprovalStatus.Rejected
                : steps.Count > 0 && steps.All(x => x.Decision == DecisionType.Approved)
                    ? ApprovalStatus.Approved
                    : steps.Any(x => x.Decision == DecisionType.Approved)
                        ? ApprovalStatus.InProgress
                        : ApprovalStatus.Pending;

            return new PayrollPeriodSubject
            {
                RequestId = period.Id,
                EmployeeCode = employee?.EmployeeCode ?? code ?? string.Empty,
                EmployeeName = employee?.EmployeeName,
                DeptCode = employee?.DeptCode,
                PositionCode = employee?.PositionCode,
                OverallStatus = status,
                PeriodCode = period.PeriodCode,
                FromDate = period.FromDate,
                ToDate = period.ToDate,
                PeriodStatus = period.Status
            };
        }).ToList();
    }

    public override Task ApplyOverallStatusAsync(int requestId, IReadOnlyList<ApprovalStepDto> allSteps, CancellationToken ct)
    {
        // Approval state is intentionally kept in F03ApprovalHistory/Snapshot.
        // PayrollCalculationPeriod.Status remains the technical lock/export state;
        // this prevents Approval from corrupting the existing payroll lifecycle.
        return Task.CompletedTask;
    }

    public override async Task NotifyStepCompletedAsync(
        PayrollPeriodSubject subject,
        ApprovalStepDto completedStep,
        bool isFullyApproved,
        CancellationToken ct)
    {
        if (isFullyApproved)
        {
            await NotifyEmployeeInAppAsync(subject, ApprovalStatus.Approved, ct);
            return;
        }

        if (completedStep.Decision == DecisionType.Rejected)
            await NotifyEmployeeInAppAsync(subject, ApprovalStatus.Rejected, ct);
    }

    public override async Task<PendingApprovalItemDto> ToPendingItemAsync(
        PayrollPeriodSubject subject,
        List<ApprovalStepDto> steps,
        bool canApprove,
        CancellationToken ct)
    {
        var deptName = string.IsNullOrWhiteSpace(subject.DeptCode)
            ? string.Empty
            : await _uow.Repository<F03Department>().Query().AsNoTracking()
                .Where(x => x.DeptCode == subject.DeptCode)
                .Select(x => x.DeptName)
                .FirstOrDefaultAsync(ct) ?? string.Empty;

        var inputCount = await _uow.Repository<F03PayrollInput>().Query().AsNoTracking()
            .CountAsync(x => x.PayrollPeriodId == subject.RequestId && x.IsActive != false, ct);

        return new PendingApprovalItemDto
        {
            RequestId = subject.RequestId,
            Kind = RequestType,
            EmployeeCode = subject.EmployeeCode,
            EmployeeName = $"Kỳ lương {subject.PeriodCode}",
            DeptCode = subject.DeptCode ?? string.Empty,
            DeptName = deptName,
            FromDate = subject.FromDate.ToDateTime(TimeOnly.MinValue),
            ToDate = subject.ToDate.ToDateTime(TimeOnly.MinValue),
            TotalUnits = inputCount,
            ApprovalSteps = steps,
            CanApprove = canApprove
        };
    }

    private async Task<ApprovalStatus> CalculateStatusAsync(int requestId, CancellationToken ct)
    {
        var snapshot = await _uow.Repository<F03ApprovalSnapshot>().Query().AsNoTracking()
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.RequestId == requestId && x.RequestType == RequestType, ct);
        if (snapshot == null) return ApprovalStatus.Pending;

        var histories = await _uow.Repository<F03ApprovalHistory>().Query().AsNoTracking()
            .Where(x => x.RequestId == requestId && x.RequestType == RequestType)
            .ToListAsync(ct);

        var required = snapshot.Steps.Where(x => x.IsRequired).ToList();
        if (required.Count == 0) return ApprovalStatus.Pending;
        if (required.Any(step => histories.Any(h => h.StepId == step.Id && h.Decision == DecisionType.Rejected)))
            return ApprovalStatus.Rejected;
        if (required.All(step => histories.Any(h => h.StepId == step.Id && h.Decision == DecisionType.Approved)))
            return ApprovalStatus.Approved;
        return histories.Count > 0 ? ApprovalStatus.InProgress : ApprovalStatus.Pending;
    }
}
