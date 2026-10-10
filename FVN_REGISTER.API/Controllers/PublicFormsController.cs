using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.PublicForms;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Logging;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

[ApiController]
[Route("api/public-forms")]
[Authorize]
public sealed class PublicFormsController : BaseApiController
{
    private const int AdministrationDepartmentCode = 13;

    private readonly IPublicFormService _service;
    private readonly IAuthorizationService _authorization;

    public PublicFormsController(
        IPublicFormService service,
        ICurrentUserService currentUser,
        IUserLogService userLog,
        IAuthorizationService authorization,
        ILogger<PublicFormsController> logger,
        IOptionsMonitor<AuthDebugOptions> options)
        : base(currentUser, userLog, logger, options)
    {
        _service = service;
        _authorization = authorization;
    }

    [HttpGet("manage")]
    public async Task<IActionResult> Manage(CancellationToken ct)
    {
        if (!await CanAnyManagementAsync(ct, SecurityFunctionCodes.PublicFormCreate, SecurityFunctionCodes.PublicFormEdit))
            return Forbid();

        return HandleResult(await _service.GetManageListAsync(ct));
    }

    [HttpGet("available")]
    public async Task<IActionResult> Available(CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        if (!await _authorization.HasPersonalAsync(UserInfo, SecurityFunctionCodes.PublicFormView, ct))
            return Forbid();

        return HandleResult(await _service.GetAvailableAsync(
            UserInfo.EmployeeCode ?? string.Empty,
            UserInfo.DeptCode,
            UserInfo.PositionCode,
            ct));
    }

    [HttpGet("audience/departments")]
    public async Task<IActionResult> AudienceDepartments(CancellationToken ct)
    {
        if (!await CanManagementAsync(SecurityFunctionCodes.PublicFormAssignAudience, ct))
            return Forbid();

        return HandleResult(await _service.GetAudienceDepartmentsAsync(ct));
    }

    [HttpGet("audience/positions")]
    public async Task<IActionResult> AudiencePositions(CancellationToken ct)
    {
        if (!await CanManagementAsync(SecurityFunctionCodes.PublicFormAssignAudience, ct))
            return Forbid();

        return HandleResult(await _service.GetAudiencePositionsAsync(ct));
    }

    [HttpGet("audience/employees")]
    public async Task<IActionResult> AudienceEmployees(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (!await CanManagementAsync(SecurityFunctionCodes.PublicFormAssignAudience, ct))
            return Forbid();

        return HandleResult(await _service.SearchAudienceEmployeesAsync(search, page, pageSize, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();

        var canManage = await CanAnyManagementAsync(ct,
            SecurityFunctionCodes.PublicFormCreate,
            SecurityFunctionCodes.PublicFormEdit,
            SecurityFunctionCodes.PublicFormAssignAudience,
            SecurityFunctionCodes.PublicFormResultView);

        if (!canManage &&
            !await _authorization.HasPersonalAsync(UserInfo, SecurityFunctionCodes.PublicFormView, ct))
            return Forbid();

        var result = await _service.GetAsync(id, ct);
        if (!result.IsSuccess)
            return NotFound(ApiResponse<object>.FromResult(result));

        if (!canManage)
        {
            var available = await _service.GetAvailableAsync(
                UserInfo.EmployeeCode ?? string.Empty,
                UserInfo.DeptCode,
                UserInfo.PositionCode,
                ct);

            if (!available.IsSuccess ||
                !(available.Data ?? new List<Contract.Dtos.PublicForms.PublicFormDto>()).Any(x => x.Id == id))
                return Forbid();
        }

        return Ok(ApiResponse<Contract.Dtos.PublicForms.PublicFormDto>.FromResult(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] Contract.Requests.PublicForms.SavePublicFormRequest request,
        CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        if (!await CanManagementAsync(SecurityFunctionCodes.PublicFormCreate, ct))
            return Forbid();

        var result = await _service.CreateAsync(request, UserInfo.UserId, ct);
        if (result.IsSuccess)
            await LogActionAsync($"Tạo biểu mẫu {request.FormCode}");

        return HandleResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] Contract.Requests.PublicForms.SavePublicFormRequest request,
        CancellationToken ct)
    {
        if (!await CanManagementAsync(SecurityFunctionCodes.PublicFormEdit, ct))
            return Forbid();

        if (UserInfo == null) return Unauthorized();
        return HandleResult(await _service.UpdateAsync(id, request, UserInfo.UserId, ct));
    }

    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id, CancellationToken ct)
    {
        if (!await CanManagementAsync(SecurityFunctionCodes.PublicFormEdit, ct))
            return Forbid();

        if (UserInfo == null) return Unauthorized();
        var result = await _service.PublishAsync(id, UserInfo.UserId, ct);
        if (result.IsSuccess)
            await LogActionAsync($"Publish biểu mẫu {id}");

        return HandleResult(result);
    }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id, CancellationToken ct)
    {
        if (!await CanManagementAsync(SecurityFunctionCodes.PublicFormEdit, ct))
            return Forbid();

        if (UserInfo == null) return Unauthorized();
        var result = await _service.CloseAsync(id, UserInfo.UserId, ct);
        if (result.IsSuccess)
            await LogActionAsync($"Đóng biểu mẫu {id}");

        return HandleResult(result);
    }

    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(
        int id,
        [FromBody] List<Contract.Requests.PublicForms.PublicFormAnswerRequest> answers,
        CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        if (!await _authorization.HasPersonalAsync(UserInfo, SecurityFunctionCodes.PublicFormSubmit, ct))
            return Forbid();

        return HandleResult(await _service.SubmitAsync(
            id,
            UserInfo.EmployeeCode ?? string.Empty,
            UserInfo.DeptCode,
            UserInfo.PositionCode,
            answers,
            ct));
    }

