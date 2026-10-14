using FVN_REGISTER.Application.Interfaces.PublicForms;
using FVN_REGISTER.Application.Interfaces.PublicInformation;
using FVN_REGISTER.Contract.Dtos.PublicForms;
using FVN_REGISTER.Contract.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FVN_REGISTER.API.Controllers;

/// <summary>
/// Anonymous, read-only endpoints of the public portal (login-page badge + public notices page).
/// Nothing here exposes personal data: public information is already published for everybody and forms are
/// limited to the AllCompany audience. Submitting a form still requires login (PublicFormsController).
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("EndpointAnonymous")]
[Route("api/public-portal")]
public sealed class PublicPortalController : ControllerBase
{
    private readonly IPublicInformationService _information;
    private readonly IPublicFormService _forms;

    public PublicPortalController(IPublicInformationService information, IPublicFormService forms)
    {
        _information = information;
        _forms = forms;
    }

    /// <summary>Number of effective notices + open company-wide forms. Drives the badge on the login page.</summary>
    [HttpGet("summary")]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var information = await _information.GetPublishedSummaryAsync(ct);
        var forms = await _forms.GetOpenForAllCompanyAsync(ct);

        var dto = new PublicNoticeSummaryDto
        {
            InformationCount = information.Count,
            FormCount = forms.IsSuccess ? forms.Data?.Count ?? 0 : 0,
            HasImportant = information.HasImportant,
            LatestPublishedAt = information.LatestPublishedAt
        };
        return Ok(ApiResponse<PublicNoticeSummaryDto>.Ok(dto));
    }

    /// <summary>Company-wide registration forms that are open now (title / description / deadline only).</summary>
    [HttpGet("forms")]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Forms(CancellationToken ct)
    {
        var result = await _forms.GetOpenForAllCompanyAsync(ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<List<PublicFormNoticeDto>>.Fail(result.Message ?? "Không tải được danh sách biểu mẫu."));
        return Ok(ApiResponse<List<PublicFormNoticeDto>>.Ok(result.Data ?? new List<PublicFormNoticeDto>()));
    }
}
