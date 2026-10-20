using System.Linq.Expressions;
using FVN_REGISTER.Application.Interfaces.Histories;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Histories;
using FVN_REGISTER.Core.Entities.LateEarly;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Histories;

public sealed class LateEarlyHistoryHandler : BaseHistoryHandler<F03LateEarlyRequest>
{
    protected override RequestModule ModuleKind => RequestModule.LateEarly;

    public LateEarlyHistoryHandler(FVNWEBAPPContext db) : base(db) { }

    protected override Expression<Func<F03LateEarlyRequest, bool>> BuildIdPredicate(int id)
        => x => x.Id == id;

    public override async Task<ServiceResult<PaginationResult<HistoryItemDto>>> GetHistoryAsync(
        HistoryFilterDto filter, UserIdentityDto user, CancellationToken ct)
    {
        var query = from request in Db.LateEarlyRequests.AsNoTracking()
                    join employee in Db.Employees.AsNoTracking()
                        on request.EmployeeCode equals employee.EmployeeCode into employees
                    from employee in employees.DefaultIfEmpty()
                    where request.IsActive == true
                        && (request.EmployeeCode == user.EmployeeCode
                            || request.RegisteredByEmployeeCode == user.EmployeeCode)
                    select new { request, employee };

        if (filter.Year.HasValue)
            query = query.Where(x => x.request.WorkDate.Year == filter.Year.Value);
        if (!string.IsNullOrWhiteSpace(filter.Status)
            && Enum.TryParse<ApprovalStatus>(filter.Status, true, out var status))
            query = query.Where(x => x.request.RequestStatus == status);
        if (filter.FromDate.HasValue)
            query = query.Where(x => x.request.WorkDate >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue)
            query = query.Where(x => x.request.WorkDate <= filter.ToDate.Value.Date);
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var term = filter.SearchText.Trim().ToLower();
            query = query.Where(x => x.request.EmployeeCode.ToLower().Contains(term)
                || x.request.ReasonCode.ToLower().Contains(term)
                || (x.request.ReasonNote != null && x.request.ReasonNote.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.request.WorkDate)
            .ThenByDescending(x => x.request.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        var ids = rows.Select(x => x.request.Id).ToList();
        var steps = await GetApprovalStepsMapAsync(ids, ct);

        var items = rows.Select(x => new HistoryItemDto
        {
            Id = x.request.Id,
            Kind = RequestModule.LateEarly,
            EmployeeCode = x.request.EmployeeCode,
            EmployeeName = x.employee?.EmployeeName ?? string.Empty,
            DeptCode = x.request.DeptCode,
            SubmittedAt = x.request.CreatedAt,
            RequestStatus = x.request.RequestStatus.ToString(),
            StatusDisplay = x.request.RequestStatus.ToDisplayName(),
            StatusColor = StatusColor(x.request.RequestStatus),
            Reason = $"{(x.request.LateEarlyType == LateEarlyType.LateArrival ? "Đi muộn" : "Về sớm")} · {x.request.PlannedTime:hh\\:mm} · {x.request.ReasonCode} {x.request.ReasonNote}",
            StartDate = x.request.WorkDate,
            EndDate = x.request.WorkDate,
            CanCancel = x.request.RequestStatus is ApprovalStatus.Draft or ApprovalStatus.Pending or ApprovalStatus.NeedsRevision,
            ApprovalSteps = steps.GetValueOrDefault(x.request.Id, new())
        }).ToList();

        return ServiceResult<PaginationResult<HistoryItemDto>>.Ok(
            new PaginationResult<HistoryItemDto>(items, total, filter.Page, filter.PageSize));
    }

    public override async Task<ServiceResult<HistoryItemDetailDto>> GetDetailAsync(
        int id, UserIdentityDto user, CancellationToken ct)
    {
        var row = await (from request in Db.LateEarlyRequests.AsNoTracking()
                         join employee in Db.Employees.AsNoTracking()
                            on request.EmployeeCode equals employee.EmployeeCode into employees
                         from employee in employees.DefaultIfEmpty()
                         where request.Id == id && request.IsActive == true
                            && (request.EmployeeCode == user.EmployeeCode
                                || request.RegisteredByEmployeeCode == user.EmployeeCode)
                         select new { request, employee }).FirstOrDefaultAsync(ct);

        if (row == null)
            return ServiceResult<HistoryItemDetailDto>.Fail("Không tìm thấy đăng ký đi muộn/về sớm.");

        return ServiceResult<HistoryItemDetailDto>.Ok(new HistoryItemDetailDto
        {
            Id = id,
            Kind = RequestModule.LateEarly.ToString(),
            RequestStatus = row.request.RequestStatus.ToString(),
            StatusDisplay = row.request.RequestStatus.ToDisplayName(),
            StatusColor = StatusColor(row.request.RequestStatus),
            SubmittedAt = row.request.CreatedAt,
            CanCancel = row.request.RequestStatus is ApprovalStatus.Draft or ApprovalStatus.Pending or ApprovalStatus.NeedsRevision,
            ApprovalSteps = await GetApprovalStepsAsync(id, ct)
        });
    }

    public override Task<ServiceResult<BalanceSummaryDto>> GetBalanceAsync(
        int year, UserIdentityDto user, CancellationToken ct)
        => Task.FromResult(ServiceResult<BalanceSummaryDto>.Ok(new BalanceSummaryDto
        {
            Kind = RequestModule.LateEarly,
            Year = year,
            Entitled = 0,
            Used = 0,
            Remaining = 0,
            PendingCount = 0
        }));

    private static string StatusColor(ApprovalStatus status) => status switch
    {
        ApprovalStatus.Approved => "success",
        ApprovalStatus.Rejected => "error",
        ApprovalStatus.Cancelled => "default",
        ApprovalStatus.InProgress or ApprovalStatus.Escalated => "info",
        _ => "warning"
    };
}