    [HttpPost("{id:int}/feedback")]
    public async Task<IActionResult> Feedback(
        int id,
        [FromBody] Contract.Requests.PublicForms.PublicFormFeedbackRequest request,
        CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        if (!await _authorization.HasPersonalAsync(UserInfo, SecurityFunctionCodes.PublicFormFeedback, ct))
            return Forbid();

        return HandleResult(await _service.SubmitFeedbackAsync(
            id,
            UserInfo.EmployeeCode ?? string.Empty,
            UserInfo.DeptCode,
            UserInfo.PositionCode,
            request,
            ct));
    }

    [HttpGet("submissions/forms")]
    public async Task<IActionResult> SubmissionForms(CancellationToken ct)
    {
        if (!await CanManagementAsync(SecurityFunctionCodes.PublicFormResultView, ct))
            return Forbid();

        return HandleResult(await _service.GetSubmissionFormsAsync(ct));
    }

    [HttpGet("{id:int}/submissions")]
    public async Task<IActionResult> Submissions(
        int id,
        [FromQuery] Contract.Dtos.PublicForms.PublicFormSubmissionQueryDto query,
        CancellationToken ct)
    {
        if (!await CanManagementAsync(SecurityFunctionCodes.PublicFormResultView, ct))
            return Forbid();

        var scope = await _authorization.GetScopeAsync(
            UserInfo!.UserId,
            SecurityFunctionCodes.PublicFormResultView,
            ct);

        if (string.Equals(scope, AuthorizationScopeCodes.Department, StringComparison.OrdinalIgnoreCase))
            query.DepartmentCode = UserInfo.DeptCode;

        return HandleResult(await _service.GetSubmissionsAsync(id, query, scope, ct));
    }

    [HttpGet("{id:int}/submissions/summary")]
    public async Task<IActionResult> SubmissionSummary(
        int id,
        [FromQuery] Contract.Dtos.PublicForms.PublicFormSubmissionQueryDto query,
        CancellationToken ct)
    {
        if (!await CanManagementAsync(SecurityFunctionCodes.PublicFormResultView, ct))
            return Forbid();

        var scope = await _authorization.GetScopeAsync(
            UserInfo!.UserId,
            SecurityFunctionCodes.PublicFormResultView,
            ct);

        if (string.Equals(scope, AuthorizationScopeCodes.Department, StringComparison.OrdinalIgnoreCase))
            query.DepartmentCode = UserInfo.DeptCode;

        return HandleResult(await _service.GetSubmissionSummaryAsync(id, query, scope, ct));
    }

    [HttpGet("{id:int}/submissions/export")]
    public async Task<IActionResult> ExportSubmissions(
        int id,
        [FromQuery] Contract.Dtos.PublicForms.PublicFormSubmissionQueryDto query,
        CancellationToken ct)
    {
        if (!await CanManagementAsync(SecurityFunctionCodes.PublicFormResultExport, ct))
            return Forbid();

        var scope = await _authorization.GetScopeAsync(
            UserInfo!.UserId,
            SecurityFunctionCodes.PublicFormResultExport,
            ct);

        if (string.Equals(scope, AuthorizationScopeCodes.Department, StringComparison.OrdinalIgnoreCase))
            query.DepartmentCode = UserInfo.DeptCode;

        var result = await _service.ExportSubmissionsAsync(id, query, scope, ct);
        if (!result.IsSuccess || result.Data == null)
            return HandleResult(result);

        return File(
            result.Data,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"PublicForm_{id}_Submissions_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
    }

    [HttpGet("{id:int}/audit")]
    public async Task<IActionResult> Audit(int id, CancellationToken ct)
    {
        if (!await CanManagementAsync(SecurityFunctionCodes.PublicFormAuditView, ct))
            return Forbid();

        return HandleResult(await _service.GetAuditHistoryAsync(id, ct));
    }

    private async Task<bool> CanManagementAsync(int functionCode, CancellationToken ct)
    {
        if (UserInfo == null ||
            !await _authorization.HasManagementAsync(UserInfo, functionCode, ct))
            return false;

        return UserInfo.DeptCode == AdministrationDepartmentCode;
    }

    private async Task<bool> CanAnyManagementAsync(CancellationToken ct, params int[] functionCodes)
    {
        foreach (var functionCode in functionCodes)
            if (await CanManagementAsync(functionCode, ct))
                return true;

        return false;
    }
}
