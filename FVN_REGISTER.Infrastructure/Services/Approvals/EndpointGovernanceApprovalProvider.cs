using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Application.Interfaces.Emails;
using FVN_REGISTER.Application.Interfaces.Notifications;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Models.Subjects;
using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Contract.Dtos.ApprovelSnapshotDto;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Entities.Security;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Repositories;
using FVN_REGISTER.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.Infrastructure.Services.Approvals;

public sealed class EndpointGovernanceApprovalProvider
    : BaseApprovalProvider<EndpointGovernanceRequestSubject, EndpointGovernanceApprovalProvider>, IApprovalProvider<EndpointGovernanceRequestSubject>
{
    public override RequestModule RequestType => RequestModule.Endpoint;

    public EndpointGovernanceApprovalProvider(IUnitOfWork uow, IEmailService email, IApprovalNotificationService notification, IEmployeeUserResolver userResolver, IApprovalRouteService routeService, IApprovalSelectionService selectionService, ILogger<EndpointGovernanceApprovalProvider> logger, IOptionsMonitor<AuthDebugOptions> options)
        : base(uow, email, notification, userResolver, routeService, selectionService, logger, options) { }

    public override async Task<EndpointGovernanceRequestSubject?> GetSubjectAsync(int requestId, CancellationToken ct)
    {
        var entity = await _uow.Repository<F03EndpointGovernanceRequest>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == requestId && x.IsActive == true, ct);
        if (entity == null) return null;
        var employee = await _uow.Repository<F03Employee>().Query().AsNoTracking().Where(x => x.EmployeeCode == entity.EmployeeCode).Select(x => new { x.EmployeeName, x.PositionCode }).FirstOrDefaultAsync(ct);
        return EndpointGovernanceRequestSubject.From(entity, employee?.EmployeeName, employee?.PositionCode);
    }

    public override async Task<List<EndpointGovernanceRequestSubject>> GetSubjectsAsync(List<int> requestIds, CancellationToken ct)
    {
        var rows = await _uow.Repository<F03EndpointGovernanceRequest>().Query().AsNoTracking().Where(x => requestIds.Contains(x.Id) && x.IsActive == true).ToListAsync(ct);
        var employeeCodes = rows.Select(x => x.EmployeeCode).Distinct().ToList();
        var employees = await _uow.Repository<F03Employee>().Query().AsNoTracking().Where(x => employeeCodes.Contains(x.EmployeeCode)).Select(x => new { x.EmployeeCode, x.EmployeeName, x.PositionCode }).ToDictionaryAsync(x => x.EmployeeCode, x => x, ct);
        return rows.Select(row => { employees.TryGetValue(row.EmployeeCode, out var employee); return EndpointGovernanceRequestSubject.From(row, employee?.EmployeeName, employee?.PositionCode); }).ToList();
    }

    public override async Task ApplyOverallStatusAsync(int requestId, IReadOnlyList<ApprovalStepDto> allSteps, CancellationToken ct)
    {
        var entity = await _uow.Repository<F03EndpointGovernanceRequest>().Query().FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (entity == null) return;
        var required = allSteps.Where(x => x.IsRequired).ToList();
        entity.RequestStatus = required.Any(x => x.Decision == DecisionType.Rejected) ? ApprovalStatus.Rejected : required.Count > 0 && required.All(x => x.Decision == DecisionType.Approved) ? ApprovalStatus.Approved : required.Any(x => x.Decision == DecisionType.Approved) ? ApprovalStatus.InProgress : ApprovalStatus.Pending;
        entity.WorkflowStatus = entity.RequestStatus switch { ApprovalStatus.Approved => "Approved", ApprovalStatus.Rejected => "Rejected", ApprovalStatus.InProgress => "InProgress", _ => "PendingApproval" };
        await _uow.SaveChangesAsync(ct);
    }

    public override async Task NotifyStepCompletedAsync(EndpointGovernanceRequestSubject subject, ApprovalStepDto completedStep, bool isFullyApproved, CancellationToken ct)
    {
        if (!isFullyApproved && completedStep.Decision != DecisionType.Rejected) return;
        var email = await _uow.Repository<F03Employee>().Query().AsNoTracking().Where(x => x.EmployeeCode == subject.EmployeeCode).Select(x => x.EmailAddress).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(email)) await _email.QueueEmail(email, isFullyApproved ? "ENDPOINT_GOVERNANCE_APPROVED" : "ENDPOINT_GOVERNANCE_REJECTED", new { subject.RequestId, subject.RequestType, subject.EndpointDeviceId, subject.ItemName, Status = isFullyApproved ? "Approved" : "Rejected" }, ct);
        await NotifyEmployeeInAppAsync(subject, isFullyApproved ? ApprovalStatus.Approved : ApprovalStatus.Rejected, ct);
    }

    public override async Task<PendingApprovalItemDto> ToPendingItemAsync(EndpointGovernanceRequestSubject subject, List<ApprovalStepDto> steps, bool canApprove, CancellationToken ct)
    {
        var deptName = await _uow.Repository<F03Department>().Query().AsNoTracking().Where(x => x.DeptCode == subject.DeptCode).Select(x => x.DeptName).FirstOrDefaultAsync(ct);
        return new PendingApprovalItemDto { RequestId = subject.RequestId, Kind = subject.Module, EmployeeCode = subject.EmployeeCode, EmployeeName = subject.EmployeeName ?? string.Empty, DeptCode = subject.DeptCode ?? 0, DeptName = deptName ?? string.Empty, FromDate = DateTime.Today, ToDate = DateTime.Today, TotalUnits = 1, ApprovalSteps = steps, CanApprove = canApprove };
    }
}
