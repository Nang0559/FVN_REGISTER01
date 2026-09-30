using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/security")]
public sealed class SecurityController : BaseApiController
{
    private readonly IAuthorizationService _authorization;

    public SecurityController(
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<SecurityController> logger,
        IOptionsMonitor<AuthDebugOptions> options,
        IAuthorizationService authorization)
        : base(currentUser, userLog, logger, options)
    {
        _authorization = authorization;
    }

    // ... existing controller actions remain unchanged ...

    [HttpPut("roles/{roleCode:int}/functions")]
    public async Task<IActionResult> SetRoleFunctions(
        int roleCode,
        [FromBody] UpdateRoleFunctionsRequest request,
        CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        if (roleCode != request.RoleCode)
            return BadRequest(ApiResponse<object>.Fail("RoleCode không khớp."));
        if (!await CanManageAsync(SecurityFunctionCodes.SecurityManageFunctions, ct))
            return Forbid();

        try
        {
            var role = await _authorization.SetRoleFunctionsAsync(
                roleCode, request.FunctionCodes, UserInfo.UserId, ct);

            await LogActionAsync($"Cập nhật function cho RoleCode={roleCode}");
            return Ok(ApiResponse<SecurityRoleDto>.Ok(role));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private Task<bool> CanManageTwoFactorAsync(CancellationToken ct)
    {
        // 2FA administration is a SuperAdmin-only security operation.
        // The 2407 function remains part of the RBAC catalog for visibility/audit,
        // but a stale/missing RoleFunction row must not lock the SuperAdmin out.
        return Task.FromResult(
            UserInfo != null &&
            UserInfo.Permission == UserPermissionCodes.SuperAdmin);
    }

    private async Task<bool> CanManageAsync(int functionCode, CancellationToken ct)
    {
        return UserInfo != null &&
               await _authorization.HasAsync(UserInfo, functionCode, ct);
    }
}
