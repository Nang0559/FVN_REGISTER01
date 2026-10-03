using FVN_REGISTER.Application.Interfaces.Equipment;
using FVN_REGISTER.Application.Interfaces.PublicForms;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.EquipmentForms;
using FVN_REGISTER.Contract.Dtos.PublicForms;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

[ApiController]
[Route("api/equipment/forms")]
[Authorize]
public sealed class EquipmentFormsController : ControllerBase
{
    private readonly IEquipmentFormService _service;
    private readonly IPublicFormService _publicForms;
    private readonly IAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;

    public EquipmentFormsController(IEquipmentFormService service, IPublicFormService publicForms, IAuthorizationService authorization, ICurrentUserService currentUser)
    { _service = service; _publicForms = publicForms; _authorization = authorization; _currentUser = currentUser; }

    [HttpGet("my/{assetId:int}")]
    public async Task<IActionResult> My(int assetId, CancellationToken ct) => ToResponse(await _service.GetMyApplicableFormsAsync(assetId, ct));

    [HttpGet("assignments/{equipmentSchemaKey}")]
    public async Task<IActionResult> Assignments(string equipmentSchemaKey, CancellationToken ct) => ToResponse(await _service.GetAssignmentsAsync(equipmentSchemaKey, ct));

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Unauthorized();
        if (!await _authorization.HasAsync(user, SecurityFunctionCodes.EquipmentFormManage, ct)) return Forbid();
        var formsResult = await _publicForms.GetManageListAsync(ct);
        if (!formsResult.IsSuccess)
            return BadRequest(FVN_REGISTER.Contract.Responses.ApiResponse<List<PublicFormDto>>.Fail(formsResult.Message ?? "Không thể tải danh mục biểu mẫu."));

        var forms = formsResult.Data ?? new List<PublicFormDto>();
        return Ok(FVN_REGISTER.Contract.Responses.ApiResponse<List<PublicFormDto>>.Ok(
            forms.Where(x => x.IsActive == true && string.Equals(x.Status, "Published", StringComparison.OrdinalIgnoreCase)).ToList()));
    }

    [HttpPost("assignments")]
    public async Task<IActionResult> Assign([FromBody] EquipmentFormAssignmentRequest request, CancellationToken ct) => ToResponse(await _service.AssignAsync(request, ct));

    [HttpPost("assignments/{id:int}/remove")]
    public async Task<IActionResult> RemoveAssignment(int id, CancellationToken ct) => ToResponse(await _service.RemoveAssignmentAsync(id, ct));

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] EquipmentFormSubmissionRequest request, CancellationToken ct) => ToResponse(await _service.SubmitAsync(request, ct));

    private static IActionResult ToResponse<T>(FVN_REGISTER.Contract.Utils.ServiceResult<T> result)
        => result.IsSuccess
            ? new OkObjectResult(FVN_REGISTER.Contract.Responses.ApiResponse<T>.Ok(result.Data!))
            : new BadRequestObjectResult(FVN_REGISTER.Contract.Responses.ApiResponse<T>.Fail(result.Message ?? "Không thể thực hiện thao tác."));
}
