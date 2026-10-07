using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Contract.Dtos.Depts;
using FVN_REGISTER.Contract.Requests.Approvals;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Repositories;
using FVN_REGISTER.Core.Utils;
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
                DeptCode = x.DeptCode,
                // Explicit SQL conversion is required because legacy databases may store these
                // codes as numeric columns while the application contract treats them as text.
                PositionCode = x.PositionCode == null ? null : x.PositionCode.ToString(),
                ApprovalPositionCode = x.ApprovalPositionCode == null ? string.Empty : x.ApprovalPositionCode.ToString(),
                Level = x.Level,
                Sequence = x.Sequence,
                LevelName = x.LevelName ?? string.Empty,
                RoleName = x.RoleName ?? string.Empty,
                Required = x.Required
            })
            .ToListAsync(ct);

        // Policy data is the source of truth for this screen. Master-data enrichment
        // must not make the whole policy list fail (e.g. an old/mismatched F03Positions row).
        // Fall back to codes when department/position names cannot be resolved.
        try
        {
            var departments = await _uow.Repository<F03Department>().Query()
                .AsNoTracking()
                .ToDictionaryAsync(x => x.DeptCode, x => x.DeptName, ct);

            foreach (var policy in policies)
                policy.DeptName = departments.GetValueOrDefault(policy.DeptCode, policy.DeptCode.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[APPROVAL-POLICY] Department-name enrichment failed; keeping DeptCode.");
            foreach (var policy in policies)
                policy.DeptName = policy.DeptCode.ToString();
        }

        try
        {
            // Convert PositionCode explicitly to text in SQL so legacy numeric PositionCode
            // storage cannot break the policy-list endpoint with Int32 -> String materialization.
            var positions = await _uow.Repository<F03Position>().Query()
                .AsNoTracking()
                .Select(x => new
                {
                    PositionCode = x.PositionCode.ToString(),
                    x.PositionName
                })
                .ToDictionaryAsync(x => x.PositionCode, x => x, ct);

            foreach (var policy in policies)
            {
                positions.TryGetValue(policy.PositionCode ?? string.Empty, out var requesterPosition);
                positions.TryGetValue(policy.ApprovalPositionCode, out var approvalPosition);

                policy.PositionName = requesterPosition?.PositionName ?? "(Tất cả vị trí)";
                policy.ApprovalPositionName = approvalPosition?.PositionName ?? policy.ApprovalPositionCode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[APPROVAL-POLICY] Position-name enrichment failed; keeping PositionCode.");
            foreach (var policy in policies)
            {
                policy.PositionName = string.IsNullOrWhiteSpace(policy.PositionCode)
                    ? "(Tất cả vị trí)"
                    : policy.PositionCode;
                policy.ApprovalPositionName = policy.ApprovalPositionCode;
            }
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
                // Explicit text conversion keeps legacy numeric PositionCode rows readable.
                PositionCode = x.PositionCode.ToString(),
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

        ApplyDerivedApprovalValues(request, approvalPosition);

        var entity = new F03ApprovalPolicy
        {
            RequestType = (RequestModule)request.RequestType,
            DeptCode = request.DeptCode,
            PositionCode = Normalize(request.PositionCode),
            ApprovalPositionCode = request.ApprovalPositionCode.Trim(),
            Level = request.Level,
            Sequence = request.Sequence,
            LevelName = request.LevelName.Trim(),
            RoleName = request.RoleName.Trim(),
            Required = request.Required,
            IsActive = request.IsActive,
            CreatedBy = actorUserId,
            LastModifiedSource = "Manual"
        };

        await _uow.Repository<F03ApprovalPolicy>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await ReconcileApproversAsync(actorUserId, ct);
        return ServiceResult<ApprovalPolicyDto>.Ok(await MapAsync(entity, ct));
    }

    public async Task<ServiceResult<ApprovalPolicyDto>> UpdateAsync(
        int id, ApprovalPolicyRequest request, int actorUserId, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<F03ApprovalPolicy>().Query()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (entity == null)
            return ServiceResult<ApprovalPolicyDto>.Fail("Không tìm thấy Approval Policy.");

        var validation = await ValidateAsync(request, id, ct);
        if (validation != null)
            return ServiceResult<ApprovalPolicyDto>.Fail(validation);

        var approvalPosition = await GetApprovalPositionAsync(request.ApprovalPositionCode, ct);
        if (approvalPosition == null)
            return ServiceResult<ApprovalPolicyDto>.Fail("Chức vụ phê duyệt không còn hoạt động.");

        ApplyDerivedApprovalValues(request, approvalPosition);

        entity.RequestType = (RequestModule)request.RequestType;
        entity.DeptCode = request.DeptCode;
        entity.PositionCode = Normalize(request.PositionCode);
        entity.ApprovalPositionCode = request.ApprovalPositionCode.Trim();
        entity.Level = request.Level;
        entity.Sequence = request.Sequence;
        entity.LevelName = request.LevelName.Trim();
        entity.RoleName = request.RoleName.Trim();
        entity.Required = request.Required;
        entity.IsActive = request.IsActive;
        entity.ModifiedBy = actorUserId;
        entity.ModifiedAt = DateTime.Now;
        entity.LastModifiedSource = "Manual";

        await _uow.SaveChangesAsync(ct);
        await ReconcileApproversAsync(actorUserId, ct);
        return ServiceResult<ApprovalPolicyDto>.Ok(await MapAsync(entity, ct));
    }

    public async Task<ServiceResult<object>> DeleteAsync(
        int id, int actorUserId, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<F03ApprovalPolicy>().Query()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (entity == null)
            return ServiceResult<object>.Fail("Không tìm thấy Approval Policy.");

        entity.IsActive = false;
        entity.ModifiedBy = actorUserId;
        entity.ModifiedAt = DateTime.Now;
        entity.LastModifiedSource = "Manual";
        await _uow.SaveChangesAsync(ct);
        await ReconcileApproversAsync(actorUserId, ct);

        return ServiceResult<object>.Ok(new { id, deactivated = true });
    }

    private async Task<string?> ValidateAsync(
        ApprovalPolicyRequest request, int? excludeId, CancellationToken ct)
    {
        if (!Enum.IsDefined(typeof(RequestModule), request.RequestType))
            return "RequestType không hợp lệ.";

        var deptCode = request.DeptCode;
        if (deptCode <= 0)
            return "Phòng ban là bắt buộc.";

        if (!await _uow.Repository<F03Department>().Query()
            .AnyAsync(x => x.IsActive == true && x.DeptCode == deptCode, ct))
            return $"Phòng ban '{deptCode}' không tồn tại hoặc đã inactive.";

        var positionCode = Normalize(request.PositionCode);
        if (positionCode != null &&
            !await _uow.Repository<F03Position>().Query()
                .AnyAsync(x => x.IsActive == true && x.PositionCode == positionCode, ct))
            return $"Position '{positionCode}' không tồn tại hoặc đã inactive.";

        var approvalPosition = await _uow.Repository<F03Position>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true &&
                        x.PositionCode == request.ApprovalPositionCode.Trim())
            .Select(x => new { x.PositionCode, x.PositionName, x.DefaultApproveLevel })
            .FirstOrDefaultAsync(ct);

        if (approvalPosition == null)
            return $"Chức vụ phê duyệt '{request.ApprovalPositionCode}' không tồn tại hoặc đã inactive.";

        if (!approvalPosition.DefaultApproveLevel.HasValue ||
            approvalPosition.DefaultApproveLevel.Value is < 1 or > 7)
            return $"Chức vụ '{approvalPosition.PositionName}' chưa có DefaultApproveLevel hợp lệ.";

        if (request.Sequence < 1)
            return "Thứ tự cấp duyệt phải >= 1.";

        var level = approvalPosition.DefaultApproveLevel.Value;
        var duplicate = await _uow.Repository<F03ApprovalPolicy>().Query()
            .AnyAsync(x =>
                x.IsActive == true &&
                x.Id != excludeId &&
                x.RequestType == (RequestModule)request.RequestType &&
                x.DeptCode == deptCode &&
                x.PositionCode == positionCode &&
                x.Level == level,
                ct);

        return duplicate
            ? "Đã có cấu hình duyệt cho cùng Loại yêu cầu + Phòng ban + Chức vụ người yêu cầu + Cấp duyệt."
            : null;
    }

    private async Task<ApprovalPolicyPositionDto?> GetApprovalPositionAsync(
        string code, CancellationToken ct)
    {
        var normalized = code?.Trim() ?? string.Empty;
        if (normalized.Length == 0) return null;

        return await _uow.Repository<F03Position>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true && x.PositionCode == normalized)
            .Select(x => new ApprovalPolicyPositionDto
            {
                PositionCode = x.PositionCode,
                PositionName = x.PositionName,
                DefaultApproveLevel = x.DefaultApproveLevel,
                RoleName = RoleNameFromPosition(x.DefaultApproveLevel),
                IsActive = x.IsActive == true
            })
            .FirstOrDefaultAsync(ct);
    }

    private static void ApplyDerivedApprovalValues(
        ApprovalPolicyRequest request, ApprovalPolicyPositionDto approvalPosition)
    {
        request.Level = approvalPosition.DefaultApproveLevel!.Value;
        request.LevelName = approvalPosition.PositionName?.Trim() ?? string.Empty;
        request.RoleName = RoleNameFromPosition(request.Level);
    }

    private async Task<ApprovalPolicyDto> MapAsync(
        F03ApprovalPolicy x, CancellationToken ct)
    {
        var deptName = await _uow.Repository<F03Department>().Query()
            .AsNoTracking()
            .Where(d => d.DeptCode == x.DeptCode)
            .Select(d => d.DeptName)
            .FirstOrDefaultAsync(ct);

        var positions = await _uow.Repository<F03Position>().Query()
            .AsNoTracking()
            .Where(p => p.PositionCode == x.PositionCode ||
                        p.PositionCode == x.ApprovalPositionCode)
            .ToDictionaryAsync(p => p.PositionCode, ct);

        positions.TryGetValue(x.PositionCode ?? string.Empty, out var requester);
        positions.TryGetValue(x.ApprovalPositionCode, out var approver);

        return new ApprovalPolicyDto
        {
            Id = x.Id,
            IsActive = x.IsActive == true,
            RequestType = (int)x.RequestType,
            RequestTypeName = RequestTypeName(x.RequestType),
            DeptCode = x.DeptCode,
            DeptName = deptName ?? x.DeptCode.ToString(),
            PositionCode = x.PositionCode,
            PositionName = requester?.PositionName ?? "(Tất cả vị trí)",
            ApprovalPositionCode = x.ApprovalPositionCode,
            ApprovalPositionName = approver?.PositionName ?? x.ApprovalPositionCode,
            Level = x.Level,
            Sequence = x.Sequence,
            LevelName = x.LevelName ?? string.Empty,
            RoleName = x.RoleName ?? string.Empty,
            Required = x.Required
        };
    }

    private async Task ReconcileApproversAsync(int actorUserId, CancellationToken ct)
    {
        try
        {
            await _uow.ExecuteSqlRawAsync(
                """
                EXEC dbo.usp_ReconcileEmployeeApprovers
                    @EmployeeCode=NULL,
                    @CreatedBy={0};
                """,
                ct,
                actorUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[APPROVAL-POLICY] Approver reconcile failed after policy change.");
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<bool> CanApproveAsync(
        RequestModule requestType,
        string requesterEmployeeCode,
        string approverEmployeeCode,
        int level,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requesterEmployeeCode) ||
            string.IsNullOrWhiteSpace(approverEmployeeCode) ||
            level <= 0)
            return false;

        var requester = await _uow.Repository<F03Employee>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true && x.EmployeeCode == requesterEmployeeCode)
            .Select(x => new { x.DeptCode, x.PositionCode })
            .FirstOrDefaultAsync(ct);

        var approver = await _uow.Repository<F03Employee>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true && x.EmployeeCode == approverEmployeeCode)
            .Select(x => new { x.DeptCode, x.PositionCode })
            .FirstOrDefaultAsync(ct);

        if (requester == null || approver == null)
            return false;

        return await _uow.Repository<F03ApprovalPolicy>().Query()
            .AsNoTracking()
            .AnyAsync(x =>
                x.IsActive == true
                && x.RequestType == requestType
                && x.DeptCode == requester.DeptCode
                && (x.PositionCode == null || x.PositionCode == requester.PositionCode)
                && x.ApprovalPositionCode == approver.PositionCode
                && x.Level == level
                && x.Required,
                ct);
    }

    private static string RoleNameFromPosition(int? level)
        => level switch
        {
            1 => ApproverRole.SubLeader,
            2 => ApproverRole.Chief,
            3 => ApproverRole.Manager,
            4 => ApproverRole.GM,
            5 => ApproverRole.Union,
            6 => ApproverRole.Manager,
            7 => ApproverRole.GM,
            _ => string.Empty
        };

    private static string RequestTypeName(RequestModule type) => type switch
    {
        RequestModule.Leave => "Nghỉ phép",
        RequestModule.Overtime => "Tăng ca",
        RequestModule.Trip => "Công tác",
        RequestModule.Equipment => "Thiết bị",
        _ => type.ToString()
    };
}
