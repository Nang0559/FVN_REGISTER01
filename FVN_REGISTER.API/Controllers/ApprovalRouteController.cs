using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.OT;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/approval-route")]
public sealed class ApprovalRouteController : ControllerBase
{
    private readonly IApprovalRouteService _routeService;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IOtRegistrationPolicy _registrationPolicy;

    public ApprovalRouteController(
        IApprovalRouteService routeService,
        ICurrentUserService currentUser,
        IAuthorizationService authorization,
        IOtRegistrationPolicy registrationPolicy)
    {
        _routeService = routeService;
        _currentUser = currentUser;
        _authorization = authorization;
        _registrationPolicy = registrationPolicy;
    }

    [HttpGet("preview")]
    public async Task<ActionResult<ApiResponse<ApprovalRoutePreviewDto>>> Preview(
        [FromQuery] RequestModule requestType,
        [FromQuery] string? employeeCode,
        CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();

        if (user == null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(user.EmployeeCode))
            return BadRequest(
                ApiResponse<ApprovalRoutePreviewDto>.Fail(
                    "Tài khoản chưa có EmployeeCode."));

        var functionCode = requestType switch
        {
            RequestModule.Leave => SecurityFunctionCodes.LeaveCreate,
            RequestModule.Overtime => SecurityFunctionCodes.OTCreate,
            RequestModule.Trip => SecurityFunctionCodes.TripCreate,
            RequestModule.Equipment => SecurityFunctionCodes.EquipmentCreate,
            RequestModule.LateEarly => SecurityFunctionCodes.LateEarlyCreate,
            _ => 0
        };

        if (functionCode == 0)
        {
            return BadRequest(
                ApiResponse<ApprovalRoutePreviewDto>.Fail(
                    "Loại yêu cầu không hỗ trợ."));
        }

        if (!await _authorization.HasAsync(user, functionCode, ct))
            return Forbid();

        // Resolve department and position from employee master, not client input.
        var requesterCode = string.IsNullOrWhiteSpace(employeeCode) ? user.EmployeeCode : employeeCode.Trim();
        if (requestType == RequestModule.LateEarly
            && !string.Equals(requesterCode, user.EmployeeCode, StringComparison.OrdinalIgnoreCase)
            && !await _registrationPolicy.CanRegisterForEmployeesAsync(user, new[] { requesterCode }, "LATE_EARLY", ct))
            return Forbid();

        var result = await _routeService.GetPreviewAsync(requestType, requesterCode, ct);

        var response = ApiResponse<ApprovalRoutePreviewDto>.FromResult(result);

        if (!result.IsSuccess)
            return UnprocessableEntity(response);

        return Ok(response);
    }
}
