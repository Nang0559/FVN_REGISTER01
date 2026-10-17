using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Application.Interfaces.Common;
using FVN_REGISTER.Application.Interfaces.Histories;
using FVN_REGISTER.Application.Interfaces.OT;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.OT;
using FVN_REGISTER.Contract.Requests.OT;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Repositories;

namespace FVN_REGISTER.Infrastructure.Services.OT;

/// <summary>
/// Keeps the existing OT query implementation intact while applying registrar policy
/// to the employee picker used by OT creation. Non-registrars can only see themselves.
/// </summary>
public sealed class PolicyAwareOTQueryService : OTQueryService, IOTQueryService
{
    private readonly IOtRegistrationPolicy _policy;
    private readonly ICurrentUserService _currentUser;

    public PolicyAwareOTQueryService(
        IUnitOfWork uow,
        IApprovalHistoryService historyService,
        IAttachmentService attachmentService,
        ICurrentUserService currentUserService,
        IApprovalProvider<OTRequestSubject> approvalProvider,
        IAuthorizationService authorization,
        IOtRegistrationPolicy policy)
        : base(uow, historyService, attachmentService, currentUserService, approvalProvider, authorization)
    {
        _policy = policy;
        _currentUser = currentUserService;
    }

    public new async Task<List<OTEmployeeDto>> GetDeptEmployeesAsync(int deptCode, CancellationToken ct = default)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null)
            return new List<OTEmployeeDto>();

        if (await _policy.CanRegisterForOthersAsync(user, deptCode, "OT", ct))
            return await base.GetDeptEmployeesAsync(deptCode, ct);

        var ownCode = user.EmployeeCode;
        if (string.IsNullOrWhiteSpace(ownCode))
            return new List<OTEmployeeDto>();

        var employees = await base.GetDeptEmployeesAsync(deptCode, ct);
        return employees.Where(x => string.Equals(x.EmployeeCode, ownCode, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>Cùng rule với GetDeptEmployeesAsync: registrar tìm được nhân viên phòng ban; người khác chỉ tìm được chính mình.</summary>
    public new async Task<List<OTEmployeeDto>> SearchEmployeesAsync(
        int deptCode, string? keyword, IReadOnlyList<string>? codes, int take = 20, CancellationToken ct = default)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null)
            return new List<OTEmployeeDto>();

        if (await _policy.CanRegisterForOthersAsync(user, deptCode, "OT", ct))
            return await base.SearchEmployeesAsync(deptCode, keyword, codes, take, ct);

        var ownCode = user.EmployeeCode;
        if (string.IsNullOrWhiteSpace(ownCode))
            return new List<OTEmployeeDto>();

        var own = await base.SearchEmployeesAsync(deptCode, null, new[] { ownCode }, 1, ct);
        var text = keyword?.Trim();
        if (string.IsNullOrEmpty(text) && (codes == null || codes.Count == 0))
            return new List<OTEmployeeDto>();
        if (codes is { Count: > 0 })
            return own.Where(x => codes.Any(c => string.Equals(c?.Trim(), x.EmployeeCode, StringComparison.OrdinalIgnoreCase))).ToList();
        return own.Where(x => x.EmployeeCode.Contains(text!, StringComparison.OrdinalIgnoreCase)
                           || x.EmployeeName.Contains(text!, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
