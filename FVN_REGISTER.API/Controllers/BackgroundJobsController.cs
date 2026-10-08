using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Jobs;
using AppAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Logging;
using FVN_REGISTER.Contract.Dtos.Jobs;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.API.Controllers;

/// <summary>Admin API to view and change the schedule of background jobs.</summary>
[Authorize]
[ApiController]
[Route("api/background-jobs")]
public sealed class BackgroundJobsController : BaseApiController
{
    private readonly IBackgroundJobScheduleService _schedules;
    private readonly AppAuthorizationService _authorization;

    public BackgroundJobsController(
        IBackgroundJobScheduleService schedules,
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<BackgroundJobsController> logger,
        IOptionsMonitor<AuthDebugOptions> options,
        AppAuthorizationService authorization)
        : base(currentUser, userLog, logger, options)
    {
        _schedules = schedules;
        _authorization = authorization;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (!await CanAsync(SecurityFunctionCodes.BackgroundJobView, ct))
            return Forbid();

        return HandleResult(await _schedules.GetAllAsync(ct));
    }

    [HttpPut("{jobKey}")]
    public async Task<IActionResult> Update(
        string jobKey, [FromBody] UpdateBackgroundJobScheduleRequest request, CancellationToken ct)
    {
        if (!await CanAsync(SecurityFunctionCodes.BackgroundJobManage, ct))
            return Forbid();

        if (request == null)
            return BadRequest("Dữ liệu không hợp lệ.");

        var result = await _schedules.UpdateAsync(jobKey, request, UserInfo?.UserId, ct);
        await LogActionAsync(
            $"Background job schedule: {jobKey} enabled={request.IsEnabled}, interval={request.IntervalMinutes}m");
        return HandleResult(result);
    }

    [HttpPost("{jobKey}/run-now")]
    public async Task<IActionResult> RunNow(string jobKey, CancellationToken ct)
    {
        if (!await CanAsync(SecurityFunctionCodes.BackgroundJobManage, ct))
            return Forbid();

        var result = await _schedules.RequestRunNowAsync(jobKey, UserInfo?.UserId, ct);
        await LogActionAsync($"Background job schedule: run now {jobKey}");
        return HandleResult(result);
    }

    [HttpPost("{jobKey}/reset")]
    public async Task<IActionResult> Reset(string jobKey, CancellationToken ct)
    {
        if (!await CanAsync(SecurityFunctionCodes.BackgroundJobManage, ct))
            return Forbid();

        var result = await _schedules.ResetToDefaultAsync(jobKey, UserInfo?.UserId, ct);
        await LogActionAsync($"Background job schedule: reset {jobKey}");
        return HandleResult(result);
    }

    private async Task<bool> CanAsync(int functionCode, CancellationToken ct)
        => UserInfo != null && await _authorization.HasAsync(UserInfo, functionCode, ct);
}
