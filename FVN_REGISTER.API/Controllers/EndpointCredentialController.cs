using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Infrastructure.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FVN_REGISTER.API.Controllers;

[ApiController]
[Route("api/security/endpoints/credentials")]
[Authorize]
public sealed class EndpointCredentialController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService _authorization;
    private readonly EndpointCredentialService _service;

    public EndpointCredentialController(FVN_REGISTER.Infrastructure.FVNWEBAPPContext db, ICurrentUserService currentUser, FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService authorization)
    {
        _currentUser = currentUser;
        _authorization = authorization;
        _service = new EndpointCredentialService(db);
    }

    [HttpGet("{deviceKey}/status")]
    public async Task<IActionResult> Status(string deviceKey, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Forbid();

        var canProvision = await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointCredentialProvision, ct);
        var canRotate = await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointCredentialRotate, ct);
        if (!canProvision && !canRotate) return Forbid();

        try
        {
            var result = await _service.GetStatusAsync(deviceKey, ct);
            return Ok(ApiResponse<EndpointCredentialStatusDto>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<EndpointCredentialStatusDto>.Fail(ex.Message));
        }
    }

    [HttpPost("provision")]
    public async Task<IActionResult> Provision([FromBody] EndpointCredentialProvisionDto request, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Forbid();

        try
        {
            var status = await _service.GetStatusAsync(request.DeviceKey, ct);
            var requiredPermission = status.HasActiveCredential
                ? SecurityFunctionCodes.EndpointCredentialRotate
                : SecurityFunctionCodes.EndpointCredentialProvision;

            if (!await _authorization.HasAsync(user, requiredPermission, ct)) return Forbid();

            var result = await _service.ProvisionAsync(request, user.UserId, ct);
            return Ok(ApiResponse<EndpointCredentialProvisionResult>.Ok(result));
        }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<EndpointCredentialProvisionResult>.Fail(ex.Message)); }
    }

    [HttpPost("{deviceKey}/revoke")]
    public async Task<IActionResult> Revoke(string deviceKey, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null || !await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointCredentialRevoke, ct)) return Forbid();
        var changed = await _service.RevokeAsync(deviceKey, user.UserId, ct);
        return changed ? Ok(ApiResponse<object>.Ok(new { DeviceKey = deviceKey, Revoked = true })) : NotFound(ApiResponse<object>.Fail("Không tìm thấy credential đang hoạt động của thiết bị.", 404));
    }
}
