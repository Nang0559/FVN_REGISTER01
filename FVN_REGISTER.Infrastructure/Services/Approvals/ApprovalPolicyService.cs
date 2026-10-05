using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Contract.Dtos.Depts;
using FVN_REGISTER.Contract.Requests.Approvals;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.Approvals;

public sealed class ApprovalPolicyService : IApprovalPolicyService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ApprovalPolicyService> _logger;

    public ApprovalPolicyService(IUnitOfWork uow, ILogger<ApprovalPolicyService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<ServiceResult<List<ApprovalPolicyDto>>> GetAllAsync(CancellationToken ct = default)
    {
        // Do not materialize F03ApprovalPolicy entities here. Older databases can contain
        // NULL LevelName/RoleName (and, in some installations, other legacy text fields)
        // while the current entity maps them as required strings. Projecting with SQL
        // COALESCE keeps this read endpoint compatible with those legacy rows.
        var policies = await _uow.Repository<F03ApprovalPolicy>().Query()
            .AsNoTracking()
            .OrderBy(x => x.RequestType)
            .ThenBy(x => x.DeptCode)
            .ThenBy(x => x.PositionCode)
            .ThenBy(x => x.Sequence)
            .ThenBy(x => x.Level)
            .Select(x => new ApprovalPolicyDto
            {
                Id = x.Id,
                IsActive = x.IsActive == true,
                RequestType = (int)x.RequestType,
                RequestTypeName = RequestTypeName(x.RequestType),
                DeptCode = x.DeptCode ?? string.Empty,
                PositionCode = x.PositionCode,
                ApprovalPositionCode = x.ApprovalPositionCode ?? string.Empty,
                Level = x.Level,
                Sequence = x.Sequence,
                LevelName = x.LevelName ?? string.Empty,
                RoleName = x.RoleName ?? string.Empty,
                Required = x.Required
            })
            .ToListAsync(ct);

        var departments = await _uow.Repository<F03Department>().Query()
            .AsNoTracking()
            .ToDictionaryAsync(x => x.DeptCode, x => x.DeptName, ct);

        var positions = await _uow.Repository<F03Position>().Query()
            .AsNoTracking()
            .ToDictionaryAsync(x => x.PositionCode, ct);

        foreach (var policy in policies)
        {
            positions.TryGetValue(policy.PositionCode ?? string.Empty, out var requesterPosition);
            positions.TryGetValue(policy.ApprovalPositionCode, out var approvalPosition);

            policy.DeptName = departments.GetValueOrDefault(policy.DeptCode, policy.DeptCode);
            policy.PositionName = requesterPosition?.PositionName ?? "(Tất cả vị trí)";
            policy.ApprovalPositionName = approvalPosition?.PositionName ?? policy.ApprovalPositionCode;
        }

        return ServiceResult<List<ApprovalPolicyDto>>.Ok(policies);
    }

    public async Task<ServiceResult<List<ApprovalPolicyPositionDto>>> GetPositionsAsync(CancellationToken ct = default)
    {
        var rows = await _uow.Repository<F03Position>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true)
            .OrderBy(x => x.DefaultApproveLevel == null)
            .ThenBy(x => x.DefaultApproveLevel)
            .ThenBy(x => x.PositionCode)
            .Select(x => new ApprovalPolicyPositionDto
            {
                PositionCode = x.PositionCode,
                PositionName = x.PositionName,
                DefaultApproveLevel = x.DefaultApproveLevel,
                RoleName = RoleNameFromPosition(x.DefaultApproveLevel),
                IsActive = x.IsActive == true
            })
            .ToListAsync(ct);

        return ServiceResult<List<ApprovalPolicyPositionDto>>.Ok(rows);
    }

    public async Task<ServiceResult<List<DepartmentDto>>> GetDepartmentsAsync(CancellationToken ct = default)
    {
        var rows = await _uow.Repository<F03Department>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true)
            .OrderBy(x => x.DeptCode)
            .Select(x => new DepartmentDto
            {
                Id = x.Id,
                DeptCode = x.DeptCode,
                DeptName = x.DeptName,
                IsActive = x.IsActive == true,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return ServiceResult<List<DepartmentDto>>.Ok(rows);
    }

    public async Task<ServiceResult<ApprovalPolicyDto>> CreateAsync(
        ApprovalPolicyRequest request, int actorUserId, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request, null, ct);
        if (validation != null) return ServiceResult<ApprovalPolicyDto>.Fail(validation);

        var approvalPosition = await GetApprovalPositionAsync(request.ApprovalPositionCode, ct);
        if (approvalPosition == null)
            return ServiceResult<ApprovalPolicyDto>.Fail("Chức vụ phê duyệt không còn hoạt động.");

        request.Level = approvalPosition.DefaultApproveLevel!.Value;
        request.LevelName = approvalPosition.PositionName;
        request.RoleName = RoleNameFromPosition(request.Level);
