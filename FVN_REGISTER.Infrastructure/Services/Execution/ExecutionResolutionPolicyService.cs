using FVN_REGISTER.Application.Interfaces.Execution;
using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Entities.WorkCalendar;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Execution;

public sealed class ExecutionResolutionPolicyService : IExecutionResolutionPolicyService
{
    private readonly FVNWEBAPPContext _db;

    public ExecutionResolutionPolicyService(FVNWEBAPPContext db) => _db = db;

    public async Task<ServiceResult<IReadOnlyList<ExecutionResolutionPolicyDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _db.ExecutionPolicies.AsNoTracking()
            .OrderBy(x => x.ModuleCode)
            .ThenByDescending(x => x.PolicyVersion)
            .Select(MapExpression())
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<ExecutionResolutionPolicyDto>>.Ok(rows);
    }

    public async Task<ServiceResult<ExecutionResolutionPolicyDto>> SaveAsync(
        int? id,
        ExecutionResolutionPolicyRequest request,
        int actorUserId,
        CancellationToken ct = default)
    {
        var error = await ValidateAsync(request, id, ct);
        if (error is not null)
            return ServiceResult<ExecutionResolutionPolicyDto>.Fail(error);

        F03ExecutionPolicy? previous = null;
        if (id.HasValue)
        {
            previous = await _db.ExecutionPolicies
                .FirstOrDefaultAsync(x => x.Id == id.Value, ct);

            if (previous is null)
                return ServiceResult<ExecutionResolutionPolicyDto>.Fail("Không tìm thấy Execution Resolution Policy.");

            if (!string.Equals(previous.ModuleCode, request.ModuleCode?.Trim(), StringComparison.OrdinalIgnoreCase))
                return ServiceResult<ExecutionResolutionPolicyDto>.Fail(
                    "Không được đổi ModuleCode của policy đã tồn tại. Hãy tạo policy mới cho module mới.");

            previous.IsActive = false;
            previous.EffectiveTo = request.EffectiveFrom ?? DateTime.Now;
            previous.ModifiedBy = actorUserId;
            previous.ModifiedAt = DateTime.Now;
            previous.LastModifiedSource = "EXECUTION_RESOLUTION_POLICY_SUPERSEDED";
        }

        var moduleCode = request.ModuleCode.Trim().ToUpperInvariant();
        var nextVersion = previous is not null
            ? Math.Max(1, previous.PolicyVersion + 1)
            : (await _db.ExecutionPolicies.AsNoTracking()
                .Where(x => x.ModuleCode == moduleCode)
                .Select(x => (int?)x.PolicyVersion)
                .MaxAsync(ct) ?? 0) + 1;

        var entity = new F03ExecutionPolicy
        {
            ModuleCode = moduleCode,
            PolicyName = request.PolicyName.Trim(),
            PolicyVersion = nextVersion,
            ReconciliationMode = request.ReconciliationMode,
            ConfirmationMode = request.ConfirmationMode,
            EvidenceMode = request.EvidenceMode,
            ReviewMode = request.ReviewMode,
            DueHours = request.DueHours,
            AutoResolveMode = request.AutoResolveMode,
            CorrectionMode = request.CorrectionMode,
            EmployeeResponseHours = request.EmployeeResponseHours,
            HrReviewHours = request.HrReviewHours,
            AllowEmployeeAppeal = request.AllowEmployeeAppeal,
            MaxAppealRounds = request.AllowEmployeeAppeal ? request.MaxAppealRounds : (byte)0,
            AppealReviewHours = request.AppealReviewHours,
            RequireEvidenceOnAppeal = request.RequireEvidenceOnAppeal,
            RequireFinalDecision = request.RequireFinalDecision,
            FinalDecisionPositionCode = string.IsNullOrWhiteSpace(request.FinalDecisionPositionCode)
                ? null : request.FinalDecisionPositionCode.Trim(),
            PayrollCutoffMode = request.PayrollCutoffMode,
            AllowReopenAfterPayroll = request.AllowReopenAfterPayroll,
            AdjustmentPeriodMode = request.AdjustmentPeriodMode,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            IsActive = request.IsActive,
            CreatedBy = actorUserId,
            CreatedAt = DateTime.Now,
            ModifiedBy = actorUserId,
            ModifiedAt = DateTime.Now,
            LastModifiedSource = "EXECUTION_RESOLUTION_POLICY"
        };

        _db.ExecutionPolicies.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dto = await _db.ExecutionPolicies.AsNoTracking()
            .Where(x => x.Id == entity.Id)
            .Select(MapExpression())
            .SingleAsync(ct);

        return ServiceResult<ExecutionResolutionPolicyDto>.Ok(dto);
    }

    public async Task<ServiceResult<object>> DeactivateAsync(int id, int actorUserId, CancellationToken ct = default)
    {
        var entity = await _db.ExecutionPolicies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return ServiceResult<object>.Fail("Không tìm thấy Execution Resolution Policy.");

        entity.IsActive = false;
        entity.ModifiedBy = actorUserId;
        entity.ModifiedAt = DateTime.Now;
        entity.LastModifiedSource = "EXECUTION_RESOLUTION_POLICY_DEACTIVATE";
        await _db.SaveChangesAsync(ct);

        return ServiceResult<object>.Ok(new { id, deactivated = true });
    }

    private async Task<string?> ValidateAsync(
        ExecutionResolutionPolicyRequest request,
        int? id,
        CancellationToken ct)
    {
        var module = request.ModuleCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(module))
            return "ModuleCode là bắt buộc.";

        if (module.Length > 50)
            return "ModuleCode tối đa 50 ký tự.";

        if (request.EmployeeResponseHours <= 0)
            return "Thời hạn nhân viên phản hồi phải > 0 giờ.";

        if (request.HrReviewHours <= 0)
            return "Thời hạn HR xử lý phải > 0 giờ.";

        if (request.AllowEmployeeAppeal)
        {
            if (request.MaxAppealRounds is < 1 or > 5)
                return "Số vòng khiếu nại phải từ 1 đến 5.";
            if (request.AppealReviewHours <= 0)
                return "Thời hạn xử lý khiếu nại phải > 0 giờ.";
        }

        if (request.PayrollCutoffMode > 1)
            return "PayrollCutoffMode không hợp lệ.";

        if (request.AdjustmentPeriodMode > 1)
            return "AdjustmentPeriodMode không hợp lệ.";

        if (request.RequireFinalDecision && string.IsNullOrWhiteSpace(request.FinalDecisionPositionCode))
            return "Khi bật quyết định cuối, phải chỉ định PositionCode có thẩm quyền.";

        if (request.EffectiveFrom.HasValue && request.EffectiveTo.HasValue &&
            request.EffectiveTo.Value <= request.EffectiveFrom.Value)
            return "EffectiveTo phải lớn hơn EffectiveFrom.";

        if (request.IsActive)
        {
            var effectiveFrom = request.EffectiveFrom ?? DateTime.MinValue;
            var effectiveTo = request.EffectiveTo ?? DateTime.MaxValue;

            var activePolicies = await _db.ExecutionPolicies.AsNoTracking()
                .Where(x => x.Id != id && x.IsActive != false && x.ModuleCode == module)
                .Select(x => new { x.EffectiveFrom, x.EffectiveTo })
                .ToListAsync(ct);

            var overlaps = activePolicies.Any(x =>
                (x.EffectiveFrom ?? DateTime.MinValue) < effectiveTo
                && effectiveFrom < (x.EffectiveTo ?? DateTime.MaxValue));

            if (overlaps)
                return "Khoảng hiệu lực của policy bị chồng lấn với policy khác cùng module.";
        }

        return null;
    }

    private static System.Linq.Expressions.Expression<Func<F03ExecutionPolicy, ExecutionResolutionPolicyDto>> MapExpression() =>
        x => new ExecutionResolutionPolicyDto(
            x.Id, x.ModuleCode, x.PolicyName, x.PolicyVersion,
            x.ReconciliationMode, x.ConfirmationMode, x.EvidenceMode,
            x.ReviewMode, x.DueHours, x.AutoResolveMode, x.CorrectionMode,
            x.EmployeeResponseHours, x.HrReviewHours, x.AllowEmployeeAppeal,
            x.MaxAppealRounds, x.AppealReviewHours, x.RequireEvidenceOnAppeal,
            x.RequireFinalDecision, x.FinalDecisionPositionCode,
            x.PayrollCutoffMode, x.AllowReopenAfterPayroll,
            x.AdjustmentPeriodMode, x.EffectiveFrom, x.EffectiveTo,
            x.IsActive == true);
}