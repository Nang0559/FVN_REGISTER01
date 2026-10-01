using FVN_REGISTER.Application.Interfaces.Language;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Language;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/localization")]
public sealed class LocalizationController : BaseApiController
{
    private readonly ILocalizationManagementService _service;
    private readonly FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService _authorization;

    public LocalizationController(
        ILocalizationManagementService service,
        FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService authorization,
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<LocalizationController> logger,
        IOptionsMonitor<FVN_REGISTER.Application.Configuration.AuthDebugOptions> options)
        : base(currentUser, userLog, logger, options)
    {
        _service = service;
        _authorization = authorization;
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken ct)
    {
        if (!await CanAsync(SecurityFunctionCodes.LanguageView, ct)) return Forbid();
        return Ok(ApiResponse<LocalizationCatalogDto>.Ok(await _service.GetCatalogAsync(ct)));
    }

    [HttpPost("entries")]
    public async Task<IActionResult> Upsert([FromBody] LocalizationUpsertRequest request, CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        if (!await CanAsync(SecurityFunctionCodes.LanguageManage, ct)) return Forbid();

        try
        {
            await _service.UpsertAsync(request, ct);
            await LogActionAsync($"Cập nhật định nghĩa ngôn ngữ: {request.Key}");
            return Ok(ApiResponse<object>.Ok(new { request.Key }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("entries/{key}")]
    public async Task<IActionResult> Delete(string key, CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        if (!await CanAsync(SecurityFunctionCodes.LanguageManage, ct)) return Forbid();

        try
        {
            await _service.DeleteAsync(key, ct);
            await LogActionAsync($"Xóa định nghĩa ngôn ngữ: {key}");
            return Ok(ApiResponse<object>.Ok(new { key }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("audit")]
    public async Task<IActionResult> Audit(CancellationToken ct)
    {
        if (!await CanAsync(SecurityFunctionCodes.LanguageAudit, ct)) return Forbid();
        return Ok(ApiResponse<LocalizationAuditResultDto>.Ok(await _service.AuditAsync(ct)));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!await CanAsync(SecurityFunctionCodes.LanguageExport, ct)) return Forbid();
        var result = await _service.ExportAsync(ct);
        await LogActionAsync("Xuất danh mục ngôn ngữ Excel");
        return File(result.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.FileName);
    }

    [HttpPost("import")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        if (!await CanAsync(SecurityFunctionCodes.LanguageImport, ct)) return Forbid();
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("File Excel rỗng."));

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _service.ImportAsync(stream, file.FileName, ct);
            if (result.Errors.Count > 0)
                return BadRequest(ApiResponse<LocalizationImportResultDto>.Fail(string.Join("\n", result.Errors)));

            await LogActionAsync($"Nhập danh mục ngôn ngữ từ Excel: {file.FileName}; {result.Imported} dòng");
            return Ok(ApiResponse<LocalizationImportResultDto>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private async Task<bool> CanAsync(int functionCode, CancellationToken ct) =>
        UserInfo != null && await _authorization.HasAsync(UserInfo, functionCode, ct);
}
