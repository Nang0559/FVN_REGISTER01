using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Application.Interfaces.Orchestrators;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.FeatureOperators;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Models.Subjects;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Entities.Security;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Security;

public sealed class EndpointGovernanceService : IEndpointGovernanceService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IApprovalWorkflowOrchestrator<EndpointGovernanceRequestSubject> _workflow;
    private readonly IFeatureOperatorAssignmentService _operators;

    public EndpointGovernanceService(IUnitOfWork uow, ICurrentUserService currentUser, IAuthorizationService authorization, IApprovalWorkflowOrchestrator<EndpointGovernanceRequestSubject> workflow, IFeatureOperatorAssignmentService operators)
    {
        _uow = uow;
        _currentUser = currentUser;
        _authorization = authorization;
        _workflow = workflow;
        _operators = operators;
    }

    public async Task<ServiceResult<IReadOnlyList<EndpointGovernancePolicyDto>>> GetPoliciesAsync(EndpointGovernanceItemType? itemType, EndpointTargetType? targetType, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            if (!await HasCatalogViewAsync(user, itemType, ct)) throw new UnauthorizedAccessException("Bạn không có quyền xem Endpoint Governance catalog.");
            var query = _uow.Repository<F03EndpointGovernancePolicy>().Query().AsNoTracking();
            if (itemType.HasValue) query = query.Where(x => x.ItemType == itemType.Value);
            if (targetType.HasValue) query = query.Where(x => x.TargetType == targetType.Value);
            var rows = await query.OrderBy(x => x.ItemType).ThenBy(x => x.TargetType).ThenByDescending(x => x.Version).ToListAsync(ct);
            var ids = rows.Select(x => x.Id).ToList();
            var items = await _uow.Repository<F03EndpointGovernancePolicyItem>().Query().AsNoTracking().Where(x => ids.Contains(x.PolicyId)).ToListAsync(ct);
            return ServiceResult<IReadOnlyList<EndpointGovernancePolicyDto>>.Ok(rows.Select(x => MapPolicy(x, items.Where(i => i.PolicyId == x.Id))).ToList());
        }
        catch (UnauthorizedAccessException ex) { return ServiceResult<IReadOnlyList<EndpointGovernancePolicyDto>>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<EndpointGovernancePolicyDto>> GetPolicyAsync(int policyId, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            var entity = await _uow.Repository<F03EndpointGovernancePolicy>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == policyId, ct);
            if (entity == null) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Không tìm thấy Endpoint Governance policy.");
            if (!await HasCatalogViewAsync(user, entity.ItemType, ct)) throw new UnauthorizedAccessException("Bạn không có quyền xem policy này.");
            var items = await _uow.Repository<F03EndpointGovernancePolicyItem>().Query().AsNoTracking().Where(x => x.PolicyId == entity.Id).ToListAsync(ct);
            return ServiceResult<EndpointGovernancePolicyDto>.Ok(MapPolicy(entity, items));
        }
        catch (UnauthorizedAccessException ex) { return ServiceResult<EndpointGovernancePolicyDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<EndpointGovernancePolicyDto>> CreateVersionAsync(EndpointGovernancePolicyUpsertRequest request, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            await EnsureCatalogManageAsync(user, request.ItemType, ct);
            if (string.IsNullOrWhiteSpace(request.PolicyCode) || string.IsNullOrWhiteSpace(request.PolicyName)) return ServiceResult<EndpointGovernancePolicyDto>.Fail("PolicyCode và PolicyName là bắt buộc.");
            if (request.Items.Count == 0) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Catalog phải có ít nhất một item.");
            if (!request.ChecklistCompleted) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Catalog phải hoàn thành checklist kiểm tra trước khi lưu version.");
            var normalized = request.Items.Where(x => !string.IsNullOrWhiteSpace(x.NormalizedName)).Select(x => Normalize(x.NormalizedName)).ToList();
            if (normalized.Count != normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count()) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Catalog không được có item trùng sau chuẩn hóa tên.");
            var code = request.PolicyCode.Trim();
            var previous = await _uow.Repository<F03EndpointGovernancePolicy>().Query().Where(x => x.PolicyCode == code).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            var now = DateTime.UtcNow;
            var entity = new F03EndpointGovernancePolicy
            {
                PolicyCode = code,
                PolicyName = request.PolicyName.Trim(),
                ItemType = request.ItemType,
                TargetType = request.TargetType,
                Version = (previous?.Version ?? 0) + 1,
                IsPublished = false,
                WorkflowStatus = "Draft",
                EffectiveFromUtc = now,
                Remark = request.Remark?.Trim(),
                ChecklistCompleted = true,
                ChecklistCompletedAtUtc = now,
                ChecklistCompletedBy = user.UserId,
                ChecklistNote = request.ChecklistNote?.Trim(),
                CreatedBy = user.UserId,
                LastModifiedSource = "EndpointGovernanceService"
            };
            await _uow.Repository<F03EndpointGovernancePolicy>().AddAsync(entity, ct);
            await _uow.SaveChangesAsync(ct);
            foreach (var item in request.Items.Where(x => !string.IsNullOrWhiteSpace(x.NormalizedName)))
            {
                await _uow.Repository<F03EndpointGovernancePolicyItem>().AddAsync(new F03EndpointGovernancePolicyItem
                {
                    PolicyId = entity.Id,
                    ItemType = request.ItemType,
                    NormalizedName = Normalize(item.NormalizedName),
                    DisplayName = item.DisplayName?.Trim(),
                    Publisher = item.Publisher?.Trim(),
                    VersionConstraint = item.VersionConstraint?.Trim(),
                    IsAllowed = item.IsAllowed,
                    Remark = item.Remark?.Trim(),
                    CreatedBy = user.UserId,
                    LastModifiedSource = "EndpointGovernanceService"
                }, ct);
            }
            await _uow.SaveChangesAsync(ct);
            var items = await _uow.Repository<F03EndpointGovernancePolicyItem>().Query().AsNoTracking().Where(x => x.PolicyId == entity.Id).ToListAsync(ct);
            return ServiceResult<EndpointGovernancePolicyDto>.Ok(MapPolicy(entity, items));
        }
        catch (UnauthorizedAccessException ex) { return ServiceResult<EndpointGovernancePolicyDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<EndpointGovernancePolicyDto>> SubmitPolicyAsync(int policyId, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            var policy = await _uow.Repository<F03EndpointGovernancePolicy>().Query().FirstOrDefaultAsync(x => x.Id == policyId, ct);
            if (policy == null) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Không tìm thấy policy.");
            await EnsureCatalogSubmitAsync(user, policy.ItemType, ct);
            if (policy.IsPublished) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Policy đã publish, không thể submit lại.");
            if (!policy.ChecklistCompleted) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Catalog chưa hoàn thành checklist kiểm tra.");
            if (!string.Equals(policy.WorkflowStatus, "Draft", StringComparison.OrdinalIgnoreCase) && !string.Equals(policy.WorkflowStatus, "Rejected", StringComparison.OrdinalIgnoreCase)) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Policy không ở trạng thái có thể submit.");
            if (!await _uow.Repository<F03EndpointGovernancePolicyItem>().Query().AnyAsync(x => x.PolicyId == policy.Id && x.IsAllowed, ct)) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Policy phải có ít nhất một item được phép.");
            var employee = await _uow.Repository<F03Employee>().Query().AsNoTracking().Where(x => x.EmployeeCode == user.EmployeeCode).Select(x => new { x.EmployeeCode, x.DeptCode, x.PositionCode }).FirstOrDefaultAsync(ct);
            if (employee == null || string.IsNullOrWhiteSpace(employee.PositionCode)) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Không xác định được PositionCode để dựng Approval route.");
            var requestType = policy.ItemType == EndpointGovernanceItemType.WindowsService ? EndpointGovernanceRequestType.ServicePolicyChange : EndpointGovernanceRequestType.SoftwarePolicyChange;
            var approvalRequest = new F03EndpointGovernanceRequest
            {
                EmployeeCode = employee.EmployeeCode,
                DeptCode = employee.DeptCode,
                RequestStatus = ApprovalStatus.Draft,
                RequestType = requestType,
                PolicyId = policy.Id,
                PolicyVersion = policy.Version,
                PolicyItemType = policy.ItemType,
                ItemName = policy.PolicyName,
                Reason = $"Submit Endpoint Governance catalog {policy.PolicyCode} v{policy.Version}",
                SecurityReviewStatus = "NotRequired",
                WorkflowStatus = "PendingApproval",
                CreatedBy = user.UserId,
                LastModifiedSource = "EndpointGovernanceService"
            };
            await _uow.Repository<F03EndpointGovernanceRequest>().AddAsync(approvalRequest, ct);
            await _uow.SaveChangesAsync(ct);
            var context = ApprovalBuildContext.ForEndpoint(approvalRequest.Id, employee.EmployeeCode, employee.DeptCode ?? 0, employee.PositionCode, requestType.ToString(), policy.PolicyName);
            var init = await _workflow.InitApprovalAsync(approvalRequest.Id, context, ct);
            if (!init.IsSuccess) return ServiceResult<EndpointGovernancePolicyDto>.Fail(init.Message ?? "Không thể khởi tạo Common Approval cho catalog.");
            approvalRequest.RequestStatus = ApprovalStatus.Pending;
            policy.ApprovalRequestId = approvalRequest.Id;
            policy.WorkflowStatus = "PendingApproval";
            policy.SubmittedAtUtc = DateTime.UtcNow;
            policy.SubmittedBy = user.UserId;
            policy.ModifiedBy = user.UserId;
            policy.LastModifiedSource = "EndpointGovernanceService";
            await _uow.SaveChangesAsync(ct);
            var items = await _uow.Repository<F03EndpointGovernancePolicyItem>().Query().AsNoTracking().Where(x => x.PolicyId == policy.Id).ToListAsync(ct);
            return ServiceResult<EndpointGovernancePolicyDto>.Ok(MapPolicy(policy, items));
        }
        catch (UnauthorizedAccessException ex) { return ServiceResult<EndpointGovernancePolicyDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<EndpointGovernancePolicyDto>> PublishAsync(int policyId, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            var entity = await _uow.Repository<F03EndpointGovernancePolicy>().Query().FirstOrDefaultAsync(x => x.Id == policyId, ct);
            if (entity == null) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Không tìm thấy policy.");
            await EnsureCatalogApproveAsync(user, entity.ItemType, ct);
            if (entity.IsPublished) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Policy đã được publish.");
            if (!entity.ChecklistCompleted) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Catalog chưa hoàn thành checklist kiểm tra.");
            if (!entity.ApprovalRequestId.HasValue) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Policy chưa được submit qua Common Approval.");
            var approval = await _uow.Repository<F03EndpointGovernanceRequest>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.ApprovalRequestId.Value, ct);
            if (approval == null || approval.RequestStatus != ApprovalStatus.Approved) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Policy chưa được Common Approval phê duyệt.");
            if (!await _uow.Repository<F03EndpointGovernancePolicyItem>().Query().AnyAsync(x => x.PolicyId == entity.Id && x.IsAllowed, ct)) return ServiceResult<EndpointGovernancePolicyDto>.Fail("Policy phải có ít nhất một item được phép trước khi publish.");
            var previous = await _uow.Repository<F03EndpointGovernancePolicy>().Query().Where(x => x.Id != entity.Id && x.ItemType == entity.ItemType && x.TargetType == entity.TargetType && x.IsPublished).ToListAsync(ct);
            foreach (var row in previous)
            {
                row.IsPublished = false;
                row.EffectiveToUtc = DateTime.UtcNow;
                row.ModifiedBy = user.UserId;
                row.LastModifiedSource = "EndpointGovernanceService";
            }
            entity.IsPublished = true;
            entity.WorkflowStatus = "Published";
            entity.EffectiveFromUtc = DateTime.UtcNow;
            entity.EffectiveToUtc = null;
            entity.ModifiedBy = user.UserId;
            entity.LastModifiedSource = "EndpointGovernanceService";
            await _uow.SaveChangesAsync(ct);
            var items = await _uow.Repository<F03EndpointGovernancePolicyItem>().Query().AsNoTracking().Where(x => x.PolicyId == entity.Id).ToListAsync(ct);
            return ServiceResult<EndpointGovernancePolicyDto>.Ok(MapPolicy(entity, items));
        }
        catch (UnauthorizedAccessException ex) { return ServiceResult<EndpointGovernancePolicyDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<EndpointGovernanceRequestDto>> CreateRequestAsync(EndpointGovernanceRequestCreateDto request, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            var capability = request.RequestType switch
            {
                EndpointGovernanceRequestType.InstallSoftware => SecurityFunctionCodes.EndpointInstallRequestCreate,
                EndpointGovernanceRequestType.Exception => SecurityFunctionCodes.EndpointExceptionCreate,
                EndpointGovernanceRequestType.ServiceChange => SecurityFunctionCodes.EndpointServicePolicySubmit,
                _ => SecurityFunctionCodes.EndpointSoftwarePolicySubmit
            };
            if (!await _authorization.HasAsync(user, capability, ct)) throw new UnauthorizedAccessException("Bạn không có quyền tạo yêu cầu Endpoint Governance.");
            if (request.RequestType is EndpointGovernanceRequestType.SoftwarePolicyChange or EndpointGovernanceRequestType.ServicePolicyChange) return ServiceResult<EndpointGovernanceRequestDto>.Fail("Catalog change phải đi qua SubmitPolicyAsync.");
            if (string.IsNullOrWhiteSpace(request.Reason)) return ServiceResult<EndpointGovernanceRequestDto>.Fail("Lý do yêu cầu là bắt buộc.");
            if (!request.EndpointDeviceId.HasValue) return ServiceResult<EndpointGovernanceRequestDto>.Fail("EndpointDeviceId là bắt buộc đối với request thiết bị.");
            var device = await _uow.Repository<F03EndpointDevice>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.EndpointDeviceId.Value, ct);
            if (device == null) return ServiceResult<EndpointGovernanceRequestDto>.Fail("Thiết bị không tồn tại.");
            if (!user.IsAdmin && !string.Equals(device.EmployeeCode, user.EmployeeCode, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Bạn không có quyền tạo yêu cầu cho thiết bị này.");
            if (request.PolicyItemId.HasValue)
            {
                var itemExists = await _uow.Repository<F03EndpointGovernancePolicyItem>().Query().AnyAsync(x => x.Id == request.PolicyItemId.Value && x.IsActive == true && x.IsAllowed, ct);
                if (!itemExists) return ServiceResult<EndpointGovernanceRequestDto>.Fail("Policy item không tồn tại hoặc không còn được phép.");
            }
            var entity = new F03EndpointGovernanceRequest
            {
                EmployeeCode = user.EmployeeCode ?? string.Empty,
                DeptCode = user.DeptCode,
                RequestStatus = ApprovalStatus.Draft,
                RequestType = request.RequestType,
                EndpointDeviceId = device.Id,
                PolicyId = request.PolicyId,
                PolicyItemId = request.PolicyItemId,
                ItemName = request.ItemName?.Trim(),
                Publisher = request.Publisher?.Trim(),
                RequestedVersion = request.RequestedVersion?.Trim(),
                Reason = request.Reason.Trim(),
                SecurityReviewStatus = "Pending",
                WorkflowStatus = "PendingSecurityReview",
                CreatedBy = user.UserId,
                LastModifiedSource = "EndpointGovernanceService"
            };
            await _uow.Repository<F03EndpointGovernanceRequest>().AddAsync(entity, ct);
            await _uow.SaveChangesAsync(ct);
            return ServiceResult<EndpointGovernanceRequestDto>.Ok(MapRequest(entity));
        }
        catch (UnauthorizedAccessException ex) { return ServiceResult<EndpointGovernanceRequestDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<IReadOnlyList<EndpointGovernanceRequestDto>>> GetRequestsAsync(bool mineOnly, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            var query = _uow.Repository<F03EndpointGovernanceRequest>().Query().AsNoTracking().Where(x => x.IsActive == true);
            if (mineOnly) query = query.Where(x => x.EmployeeCode == user.EmployeeCode);
            else if (!user.IsAdmin && !await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointInstallRequestView, ct)) throw new UnauthorizedAccessException("Bạn không có quyền xem danh sách Endpoint requests.");
            var rows = await query.OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync(ct);
            return ServiceResult<IReadOnlyList<EndpointGovernanceRequestDto>>.Ok(rows.Select(MapRequest).ToList());
        }
        catch (UnauthorizedAccessException ex) { return ServiceResult<IReadOnlyList<EndpointGovernanceRequestDto>>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<EndpointGovernanceRequestDto>> ReviewAsync(int requestId, bool approved, string? comment, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            var entity = await _uow.Repository<F03EndpointGovernanceRequest>().Query().FirstOrDefaultAsync(x => x.Id == requestId && x.IsActive == true, ct);
            if (entity == null) return ServiceResult<EndpointGovernanceRequestDto>.Fail("Không tìm thấy request.");
            if (entity.RequestType is EndpointGovernanceRequestType.SoftwarePolicyChange or EndpointGovernanceRequestType.ServicePolicyChange) return ServiceResult<EndpointGovernanceRequestDto>.Fail("Catalog approval phải thực hiện qua Common Approval.");
            var reviewCapability = entity.RequestType == EndpointGovernanceRequestType.ServiceChange ? SecurityFunctionCodes.EndpointServiceSecurityReview : entity.RequestType == EndpointGovernanceRequestType.Exception ? SecurityFunctionCodes.EndpointExceptionApprove : SecurityFunctionCodes.EndpointSoftwareSecurityReview;
            if (!await _authorization.HasAsync(user, reviewCapability, ct)) throw new UnauthorizedAccessException("Bạn không có quyền Security Review request này.");
            if (!string.Equals(entity.SecurityReviewStatus, "Pending", StringComparison.OrdinalIgnoreCase)) return ServiceResult<EndpointGovernanceRequestDto>.Fail("Request không còn ở trạng thái chờ Security Review.");
            entity.SecurityReviewStatus = approved ? "Approved" : "Rejected";
            entity.WorkflowStatus = approved ? "PendingApproval" : "Rejected";
            entity.RequestStatus = approved ? ApprovalStatus.Pending : ApprovalStatus.Rejected;
            entity.ReviewedAtUtc = DateTime.UtcNow;
            entity.ReviewedBy = user.UserId;
            entity.LastModifiedSource = "EndpointGovernanceService";
            if (!string.IsNullOrWhiteSpace(comment)) entity.Reason = $"{entity.Reason}\n[Security Review] {comment.Trim()}";
            await _uow.SaveChangesAsync(ct);
            if (approved)
            {
                var employee = await _uow.Repository<F03Employee>().Query().AsNoTracking().Where(x => x.EmployeeCode == entity.EmployeeCode).Select(x => new { x.EmployeeCode, x.DeptCode, x.PositionCode }).FirstOrDefaultAsync(ct);
                if (employee == null || string.IsNullOrWhiteSpace(employee.PositionCode)) return ServiceResult<EndpointGovernanceRequestDto>.Fail("Không xác định được PositionCode để dựng Approval route.");
                var steps = await _workflow.GetStepsAsync(entity.Id, ct);
                if (steps.Count == 0)
                {
                    var context = ApprovalBuildContext.ForEndpoint(entity.Id, entity.EmployeeCode, employee.DeptCode ?? 0, employee.PositionCode, entity.RequestType.ToString(), entity.ItemName);
                    var init = await _workflow.InitApprovalAsync(entity.Id, context, ct);
                    if (!init.IsSuccess) return ServiceResult<EndpointGovernanceRequestDto>.Fail(init.Message ?? "Không thể khởi tạo Common Approval.");
                }
            }
            return ServiceResult<EndpointGovernanceRequestDto>.Ok(MapRequest(entity));
        }
        catch (UnauthorizedAccessException ex) { return ServiceResult<EndpointGovernanceRequestDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<IReadOnlyList<EndpointComplianceFindingDto>>> GetFindingsAsync(long? endpointDeviceId, bool openOnly, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            if (!await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointComplianceView, ct)) throw new UnauthorizedAccessException("Bạn không có quyền xem Endpoint Compliance.");
            var query = _uow.Repository<F03EndpointComplianceFinding>().Query().AsNoTracking();
            if (endpointDeviceId.HasValue) query = query.Where(x => x.EndpointDeviceId == endpointDeviceId.Value);
            if (openOnly) query = query.Where(x => x.ResolvedAtUtc == null);
            var rows = await query.OrderByDescending(x => x.EvaluatedAtUtc).Take(1000).ToListAsync(ct);
            return ServiceResult<IReadOnlyList<EndpointComplianceFindingDto>>.Ok(rows.Select(MapFinding).ToList());
        }
        catch (UnauthorizedAccessException ex) { return ServiceResult<IReadOnlyList<EndpointComplianceFindingDto>>.Fail(ex.Message); }
    }

    private async Task<bool> HasCatalogViewAsync(UserIdentityDto user, EndpointGovernanceItemType? itemType, CancellationToken ct)
    {
        if (user.IsAdmin) return true;
        if (itemType == EndpointGovernanceItemType.WindowsService) return await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointServiceCatalogView, ct);
        if (itemType == EndpointGovernanceItemType.Software) return await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointSoftwareCatalogView, ct);
        return await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointSoftwareCatalogView, ct) || await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointServiceCatalogView, ct);
    }

    private async Task EnsureCatalogManageAsync(UserIdentityDto user, EndpointGovernanceItemType itemType, CancellationToken ct)
    {
        if (user.IsAdmin) return;
        var code = itemType == EndpointGovernanceItemType.WindowsService ? SecurityFunctionCodes.EndpointServiceCatalogManage : SecurityFunctionCodes.EndpointSoftwareCatalogManage;
        if (!await _authorization.HasAsync(user, code, ct)) throw new UnauthorizedAccessException("Bạn không có quyền quản trị Endpoint catalog.");

        // Software Catalog Manage may additionally be restricted to specifically
        // designated operators. When no operator assignment exists, the existing
        // RBAC behavior remains effective for backward compatibility.
        if (itemType == EndpointGovernanceItemType.Software &&
            !await _operators.CanOperateAsync(
                user.UserId,
                user.EmployeeCode,
                SecurityFunctionCodes.EndpointSoftwareCatalogManage,
                "ENDPOINT_SOFTWARE_CATALOG",
                null,
                ct))
            throw new UnauthorizedAccessException("Bạn chưa được chỉ định quyền quản lý danh sách phần mềm Endpoint.");
    }

    private async Task EnsureCatalogSubmitAsync(UserIdentityDto user, EndpointGovernanceItemType itemType, CancellationToken ct)
    {
        if (user.IsAdmin) return;
        var code = itemType == EndpointGovernanceItemType.WindowsService ? SecurityFunctionCodes.EndpointServicePolicySubmit : SecurityFunctionCodes.EndpointSoftwarePolicySubmit;
        if (!await _authorization.HasAsync(user, code, ct)) throw new UnauthorizedAccessException("Bạn không có quyền submit Endpoint catalog.");
    }

    private async Task EnsureCatalogApproveAsync(UserIdentityDto user, EndpointGovernanceItemType itemType, CancellationToken ct)
    {
        if (user.IsAdmin) return;
        var code = itemType == EndpointGovernanceItemType.WindowsService ? SecurityFunctionCodes.EndpointServicePolicyApprove : SecurityFunctionCodes.EndpointSoftwarePolicyApprove;
        if (!await _authorization.HasAsync(user, code, ct)) throw new UnauthorizedAccessException("Bạn không có quyền publish catalog đã được phê duyệt.");
    }

    private UserIdentityDto RequireUser() => _currentUser.GetCurrentUser() ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static EndpointGovernancePolicyDto MapPolicy(F03EndpointGovernancePolicy entity, IEnumerable<F03EndpointGovernancePolicyItem> items)
        => new(entity.Id, entity.PolicyCode, entity.PolicyName, entity.ItemType, entity.TargetType, entity.Version, entity.IsPublished, entity.WorkflowStatus, entity.ApprovalRequestId, entity.EffectiveFromUtc, entity.EffectiveToUtc, items.Select(x => new EndpointGovernancePolicyItemDto(x.Id, x.ItemType, x.NormalizedName, x.DisplayName, x.Publisher, x.VersionConstraint, x.IsAllowed, x.Remark)).ToList(), entity.Remark, entity.ChecklistCompleted, entity.ChecklistCompletedAtUtc, entity.ChecklistCompletedBy, entity.ChecklistNote);

    private static EndpointGovernanceRequestDto MapRequest(F03EndpointGovernanceRequest entity)
        => new(entity.Id, entity.RequestType, entity.EndpointDeviceId, entity.PolicyId, entity.PolicyVersion, entity.PolicyItemId, entity.PolicyItemType, entity.EmployeeCode, entity.DeptCode, entity.ItemName, entity.Publisher, entity.RequestedVersion, entity.Reason, entity.SecurityReviewStatus, entity.WorkflowStatus, (int)entity.RequestStatus, entity.ApprovalSnapshotId, entity.CreatedAt, entity.ReviewedAtUtc);

    private static EndpointComplianceFindingDto MapFinding(F03EndpointComplianceFinding entity)
        => new(entity.Id, entity.EndpointDeviceId, entity.PolicyId, entity.PolicyVersion, entity.PolicyItemId, entity.ItemType, entity.InventoryItemId, entity.Result, entity.ObservedName, entity.ObservedVersion, entity.FindingCode, entity.FindingMessage, entity.EvaluatedAtUtc, entity.ResolvedAtUtc);
}
