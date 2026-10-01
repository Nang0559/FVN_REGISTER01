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
public sealed class ExecutionController : BaseApiController
{
    private readonly IExecutionReconciliationService _execution;
    private readonly FvnAuthorizationService _authorization;

    public ExecutionController(
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<ExecutionController> logger,
        IOptionsMonitor<AuthDebugOptions> options,
        IExecutionReconciliationService execution,
        FvnAuthorizationService authorization)
        : base(currentUser, userLog, logger, options)
    {
        _execution = execution;
        _authorization = authorization;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        if (UserInfo == null || !await _authorization.HasPersonalAsync(UserInfo, SecurityFunctionCodes.AttendanceView, ct))
            return Forbid();
        if (string.IsNullOrWhiteSpace(UserInfo?.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không có định danh nhân viên hợp lệ."));

        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = from ?? today.AddDays(-30);
        var last = to ?? today.AddDays(30);

        if (last < first)
            return BadRequest(ApiResponse<object>.Fail("Khoảng ngày không hợp lệ."));

        if (last.DayNumber - first.DayNumber > 93)
            return BadRequest(ApiResponse<object>.Fail("Đối soát chỉ cho phép tối đa 94 ngày mỗi lần tải."));

        return HandleResult(await _execution.GetMineAsync(UserInfo.EmployeeCode, first, last, ct));
    }

    [HttpPost("me/attendance-feedback")]
    public async Task<IActionResult> EnsureAttendanceFeedback(
        [FromQuery] DateOnly date,
        CancellationToken ct)
    {
        if (UserInfo == null || !await _authorization.HasPersonalAsync(UserInfo, SecurityFunctionCodes.AttendanceFeedback, ct))
            return Forbid();
        if (string.IsNullOrWhiteSpace(UserInfo?.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không có định danh nhân viên hợp lệ."));

        return HandleResult(await _execution.EnsureAttendanceFeedbackAsync(UserInfo.EmployeeCode, date, ct));
    }

    [HttpGet("me/{reconciliationId:long}")]
    public async Task<IActionResult> Get(long reconciliationId, CancellationToken ct)
    {
        if (UserInfo == null || !await _authorization.HasAsync(UserInfo, SecurityFunctionCodes.AttendanceView, ct))
            return Forbid();
        if (string.IsNullOrWhiteSpace(UserInfo?.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không có định danh nhân viên hợp lệ."));

        var result = await _execution.GetAsync(UserInfo.EmployeeCode, reconciliationId, ct);
        return result.IsSuccess ? Ok(ApiResponse<object>.FromResult(result)) : NotFound(ApiResponse<object>.FromResult(result));
    }

    [HttpGet("me/{reconciliationId:long}/detail")]
    public async Task<IActionResult> GetDetail(long reconciliationId, CancellationToken ct)
    {
        if (UserInfo == null || !await _authorization.HasAsync(UserInfo, SecurityFunctionCodes.AttendanceView, ct))
            return Forbid();
        if (string.IsNullOrWhiteSpace(UserInfo?.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không có định danh nhân viên hợp lệ."));

        var result = await _execution.GetDetailAsync(UserInfo.EmployeeCode, reconciliationId, ct);
        return result.IsSuccess ? Ok(ApiResponse<object>.FromResult(result)) : NotFound(ApiResponse<object>.FromResult(result));
    }

    [HttpPost("me/{reconciliationId:long}/confirmation")]
    public async Task<IActionResult> SubmitConfirmation(
        long reconciliationId,
        [FromBody] ExecutionConfirmationRequest request,
        CancellationToken ct)
    {
        if (UserInfo == null || !await _authorization.HasAsync(UserInfo, SecurityFunctionCodes.AttendanceFeedback, ct))
            return Forbid();
        if (string.IsNullOrWhiteSpace(UserInfo?.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không có định danh nhân viên hợp lệ."));

        return HandleResult(await _execution.SubmitConfirmationAsync(UserInfo.EmployeeCode, reconciliationId, request, ct));
    }

    [HttpPost("me/confirmations/{confirmationId:long}/evidence/upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadEvidence(
        long confirmationId,
        IFormFile file,
        CancellationToken ct)
    {
        if (UserInfo == null || !await _authorization.HasAsync(UserInfo, SecurityFunctionCodes.AttendanceFeedback, ct))
            return Forbid();
        if (UserInfo?.UserId is not int userId || string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không hợp lệ."));

        if (file is null || file.Length <= 0)
            return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn file evidence."));

        await using var stream = file.OpenReadStream();
        return HandleResult(await _execution.UploadEvidenceFileAsync(UserInfo.EmployeeCode,userId,confirmationId,file.FileName,file.ContentType,file.Length,stream,ct));
    }

    [HttpPost("me/confirmations/{confirmationId:long}/evidence")]
    public async Task<IActionResult> AddEvidence(
        long confirmationId,
        [FromBody] ExecutionEvidenceRequest request,
        CancellationToken ct)
    {
        if (UserInfo == null || !await _authorization.HasAsync(UserInfo, SecurityFunctionCodes.AttendanceFeedback, ct))
            return Forbid();
        if (UserInfo?.UserId is not int userId || string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))
            return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không có định danh người dùng hợp lệ."));

        return HandleResult(await _execution.AddEvidenceAsync(UserInfo.EmployeeCode,userId,confirmationId,request,ct));
    }

}