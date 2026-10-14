using FVN_REGISTER.Application.Interfaces.PublicInformation;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Interfaces.FeatureOperators;
using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Contract.Dtos.PublicForms;
using FVN_REGISTER.Contract.Dtos.PublicInformation;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Contract.Requests.PublicInformation;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

[ApiController]
[Route("api/public-information")]
public sealed class PublicInformationController : ControllerBase
{
    private readonly IPublicInformationService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IFeatureOperatorAssignmentService _operators;

    public PublicInformationController(IPublicInformationService service, ICurrentUserService currentUser, IAuthorizationService authorization,
        IFeatureOperatorAssignmentService operators)
    { _service = service; _currentUser = currentUser; _authorization = authorization; _operators = operators; }

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await _service.GetPublishedAsync(ct));

    /// <summary>UI bootstrap for the Public pages: only the information-management flag is filled here.</summary>
    [Authorize]
    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities(CancellationToken ct)
        => Ok(ApiResponse<PublicCapabilitiesDto>.Ok(new PublicCapabilitiesDto { CanManageInformation = await CanManageAsync(ct) }));

    [Authorize]
    [HttpGet("manage")]
    public async Task<IActionResult> Manage(CancellationToken ct)
    {
        if (!await CanManageAsync(ct)) return Forbid();
        var items = await _service.GetManageListAsync(ct);
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Unauthorized();
        var allowed = new List<PublicInformationDto>();
        foreach (var item in items)
            if (await _operators.CanOperateAsync(user.UserId, user.EmployeeCode, SecurityFunctionCodes.PublicInformationManage, "PUBLIC_INFORMATION", item.Id, ct))
                allowed.Add(item);
        return Ok(allowed);
    }

    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id, CancellationToken ct)
    {
        if (!await CanOperateAsync(id, ct)) return Forbid();
        var item = await _service.GetAsync(id, ct);
        return item == null ? NotFound() : Ok(item);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePublicInformationRequest request, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Unauthorized();
        if (!await CanManageAsync(ct)) return Forbid();
        return Ok(await _service.CreateAsync(request, user.UserId, ct));
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SavePublicInformationRequest request, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Unauthorized();
        if (!await CanOperateAsync(id, ct)) return Forbid();
        return Ok(await _service.UpdateAsync(id, request, user.UserId, ct));
    }

    [Authorize]
    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Unauthorized();
        if (!await CanOperateAsync(id, ct)) return Forbid();
        return Ok(await _service.PublishAsync(id, user.UserId, ct));
    }

    [Authorize]
    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Unauthorized();
        if (!await CanOperateAsync(id, ct)) return Forbid();
        return Ok(await _service.ArchiveAsync(id, user.UserId, ct));
    }

    // ---- attachments: signed / stamped documents ---------------------------------------------------------------

    [Authorize]
    [HttpPost("{id:int}/attachments")]
    [RequestSizeLimit(11_000_000)]
    public async Task<IActionResult> UploadAttachment(int id, IFormFile file, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Unauthorized();
        if (!await CanOperateAsync(id, ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn file."));

        await using var stream = file.OpenReadStream();
        var result = await _service.AddAttachmentAsync(id, file.FileName, file.Length, stream, user.UserId, ct);
        return result.IsSuccess ? Ok(ApiResponse<AttachmentDto>.FromResult(result)) : BadRequest(ApiResponse<AttachmentDto>.FromResult(result));
    }

    [Authorize]
    [HttpDelete("{id:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> RemoveAttachment(int id, int attachmentId, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Unauthorized();
        if (!await CanOperateAsync(id, ct)) return Forbid();
        var result = await _service.RemoveAttachmentAsync(id, attachmentId, user.UserId, ct);
        return result.IsSuccess ? Ok(ApiResponse<object>.FromResult(result)) : BadRequest(ApiResponse<object>.FromResult(result));
    }

    /// <summary>Readers get files of Published items (same audience as the anonymous list); drafts/archived need an operator.</summary>
    [AllowAnonymous]
    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId, CancellationToken ct)
    {
        var result = await _service.OpenAttachmentAsync(id, attachmentId, publishedOnly: true, ct);
        if (!result.IsSuccess && User.Identity?.IsAuthenticated == true && await CanOperateAsync(id, ct))
            result = await _service.OpenAttachmentAsync(id, attachmentId, publishedOnly: false, ct);
        if (!result.IsSuccess || result.Data == null) return NotFound(ApiResponse<object>.Fail(result.Message ?? "Không tìm thấy file.", 404));
        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    private async Task<bool> CanOperateAsync(int id, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        return user != null
            && await _authorization.HasAsync(user, SecurityFunctionCodes.PublicInformationManage, ct)
            && await _operators.CanOperateAsync(user.UserId, user.EmployeeCode, SecurityFunctionCodes.PublicInformationManage, "PUBLIC_INFORMATION", id, ct);
    }

    /// <summary>
    /// Module-level gate (create / list / open the admin page): RBAC PublicInformation.Manage AND, when operators
    /// were assigned in Admin/Security, the user must be one of them. With no assignment configured the RBAC
    /// grant alone is enough (same fallback CanOperateAsync already applies per item).
    /// </summary>
    private async Task<bool> CanManageAsync(CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        return user != null
            && await _authorization.HasAsync(user, SecurityFunctionCodes.PublicInformationManage, ct)
            && await _operators.CanOperateAsync(user.UserId, user.EmployeeCode, SecurityFunctionCodes.PublicInformationManage, "PUBLIC_INFORMATION", null, ct);
    }
}
