using FVN_REGISTER.Application.Interfaces.Leaves;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Leaves;
using FVN_REGISTER.Contract.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using FVN_REGISTER.Application.Interfaces.Users;

namespace FVN_REGISTER.API.Controllers;

[ApiController]
[Route("api/leave-entitlements")]
[Authorize]
public sealed class LeaveEntitlementController : BaseApiController
{
    private readonly ILeaveEntitlementService _service;
    private readonly ICurrentUserService _currentUser;

    public LeaveEntitlementController(
        ILeaveEntitlementService service,
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<LeaveEntitlementController> logger,
        IOptionsMonitor<FVN_REGISTER.Application.Configuration.AuthDebugOptions> options)
        : base(currentUser, userLog, logger, options)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    public async Task<ActionResult<LeaveEntitlementDto>> GetMine(
        [FromQuery] int? year,
        CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null || string.IsNullOrWhiteSpace(user.EmployeeCode))
            return Unauthorized();

        var workYear = year ?? DateTime.Today.Year;
        return HandleResult(await _service.EnsureCalculatedAsync(user.EmployeeCode, workYear, ct));
    }
}
