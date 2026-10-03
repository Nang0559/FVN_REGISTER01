using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Execution;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Interfaces.Security;
using FvnAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/execution")]
public sealed class ExecutionEmployeeResolutionController : BaseApiController
{
    private readonly IExecutionEmployeeResolutionService _service;
    private readonly FvnAuthorizationService _authorization;

    public ExecutionEmployeeResolutionController(
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<ExecutionEmployeeResolutionController> logger,
        IOptionsMonitor<AuthDebugOptions> options,
        IExecutionEmployeeResolutionService service,
        FvnAuthorizationService authorization)
        : base(currentUser, userLog, logger, options)
    {
        _service = service;
        _authorization = authorization;
    }

    [HttpPost("me/{reconciliationId:long}/resolution")]
    public async Task<IActionResult> Decide(
        long reconciliationId,
        [FromBody] ExecutionEmployeeDecisionRequest request,
        CancellationToken ct)
    {
        if (UserInfo == null
            || !await _authorization.HasPersonalAsync(UserInfo, SecurityFunctionCodes.AttendanceFeedback, ct))
            return Forbid();

        if (UserInfo.UserId is not int userId || string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không hợp lệ."));

        return HandleResult(await _service.DecideAsync(
            userId, UserInfo.EmployeeCode, reconciliationId, request, ct));
    }
}