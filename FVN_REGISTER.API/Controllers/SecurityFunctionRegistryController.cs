using FVN_REGISTER.Application.Interfaces.Auths;
using FVN_REGISTER.Application.Interfaces.Security;
using AppAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Infrastructure.Services.Security;
using FVN_REGISTER.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/security/registry")]
public sealed class SecurityFunctionRegistryController : BaseApiController
{
    private readonly SecurityFunctionRegistryService _registry;
    private readonly SecurityWebManifestService _webManifest;
    private readonly AppAuthorizationService _authorization;
    private readonly FVNWEBAPPContext _db;
    private readonly IEnumerable<EndpointDataSource> _endpointSources;

    public SecurityFunctionRegistryController(
        SecurityFunctionRegistryService registry,
        FVNWEBAPPContext db,
        AppAuthorizationService authorization,
        IEnumerable<EndpointDataSource> endpointSources,
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<SecurityFunctionRegistryController> logger,
        IOptionsMonitor<FVN_REGISTER.Application.Configuration.AuthDebugOptions> options)
        : base(currentUser, userLog, logger, options)
    {
        _registry = registry;
        _webManifest = new SecurityWebManifestService(db);
        _authorization = authorization;
        _db = db;
        _endpointSources = endpointSources;
    }

    [HttpPost("scan")]
    public async Task<IActionResult> Scan(CancellationToken ct)
    {
        if (!await CanManageAsync(SecurityFunctionCodes.SecurityManageFunctions, ct)) return Forbid();

        var definitionResult = await _registry.ReconcileAsync(ct);
        var candidateDiscovery = new SecurityCandidateDiscovery(_db, _endpointSources);
        var candidateCount = await candidateDiscovery.ScanAsync(ct);
        var now = DateTime.Now;

        var pendingRegistration = await _db.SecurityFunctionRegistry.CountAsync(x => x.LifecycleStatus == "PendingRegistration" && !x.IsIgnored, ct);
        var pendingRetirement = await _db.SecurityFunctionRegistry.CountAsync(x => x.LifecycleStatus == "PendingRetirement", ct);
        var conflicts = await _db.SecurityFunctionRegistry.CountAsync(x => x.LifecycleStatus == "Conflict", ct);
        var matched = await _db.SecurityFunctionRegistry.CountAsync(x => x.LifecycleStatus == "Active", ct);

        var summary = new SecurityFunctionDiscoverySummaryDto(
            now,
            definitionResult.Discovered + candidateCount,
            matched,
            pendingRegistration,
            pendingRetirement,
            conflicts);

        return Ok(ApiResponse<SecurityFunctionDiscoverySummaryDto>.Ok(summary));
    }

