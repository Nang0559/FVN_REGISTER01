using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Attributes;
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
    private readonly IEndpointCredentialService _service;

    public EndpointCredentialController(
        ICurrentUserService currentUser,
        FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService authorization,
        IEndpointCredentialService service)
    {
        _currentUser = currentUser;
        _authorization = authorization;
        _service = service;
    }

    [HttpGet("equipment/{equipmentAssetId:int}/status")]
    [SecurityFunctionDefinition("Endpoint.CredentialProvision", "Endpoint Credential - xem theo Equipment")]
    public async Task<IActionResult> EquipmentStatus(int equipmentAssetId, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Forbid();
        var canView = await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointCredentialProvision, ct)
                   || await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointCredentialRotate, ct)
                   || await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointCredentialRevoke, ct);
        if (!canView) return Forbid();
        try { return Ok(ApiResponse<EndpointCredentialStatusDto>.Ok(await _service.GetStatusByEquipmentAsync(equipmentAssetId, ct))); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<EndpointCredentialStatusDto>.Fail(ex.Message)); }
    }

    [HttpGet("equipment/{equipmentAssetId:int}/history")]
    [SecurityFunctionDefinition("Endpoint.CredentialProvision", "Endpoint Credential - lịch sử theo Equipment")]
    public async Task<IActionResult> EquipmentHistory(int equipmentAssetId, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Forbid();
        var canView = await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointCredentialProvision, ct)
                   || await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointCredentialRotate, ct);
        if (!canView) return Forbid();
        try { return Ok(ApiResponse<IReadOnlyList<EndpointCredentialHistoryDto>>.Ok(await _service.GetHistoryByEquipmentAsync(equipmentAssetId, ct))); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<IReadOnlyList<EndpointCredentialHistoryDto>>.Fail(ex.Message)); }
    }

    [HttpPost("equipment/{equipmentAssetId:int}/provision")]
    [SecurityFunctionDefinition("Endpoint.CredentialProvision", "Endpoint Credential - cấp theo Equipment")]
    public async Task<IActionResult> EquipmentProvision(int equipmentAssetId, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Forbid();
        var status = await _service.GetStatusByEquipmentAsync(equipmentAssetId, ct);
        var requiredPermission = status.HasActiveCredential
            ? SecurityFunctionCodes.EndpointCredentialRotate
            : SecurityFunctionCodes.EndpointCredentialProvision;
        if (!await _authorization.HasAsync(user, requiredPermission, ct)) return Forbid();
        try { return Ok(ApiResponse<EndpointCredentialProvisionResult>.Ok(await _service.ProvisionForEquipmentAsync(equipmentAssetId, user.UserId, ct))); }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<EndpointCredentialProvisionResult>.Fail(ex.Message)); }
    }

    [HttpPost("equipment/{equipmentAssetId:int}/revoke")]
    [SecurityFunctionDefinition("Endpoint.CredentialRevoke", "Endpoint Credential - thu hồi theo Equipment")]
    public async Task<IActionResult> EquipmentRevoke(int equipmentAssetId, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null || !await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointCredentialRevoke, ct)) return Forbid();
        var status = await _service.GetStatusByEquipmentAsync(equipmentAssetId, ct);
        if (!status.EndpointExists || string.IsNullOrWhiteSpace(status.DeviceKey))
            return NotFound(ApiResponse<object>.Fail("Equipment chưa có Endpoint Agent.", 404));
        var changed = await _service.RevokeAsync(status.DeviceKey, user.UserId, ct);
        return changed ? Ok(ApiResponse<object>.Ok(new { EquipmentAssetId = equipmentAssetId, Revoked = true }))
                       : NotFound(ApiResponse<object>.Fail("Không tìm thấy credential đang hoạt động.", 404));
    }

    [HttpGet("{deviceKey}/status")]
    [SecurityFunctionDefinition("Endpoint.CredentialProvision", "Endpoint Credential - xem/provision status")]
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
        catch (ArgumentException ex) { return BadRequest(ApiResponse<EndpointCredentialStatusDto>.Fail(ex.Message)); }
    }

    [HttpPost("provision")]
    [SecurityFunctionDefinition("Endpoint.CredentialProvision", "Endpoint Credential - provision/rotate")]
    public async Task<IActionResult> Provision([FromBody] EndpointCredentialProvisionDto request, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Forbid();
        try
        {
            var status = await _service.GetStatusAsync(request.DeviceKey, ct);
            var requiredPermission = status.HasActiveCredential ? SecurityFunctionCodes.EndpointCredentialRotate : SecurityFunctionCodes.EndpointCredentialProvision;
            if (!await _authorization.HasAsync(user, requiredPermission, ct)) return Forbid();
            var result = await _service.ProvisionAsync(request, user.UserId, ct);
            return Ok(ApiResponse<EndpointCredentialProvisionResult>.Ok(result));
        }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<EndpointCredentialProvisionResult>.Fail(ex.Message)); }
    }

    [AllowAnonymous]
    [HttpPost("agent-rotate")]
    public async Task<IActionResult> AgentRotate(CancellationToken ct)
    {
        var apiKey = Request.Headers["X-FVN-Device-Api-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey)) return Unauthorized(ApiResponse<EndpointCredentialProvisionResult>.Fail("Thiếu API key thiết bị.", 401));
        var result = await _service.RotateWithCurrentApiKeyAsync(apiKey, ct);
        return result == null
            ? Unauthorized(ApiResponse<EndpointCredentialProvisionResult>.Fail("API key không hợp lệ hoặc đã hết hạn; cần SuperAdmin provision lại credential.", 401))
            : Ok(ApiResponse<EndpointCredentialProvisionResult>.Ok(result));
    }

    [HttpPost("{deviceKey}/revoke")]
    [SecurityFunctionDefinition("Endpoint.CredentialRevoke", "Endpoint Credential - revoke")]
    public async Task<IActionResult> Revoke(string deviceKey, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null || !await _authorization.HasAsync(user, SecurityFunctionCodes.EndpointCredentialRevoke, ct)) return Forbid();
        var changed = await _service.RevokeAsync(deviceKey, user.UserId, ct);
        return changed ? Ok(ApiResponse<object>.Ok(new { DeviceKey = deviceKey, Revoked = true })) : NotFound(ApiResponse<object>.Fail("Không tìm thấy credential đang hoạt động của thiết bị.", 404));
    }
}
