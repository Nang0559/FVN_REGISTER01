using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Execution;
using FVN_REGISTER.Application.Interfaces.FeatureOperators;
using FVN_REGISTER.Application.Interfaces.Security;
using FvnAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
namespace FVN_REGISTER.API.Controllers;
[Authorize]
[ApiController]
[Route("api/execution/hr")]
public sealed class ExecutionHrReviewController:BaseApiController
{
readonly IExecutionHrResolutionService _service; readonly IExecutionHrClaimService _claims; readonly IFeatureOperatorAssignmentService _operators; readonly FvnAuthorizationService _authorization;
public ExecutionHrReviewController(ICurrentUserService currentUser,IUserLogService userLog,ILogger<ExecutionHrReviewController> logger,IOptionsMonitor<AuthDebugOptions> options,IExecutionHrResolutionService service,IExecutionHrClaimService claims,IFeatureOperatorAssignmentService operators,FvnAuthorizationService authorization):base(currentUser,userLog,logger,options){_service=service;_claims=claims;_operators=operators;_authorization=authorization;}
[HttpGet("reconciliations")] public async Task<IActionResult> Get([FromQuery]string? moduleCode,[FromQuery]string? status,[FromQuery]DateOnly? from,[FromQuery]DateOnly? to,CancellationToken ct){if(UserInfo?.UserId is not int uid)return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không hợp lệ."));if(!await CanReviewAsync(ct))return Forbid();return HandleResult(await _service.GetPendingAsync(uid,moduleCode,status,from,to,ct));}
[HttpGet("reconciliations/{reconciliationId:long}/detail")] public async Task<IActionResult> GetDetail(long reconciliationId,CancellationToken ct){if(UserInfo?.UserId is not int uid||string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không hợp lệ."));if(!await CanReviewAsync(ct))return Forbid();var r=await _service.GetDetailAsync(uid,UserInfo.EmployeeCode,reconciliationId,ct);return r.IsSuccess?Ok(ApiResponse<object>.FromResult(r)):NotFound(ApiResponse<object>.FromResult(r));}
[HttpGet("reconciliations/claims")]
public async Task<IActionResult> GetClaims([FromQuery] long[] ids, CancellationToken ct)
{
    if (UserInfo?.UserId is not int uid || string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))
        return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không hợp lệ."));
    if (!await CanReviewAsync(ct))
        return Forbid();
    return HandleResult(await _claims.GetClaimsAsync(uid, UserInfo.EmployeeCode, ids, ct));
}

[HttpPost("reconciliations/{reconciliationId:long}/claim")]
public async Task<IActionResult> Claim(long reconciliationId, CancellationToken ct)
{
    if (UserInfo?.UserId is not int uid || string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))
        return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không hợp lệ."));
    if (!await CanReviewAsync(ct))
        return Forbid();
    return HandleResult(await _claims.ClaimAsync(uid, UserInfo.EmployeeCode, reconciliationId, ct));
}

[HttpPost("reconciliations/{reconciliationId:long}/release")]
public async Task<IActionResult> Release(long reconciliationId, CancellationToken ct)
{
    if (UserInfo?.UserId is not int uid || string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))
        return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không hợp lệ."));
    if (!await CanReviewAsync(ct))
        return Forbid();
    return HandleResult(await _claims.ReleaseAsync(uid, UserInfo.EmployeeCode, reconciliationId, ct));
}

[HttpPost("evidence/{evidenceId:long}/review")] public async Task<IActionResult> ReviewEvidence(long evidenceId,[FromBody]ExecutionEvidenceReviewRequest request,CancellationToken ct){if(UserInfo?.UserId is not int uid||string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không hợp lệ."));if(!await CanReviewAsync(ct))return Forbid();return HandleResult(await _service.ReviewEvidenceAsync(uid,UserInfo.EmployeeCode,evidenceId,request,ct));}
[HttpPost("reconciliations/{reconciliationId:long}/resolve")] public async Task<IActionResult> Resolve(long reconciliationId,[FromBody]ExecutionHrResolutionRequest request,CancellationToken ct){if(UserInfo?.UserId is not int uid||string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập không hợp lệ."));if(!await CanReviewAsync(ct))return Forbid();return HandleResult(await _service.ResolveAsync(uid,UserInfo.EmployeeCode,reconciliationId,request,ct));}
async Task<bool> CanReviewAsync(CancellationToken ct)
{
    if (UserInfo is null)
        return false;

    var hasExecutionReview = await _authorization.HasAsync(
        UserInfo,
        SecurityFunctionCodes.ExecutionReview,
        ct);

    var assignedOperator = await _operators.CanOperateAsync(
        UserInfo.UserId,
        UserInfo.EmployeeCode,
        SecurityFunctionCodes.ExecutionReview,
        "EXECUTION_REVIEW",
        null,
        ct);

    return hasExecutionReview || assignedOperator;
}
}