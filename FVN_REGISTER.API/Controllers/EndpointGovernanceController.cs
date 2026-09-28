using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Infrastructure.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

[ApiController]
[Route("api/security/endpoint-governance")]
[Authorize]
public sealed class EndpointGovernanceController : ControllerBase
{
    private readonly IEndpointGovernanceService _service;
    private readonly EndpointGovernanceExcelImportService _excelImport;
    private readonly ICurrentUserService _currentUser;
    private readonly AppAuthorizationService _authorization;

    public EndpointGovernanceController(IEndpointGovernanceService service, EndpointGovernanceExcelImportService excelImport, ICurrentUserService currentUser, AppAuthorizationService authorization)
    {
        _service = service;
        _excelImport = excelImport;
        _currentUser = currentUser;
        _authorization = authorization;
    }

    [HttpGet("policies")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EndpointGovernancePolicyDto>>>> GetPolicies([FromQuery] EndpointGovernanceItemType? itemType, [FromQuery] EndpointTargetType? targetType, CancellationToken ct)
    {
        var result = await _service.GetPoliciesAsync(itemType, targetType, ct);
        return result.Success ? Ok(ApiResponse<IReadOnlyList<EndpointGovernancePolicyDto>>.Ok(result.Data!)) : StatusCode(403, ApiResponse<IReadOnlyList<EndpointGovernancePolicyDto>>.Fail(result.Message ?? "Không có quyền."));
    }

    [HttpGet("policies/{policyId:int}")]
    public async Task<ActionResult<ApiResponse<EndpointGovernancePolicyDto>>> GetPolicy(int policyId, CancellationToken ct)
    {
        var result = await _service.GetPolicyAsync(policyId, ct);
        return result.Success ? Ok(ApiResponse<EndpointGovernancePolicyDto>.Ok(result.Data!)) : BadRequest(ApiResponse<EndpointGovernancePolicyDto>.Fail(result.Message ?? "Không thể tải policy."));
    }

    [HttpPost("policies/versions")]
    public async Task<ActionResult<ApiResponse<EndpointGovernancePolicyDto>>> CreateVersion(EndpointGovernancePolicyUpsertRequest request, CancellationToken ct)
    {
        var result = await _service.CreateVersionAsync(request, ct);
        return result.Success ? Ok(ApiResponse<EndpointGovernancePolicyDto>.Ok(result.Data!)) : BadRequest(ApiResponse<EndpointGovernancePolicyDto>.Fail(result.Message ?? "Không thể tạo policy version."));
    }

    [HttpPost("policies/{policyId:int}/submit")]
    public async Task<ActionResult<ApiResponse<EndpointGovernancePolicyDto>>> Submit(int policyId, CancellationToken ct)
    {
        var result = await _service.SubmitPolicyAsync(policyId, ct);
        return result.Success ? Ok(ApiResponse<EndpointGovernancePolicyDto>.Ok(result.Data!)) : BadRequest(ApiResponse<EndpointGovernancePolicyDto>.Fail(result.Message ?? "Không thể submit catalog."));
    }

    [HttpPost("policies/import/preview")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<EndpointGovernanceExcelPreviewDto>>> PreviewExcel(IFormFile file, [FromQuery] EndpointGovernanceItemType itemType, [FromQuery] EndpointTargetType targetType, CancellationToken ct)
    {
        if (!await HasManageAsync(itemType, ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<EndpointGovernanceExcelPreviewDto>.Fail("File Excel rỗng."));
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase)) return BadRequest(ApiResponse<EndpointGovernanceExcelPreviewDto>.Fail("Chỉ hỗ trợ .xlsx."));
        try
        {
            await using var stream = file.OpenReadStream();
            var preview = await _excelImport.PreviewAsync(stream, file.FileName, itemType, targetType, ct);
            return Ok(ApiResponse<EndpointGovernanceExcelPreviewDto>.Ok(preview));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException) { return BadRequest(ApiResponse<EndpointGovernanceExcelPreviewDto>.Fail(ex.Message)); }
    }

    [HttpPost("policies/import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<EndpointGovernancePolicyDto>>> ImportExcel(IFormFile file, [FromForm] string policyCode, [FromForm] string policyName, [FromForm] EndpointGovernanceItemType itemType, [FromForm] EndpointTargetType targetType, [FromForm] string? remark, CancellationToken ct)
    {
        if (!await HasManageAsync(itemType, ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<EndpointGovernancePolicyDto>.Fail("File Excel rỗng."));
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase)) return BadRequest(ApiResponse<EndpointGovernancePolicyDto>.Fail("Chỉ hỗ trợ .xlsx."));
        try
        {
            await using var stream = file.OpenReadStream();
            var preview = await _excelImport.PreviewAsync(stream, file.FileName, itemType, targetType, ct);
            if (preview.Errors.Count > 0) return BadRequest(ApiResponse<EndpointGovernancePolicyDto>.Fail(string.Join(" | ", preview.Errors)));
            var request = EndpointGovernanceExcelImportService.ToUpsert(preview, new EndpointGovernanceExcelImportRequest(policyCode, policyName, itemType, targetType, remark))
                with { ChecklistCompleted = true, ChecklistNote = "Excel preview passed: rows, duplicates and required columns validated." };
            var result = await _service.CreateVersionAsync(request, ct);
            return result.Success ? Ok(ApiResponse<EndpointGovernancePolicyDto>.Ok(result.Data!)) : BadRequest(ApiResponse<EndpointGovernancePolicyDto>.Fail(result.Message ?? "Không thể import catalog."));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or InvalidOperationException) { return BadRequest(ApiResponse<EndpointGovernancePolicyDto>.Fail(ex.Message)); }
    }

    [HttpPost("policies/{policyId:int}/publish")]
    public async Task<ActionResult<ApiResponse<EndpointGovernancePolicyDto>>> Publish(int policyId, CancellationToken ct)
    {
        var result = await _service.PublishAsync(policyId, ct);
        return result.Success ? Ok(ApiResponse<EndpointGovernancePolicyDto>.Ok(result.Data!)) : BadRequest(ApiResponse<EndpointGovernancePolicyDto>.Fail(result.Message ?? "Không thể publish policy."));
    }

    [HttpPost("requests")]
    public async Task<ActionResult<ApiResponse<EndpointGovernanceRequestDto>>> CreateRequest(EndpointGovernanceRequestCreateDto request, CancellationToken ct)
    {
        var result = await _service.CreateRequestAsync(request, ct);
        return result.Success ? Ok(ApiResponse<EndpointGovernanceRequestDto>.Ok(result.Data!)) : BadRequest(ApiResponse<EndpointGovernanceRequestDto>.Fail(result.Message ?? "Không thể tạo request."));
    }

    [HttpGet("requests")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EndpointGovernanceRequestDto>>>> GetRequests([FromQuery] bool mineOnly = true, CancellationToken ct = default)
    {
        var result = await _service.GetRequestsAsync(mineOnly, ct);
        return result.Success ? Ok(ApiResponse<IReadOnlyList<EndpointGovernanceRequestDto>>.Ok(result.Data!)) : StatusCode(403, ApiResponse<IReadOnlyList<EndpointGovernanceRequestDto>>.Fail(result.Message ?? "Không có quyền."));
    }

    [HttpPost("requests/{requestId:int}/security-review")]
    public async Task<ActionResult<ApiResponse<EndpointGovernanceRequestDto>>> Review(int requestId, [FromBody] EndpointSecurityReviewRequest request, CancellationToken ct)
    {
        var result = await _service.ReviewAsync(requestId, request.Approved, request.Comment, ct);
        return result.Success ? Ok(ApiResponse<EndpointGovernanceRequestDto>.Ok(result.Data!)) : BadRequest(ApiResponse<EndpointGovernanceRequestDto>.Fail(result.Message ?? "Không thể Security Review request."));
    }

    [HttpGet("compliance/findings")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EndpointComplianceFindingDto>>>> GetFindings([FromQuery] long? endpointDeviceId, [FromQuery] bool openOnly = true, CancellationToken ct = default)
    {
        var result = await _service.GetFindingsAsync(endpointDeviceId, openOnly, ct);
        return result.Success ? Ok(ApiResponse<IReadOnlyList<EndpointComplianceFindingDto>>.Ok(result.Data!)) : StatusCode(403, ApiResponse<IReadOnlyList<EndpointComplianceFindingDto>>.Fail(result.Message ?? "Không có quyền."));
    }

    private async Task<bool> HasManageAsync(EndpointGovernanceItemType itemType, CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user?.IsAdmin == true) return true;
        if (user == null) return false;
        var capability = itemType == EndpointGovernanceItemType.WindowsService ? SecurityFunctionCodes.EndpointServiceCatalogManage : SecurityFunctionCodes.EndpointSoftwareCatalogManage;
        return await _authorization.HasAsync(user, capability, ct);
    }

    public sealed record EndpointSecurityReviewRequest(bool Approved, string? Comment);
}
