using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Execution;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.FeatureOperators;
using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/execution/policies")]
public sealed class ExecutionResolutionPolicyController : BaseApiController
{
    private readonly IExecutionResolutionPolicyService _service;
    private readonly IAuthorizationService _authorization;
    private readonly IFeatureOperatorAssignmentService _operators;

    public ExecutionResolutionPolicyController(
        IExecutionResolutionPolicyService service,
        IAuthorizationService authorization,
        IFeatureOperatorAssignmentService operators,
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<ExecutionResolutionPolicyController> logger,
        IOptionsMonitor<AuthDebugOptions> options)
        : base(currentUser, userLog, logger, options)
    {
        _service = service;
        _authorization = authorization;
        _operators = operators;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        return HandleResult(await _service.GetAllAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] ExecutionResolutionPolicyRequest request,
        CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (UserInfo is null) return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập hết hạn."));
        return HandleResult(await _service.SaveAsync(null, request, UserInfo.UserId, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] ExecutionResolutionPolicyRequest request,
        CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (UserInfo is null) return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập hết hạn."));
        return HandleResult(await _service.SaveAsync(id, request, UserInfo.UserId, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (UserInfo is null) return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập hết hạn."));
        return HandleResult(await _service.DeactivateAsync(id, UserInfo.UserId, ct));
    }

    private async Task<bool> CanAsync(CancellationToken ct)
    {
        if (UserInfo is null || !await _authorization.HasAsync(UserInfo, SecurityFunctionCodes.ExecutionPolicyManage, ct))
            return false;
        return await _operators.CanOperateAsync(UserInfo.UserId, UserInfo.EmployeeCode,
            SecurityFunctionCodes.ExecutionPolicyManage, FeatureOperatorCatalog.ExecutionPolicy, null, ct);
    }
}