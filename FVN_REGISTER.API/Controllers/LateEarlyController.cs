using FVN_REGISTER.Application.Interfaces.LateEarly;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Requests.LateEarly;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Contract.Responses.LateEarly;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

[ApiController]
[Route("api/late-early")]
[Authorize]
public sealed class LateEarlyController : ControllerBase
{
    private readonly ILateEarlyService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;

    public LateEarlyController(
        ILateEarlyService service,
        ICurrentUserService currentUser,
        IAuthorizationService authorization)
    {
        _service = service;
        _currentUser = currentUser;
        _authorization = authorization;
    }

    [HttpPost]
    public async Task<ActionResult<LateEarlyRequestDto>> Create(
        [FromBody] CreateLateEarlyRequestDto request, CancellationToken ct)
    {
        if (!await CanPersonalAsync(SecurityFunctionCodes.LateEarlyCreate, ct)) return Forbid();
        var result = await _service.CreateDraftAsync(request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<LateEarlyRequestDto>.FromResult(result))
            : BadRequest(ApiResponse<LateEarlyRequestDto>.FromResult(result));
    }

    [HttpPost("{id:int}/submit")]
    public async Task<ActionResult<LateEarlyRequestDto>> Submit(
        int id, [FromBody] List<FVN_REGISTER.Contract.Requests.Approvals.ApprovalSelectionDto>? selections,
        CancellationToken ct)
    {
        if (!await CanPersonalAsync(SecurityFunctionCodes.LateEarlyEdit, ct)) return Forbid();
        var result = await _service.SubmitAsync(id, selections, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<LateEarlyRequestDto>.FromResult(result))
            : BadRequest(ApiResponse<LateEarlyRequestDto>.FromResult(result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LateEarlyRequestDto>> Get(int id, CancellationToken ct)
    {
        if (!await CanAsync(SecurityFunctionCodes.LateEarlyView, ct)) return Forbid();
        var result = await _service.GetAsync(id, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<LateEarlyRequestDto>.FromResult(result))
            : BadRequest(ApiResponse<LateEarlyRequestDto>.FromResult(result));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<LateEarlyRequestDto>>> Mine(CancellationToken ct)
    {
        if (!await CanPersonalAsync(SecurityFunctionCodes.LateEarlyView, ct)) return Forbid();
        var result = await _service.GetMineAsync(ct);
        return result.IsSuccess
            ? Ok(ApiResponse<List<LateEarlyRequestDto>>.FromResult(result))
            : BadRequest(ApiResponse<List<LateEarlyRequestDto>>.FromResult(result));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(
        int id, [FromBody] CancelLateEarlyRequestDto request, CancellationToken ct)
    {
        if (!await CanPersonalAsync(SecurityFunctionCodes.LateEarlyCancel, ct)) return Forbid();
        var result = await _service.CancelAsync(id, request.Reason, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<object>.FromResult(result))
            : BadRequest(ApiResponse<object>.FromResult(result));
    }

    private async Task<bool> CanAsync(int functionCode, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        return user != null && await _authorization.HasAsync(user, functionCode, ct);
    }

    private async Task<bool> CanPersonalAsync(int functionCode, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        return user != null && await _authorization.HasPersonalAsync(user, functionCode, ct);
    }
}

public sealed class CancelLateEarlyRequestDto
{
    public string? Reason { get; set; }
}
