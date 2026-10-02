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

        F03ExecutionPolicy entity;
        if (id.HasValue)
        {
            entity = await _db.ExecutionPolicies
                .FirstOrDefaultAsync(x => x.Id == id.Value, ct)
                ?? throw new KeyNotFoundException("Không tìm thấy Execution Resolution Policy.");

            // Existing cases must keep their historical policy snapshot. Incrementing
            // the version makes every later case/round distinguishable in audit.
            entity.PolicyVersion = Math.Max(1, entity.PolicyVersion + 1);
        }
        else
        {
            entity = new F03ExecutionPolicy { PolicyVersion = 1 };
            _db.ExecutionPolicies.Add(entity);
        }

        entity.ModuleCode = request.ModuleCode.Trim().ToUpperInvariant();
        entity.PolicyName = request.PolicyName.Trim();
        entity.ReconciliationMode = request.ReconciliationMode;
        entity.ConfirmationMode = request.ConfirmationMode;
        entity.EvidenceMode = request.EvidenceMode;
        entity.ReviewMode = request.ReviewMode;
        entity.DueHours = request.DueHours;
        entity.AutoResolveMode = request.AutoResolveMode;
        entity.CorrectionMode = request.CorrectionMode;
        entity.EmployeeResponseHours = request.EmployeeResponseHours;
        entity.HrReviewHours = request.HrReviewHours;
        entity.AllowEmployeeAppeal = request.AllowEmployeeAppeal;
        entity.MaxAppealRounds = request.AllowEmployeeAppeal ? request.MaxAppealRounds : (byte)0;
        entity.AppealReviewHours = request.AppealReviewHours;
        entity.RequireEvidenceOnAppeal = request.RequireEvidenceOnAppeal;
        entity.RequireFinalDecision = request.RequireFinalDecision;
        entity.FinalDecisionPositionCode = string.IsNullOrWhiteSpace(request.FinalDecisionPositionCode)
            ? null : request.FinalDecisionPositionCode.Trim();
        entity.PayrollCutoffMode = request.PayrollCutoffMode;
        entity.AllowReopenAfterPayroll = request.AllowReopenAfterPayroll;
        entity.AdjustmentPeriodMode = request.AdjustmentPeriodMode;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.IsActive = request.IsActive;
        entity.ModifiedBy = actorUserId;
        entity.ModifiedAt = DateTime.Now;
        entity.LastModifiedSource = "EXECUTION_RESOLUTION_POLICY";

        if (entity.CreatedBy is null || entity.CreatedBy == 0)
        {
            entity.CreatedBy = actorUserId;
            entity.CreatedAt = DateTime.Now;
        }

        // Only one active policy may govern a module at a time.
        if (entity.IsActive)
        {
            await _db.ExecutionPolicies
                .Where(x => x.Id != entity.Id && x.IsActive != false && x.ModuleCode == entity.ModuleCode)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsActive, false)
                    .SetProperty(x => x.ModifiedBy, actorUserId)
                    .SetProperty(x => x.ModifiedAt, DateTime.Now)
                    .SetProperty(x => x.LastModifiedSource, "EXECUTION_RESOLUTION_POLICY_SUPERSEDED"), ct);
        }

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

        if (await _db.ExecutionPolicies.AnyAsync(x =>
                x.Id != id &&
                x.IsActive != false &&
                x.ModuleCode == module, ct))
            return "Module này đang có policy active. Hãy sửa policy hiện tại; hệ thống sẽ tự version và vô hiệu policy cũ.";

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