    [HttpPost("web-manifest")]
    public async Task<IActionResult> PublishWebManifest([FromBody] WebSecurityManifestRequest request, CancellationToken ct)
    {
        // Discovery is telemetry only. It must work for an authenticated user even when
        // the function catalog has been emptied. It never grants or changes permissions.
        if (UserInfo == null) return Unauthorized();
        if (request.Entries is null) return BadRequest(ApiResponse<object>.Fail("Danh sách chức năng giao diện không được để trống."));
        try
        {
            var result = await _webManifest.PublishAsync(request.Entries, ct);
            await LogActionAsync($"Cập nhật danh mục phát hiện chức năng giao diện: {result.Received} mục");
            return Ok(ApiResponse<WebSecurityManifestSummaryDto>.Ok(result));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }

    [HttpGet("functions")]
    public async Task<IActionResult> GetFunctions([FromQuery] string? status, CancellationToken ct)
    {
        if (!await CanManageAsync(SecurityFunctionCodes.SecurityManageFunctions, ct)) return Forbid();
        return Ok(ApiResponse<IReadOnlyList<SecurityFunctionRegistryItemDto>>.Ok(await _registry.GetRegistryAsync(status, ct)));
    }

    [HttpGet("functions/{functionKey}")]
    public async Task<IActionResult> GetFunction(string functionKey, CancellationToken ct)
    {
        if (!await CanManageAsync(SecurityFunctionCodes.SecurityManageFunctions, ct)) return Forbid();
        var item = await _registry.GetAsync(functionKey, ct);
        return item == null
            ? NotFound(ApiResponse<object>.Fail("Không tìm thấy chức năng bảo mật."))
            : Ok(ApiResponse<SecurityFunctionRegistryItemDto>.Ok(item));
    }

    [HttpPost("functions/{functionKey}/register")]
    public async Task<IActionResult> Register(string functionKey, [FromBody] RegisterDiscoveredFunctionRequest request, CancellationToken ct)
    {
        if (!await CanManageAsync(SecurityFunctionCodes.SecurityManageFunctions, ct)) return Forbid();
        if (UserInfo == null) return Unauthorized();
        try
        {
            await _registry.RegisterAsync(functionKey, request, UserInfo.UserId, ct);
            await LogActionAsync($"Đăng ký chức năng bảo mật {functionKey}");
            return Ok(ApiResponse<object>.Ok(new { functionKey }));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }

    [HttpPost("functions/{functionKey}/retire")]
    public async Task<IActionResult> Retire(string functionKey, CancellationToken ct)
    {
        if (!await CanManageAsync(SecurityFunctionCodes.SecurityManageFunctions, ct)) return Forbid();
        if (UserInfo == null) return Unauthorized();
        try
        {
            await _registry.RetireAsync(functionKey, UserInfo.UserId, ct);
            await LogActionAsync($"Ngừng chức năng bảo mật {functionKey}");
            return Ok(ApiResponse<object>.Ok(new { functionKey }));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }

    [HttpPost("functions/{functionKey}/replace")]
    public async Task<IActionResult> Replace(string functionKey, [FromBody] ReplaceFunctionRequest request, CancellationToken ct)
    {
        if (!await CanManageAsync(SecurityFunctionCodes.SecurityManageFunctions, ct)) return Forbid();
        if (UserInfo == null) return Unauthorized();
        try
        {
            await _registry.ReplaceAsync(functionKey, request, UserInfo.UserId, ct);
            await LogActionAsync($"Thay thế chức năng bảo mật {functionKey} -> {request.ReplacementFunctionKey}");
            return Ok(ApiResponse<object>.Ok(new { functionKey, request.ReplacementFunctionKey }));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }

    [HttpPost("functions/{functionKey}/ignore")]
    public async Task<IActionResult> Ignore(string functionKey, CancellationToken ct)
    {
        if (!await CanManageAsync(SecurityFunctionCodes.SecurityManageFunctions, ct)) return Forbid();
        if (UserInfo == null) return Unauthorized();
        try
        {
            await _registry.IgnoreAsync(functionKey, UserInfo.UserId, ct);
            await LogActionAsync($"Bỏ qua chức năng bảo mật {functionKey}");
            return Ok(ApiResponse<object>.Ok(new { functionKey }));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }

    [HttpPost("functions")]
    public async Task<IActionResult> UpsertFunction([FromBody] SecurityFunctionUpsertRequest request, CancellationToken ct)
    {
        if (!await CanManageAsync(SecurityFunctionCodes.SecurityManageFunctions, ct)) return Forbid();
        if (UserInfo == null) return Unauthorized();
        try
        {
            await _registry.UpsertFunctionAsync(request, UserInfo.UserId, ct);
            await LogActionAsync($"Thêm/Sửa chức năng bảo mật {request.FunctionKey}");
            return Ok(ApiResponse<object>.Ok(new { request.FunctionKey }));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }

    [HttpDelete("functions/{id:int}")]
    public async Task<IActionResult> DeleteFunction(int id, CancellationToken ct)
    {
        if (!await CanManageAsync(SecurityFunctionCodes.SecurityManageFunctions, ct)) return Forbid();
        if (UserInfo == null) return Unauthorized();
        try
        {
            await _registry.DeleteFunctionAsync(id, UserInfo.UserId, ct);
            await LogActionAsync($"Xóa/Ngừng chức năng bảo mật Id={id}");
            return Ok(ApiResponse<object>.Ok(new { id }));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }

    [HttpPost("roles")]
    public async Task<IActionResult> UpsertRole([FromBody] SecurityRoleUpsertRequest request, CancellationToken ct)
    {
        if (!await CanManageAsync(SecurityFunctionCodes.SecurityManageRoles, ct)) return Forbid();
        if (UserInfo == null) return Unauthorized();
        try
        {
            await _registry.UpsertRoleAsync(request, UserInfo.UserId, ct);
            await LogActionAsync($"Thêm/Sửa vai trò bảo mật {request.RoleCode}");
            return Ok(ApiResponse<object>.Ok(new { request.RoleCode }));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }

    [HttpDelete("roles/{id:int}")]
    public async Task<IActionResult> DeleteRole(int id, CancellationToken ct)
    {
        if (!await CanManageAsync(SecurityFunctionCodes.SecurityManageRoles, ct)) return Forbid();
        if (UserInfo == null) return Unauthorized();
        try
        {
            await _registry.DeleteRoleAsync(id, UserInfo.UserId, ct);
            await LogActionAsync($"Xóa/Ngừng vai trò bảo mật Id={id}");
            return Ok(ApiResponse<object>.Ok(new { id }));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }

    private async Task<bool> CanManageAsync(int functionCode, CancellationToken ct)
    {
        if (UserInfo == null) return false;
        if (await _authorization.HasAsync(UserInfo, functionCode, ct)) return true;

        // Bootstrap path only protects registry management surface.
        // It does not grant any business capability and is available only to SuperAdmin RoleCode/PermissionCode = 1.
        return functionCode is SecurityFunctionCodes.SecurityManageFunctions or SecurityFunctionCodes.SecurityManageRoles
            && await _registry.CanBootstrapRegistryAsync(UserInfo.UserId, ct);
    }
}
