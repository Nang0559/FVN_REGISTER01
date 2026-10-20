using System.Text.Json;
using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.FeatureOperators;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.Common;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/approval-policies/late-early-rules")]
public sealed class LateEarlyApprovalRulesController : BaseApiController
{
    private const string CodePrefix = "LATE_EARLY_POLICY_";
    private readonly FVNWEBAPPContext _db;
    private readonly IAuthorizationService _authorization;
    private readonly IFeatureOperatorAssignmentService _operators;

    public LateEarlyApprovalRulesController(
        FVNWEBAPPContext db,
        ICurrentUserService currentUser,
        IUserLogService userLog,
        IAuthorizationService authorization,
        IFeatureOperatorAssignmentService operators,
        ILogger<LateEarlyApprovalRulesController> logger,
        IOptionsMonitor<AuthDebugOptions> options)
        : base(currentUser, userLog, logger, options)
    {
        _db = db;
        _authorization = authorization;
        _operators = operators;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int deptCode, CancellationToken ct)
    {
        if (!await CanManageAsync(ct)) return Forbid();
        if (deptCode < 0) return BadRequest(ApiResponse<object>.Fail("DeptCode không hợp lệ."));

        var ownCode = CodePrefix + deptCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var fallbackCode = CodePrefix + "0";
        var rows = await _db.BusinessRules.AsNoTracking()
            .Where(x => x.Module == RequestModule.LateEarly
                && (x.Code == ownCode || x.Code == fallbackCode))
            .ToListAsync(ct);

        var own = rows.FirstOrDefault(x => x.Code == ownCode);
        var fallback = rows.FirstOrDefault(x => x.Code == fallbackCode);
        var selected = (own?.IsActive == true ? own : null)
            ?? (fallback?.IsActive == true ? fallback : null)
            ?? fallback
            ?? own;
        var result = selected == null
            ? new LateEarlyApprovalRuleDto { DeptCode = deptCode, IsFallback = deptCode != 0 }
            : JsonSerializer.Deserialize<LateEarlyApprovalRuleDto>(selected.ConfigJson ?? "{}")
                ?? new LateEarlyApprovalRuleDto();
        result.DeptCode = deptCode;
        result.IsFallback = (own?.IsActive != true) && deptCode != 0 && fallback != null;
        return Ok(ApiResponse<LateEarlyApprovalRuleDto>.Ok(result));
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] LateEarlyApprovalRuleDto request, CancellationToken ct)
    {
        if (!await CanManageAsync(ct)) return Forbid();
        if (request.DeptCode < 0 || request.MonthlyLimit < 0
            || request.EarlyLeaveLevel2BeforeMinutes is < 0 or > 1440)
            return BadRequest(ApiResponse<object>.Fail("Thông số giới hạn không hợp lệ."));

        var code = CodePrefix + request.DeptCode;
        var entity = await _db.BusinessRules.FirstOrDefaultAsync(
            x => x.Module == RequestModule.LateEarly && x.Code == code, ct);
        if (entity == null)
        {
            entity = new F03BusinessRule
            {
                Module = RequestModule.LateEarly,
                Code = code,
                Name = request.DeptCode == 0
                    ? "Chính sách Đi muộn / Về sớm mặc định"
                    : $"Chính sách Đi muộn / Về sớm - Dept {request.DeptCode}",
                CreatedBy = UserInfo!.UserId,
                IsActive = true
            };
            await _db.BusinessRules.AddAsync(entity, ct);
        }

        request.IsFallback = request.DeptCode == 0;
        entity.ConfigJson = JsonSerializer.Serialize(request);
        entity.IsActive = request.IsActive;
        entity.ModifiedBy = UserInfo!.UserId;
        entity.ModifiedAt = DateTime.Now;
        entity.LastModifiedSource = "LateEarlyApprovalPolicy";
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<LateEarlyApprovalRuleDto>.Ok(request));
    }

    private async Task<bool> CanManageAsync(CancellationToken ct)
    {
        if (UserInfo is null
            || !await _authorization.HasAsync(UserInfo, SecurityFunctionCodes.ApprovalPolicyManage, ct))
            return false;
        return await _operators.CanOperateAsync(
            UserInfo.UserId, UserInfo.EmployeeCode,
            SecurityFunctionCodes.ApprovalPolicyManage,
            FeatureOperatorCatalog.ApprovalPolicy, null, ct);
    }
}
