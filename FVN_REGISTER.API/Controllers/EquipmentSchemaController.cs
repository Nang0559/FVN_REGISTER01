using FVN_REGISTER.Application.Interfaces.Equipment;
using FvnAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Depts;
using FVN_REGISTER.Contract.Dtos.EquipmentImport;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FVN_REGISTER.Contract.Dtos.Equipment;
using FVN_REGISTER.Contract.Responses;

namespace FVN_REGISTER.API.Controllers;

[ApiController]
[Route("api/equipment/schemas")]
[Authorize]
public sealed class EquipmentSchemaController : ControllerBase
{
    private readonly IEquipmentImportService _service;
    private readonly FVNWEBAPPContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly FvnAuthorizationService _authorization;

    public EquipmentSchemaController(IEquipmentImportService service, FVNWEBAPPContext db, ICurrentUserService currentUser, FvnAuthorizationService authorization)
    {
        _service = service; _db = db; _currentUser = currentUser; _authorization = authorization;
    }

    [HttpGet("departments")]
    public async Task<ActionResult<List<DepartmentDto>>> Departments(CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        var user = _currentUser.GetCurrentUser();
        if (user == null) return Unauthorized();
        var query = _db.Departments.AsNoTracking().Where(x => x.IsActive == true);
        if (!user.IsAdmin && user.DeptCode != null) query = query.Where(x => x.DeptCode == user.DeptCode);
        return Ok(await query.OrderBy(x => x.DeptCode).Select(x => new DepartmentDto { Id=x.Id, DeptCode=x.DeptCode, DeptName=x.DeptName, IsActive=x.IsActive == true, CreatedAt=x.CreatedAt }).ToListAsync(ct));
    }

    [HttpGet]
    public async Task<ActionResult<List<ExcelSchemaSummaryDto>>> List([FromQuery] int? deptCode, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        return Ok(await _service.GetSchemasAsync(deptCode, ct));
    }

    [HttpGet("{schemaId:int}")]
    public async Task<ActionResult<ExcelSchemaDto>> Get(int schemaId, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        var result = await _service.GetSchemaAsync(schemaId, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ExcelSchemaDto>> Save([FromBody] ExcelSchemaUpsertRequest request, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        var result = await _service.SaveSchemaAsync(request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<ExcelSchemaDto>.FromResult(result))
            : BadRequest(ApiResponse<ExcelSchemaDto>.FromResult(result));
    }

    [HttpPost("{schemaId:int}/clone")]
    public async Task<ActionResult<ExcelSchemaDto>> Clone(int schemaId, [FromBody] ExcelSchemaCloneRequest request, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        var result = await _service.CloneSchemaAsync(schemaId, request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<ExcelSchemaDto>.FromResult(result))
            : BadRequest(ApiResponse<ExcelSchemaDto>.FromResult(result));
    }

    [HttpPost("{schemaId:int}/version")]
    public async Task<ActionResult<ExcelSchemaDto>> CreateVersion(int schemaId, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        var result = await _service.CreateVersionAsync(schemaId, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<ExcelSchemaDto>.FromResult(result))
            : BadRequest(ApiResponse<ExcelSchemaDto>.FromResult(result));
    }

    [HttpDelete("{schemaId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int schemaId,CancellationToken ct)
    {
        if(!await CanAsync(ct))return Forbid();
        var result=await _service.DeleteDraftSchemaAsync(schemaId,ct);
        return result.IsSuccess?Ok(ApiResponse<bool>.FromResult(result)):BadRequest(ApiResponse<bool>.FromResult(result));
    }

    [HttpPut("fields")]
    public async Task<ActionResult<ExcelSchemaFieldDto>> SaveField([FromBody] SaveExcelSchemaFieldRequest request, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        var result = await _service.SaveSchemaFieldAsync(request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<ExcelSchemaFieldDto>.FromResult(result))
            : BadRequest(ApiResponse<ExcelSchemaFieldDto>.FromResult(result));
    }

    [HttpPost("inspect-excel")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<ExcelWorkbookDto>>> InspectExcel([FromForm] IFormFile? file, [FromQuery] int deptCode, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<ExcelWorkbookDto>.Fail("File Excel rỗng."));
        await using var stream = file.OpenReadStream();
        var result = await _service.InspectExcelAsync(deptCode, file.FileName, stream, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<ExcelWorkbookDto>.FromResult(result))
            : BadRequest(ApiResponse<ExcelWorkbookDto>.FromResult(result));
    }

    [HttpPost("preview-excel")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<ExcelSchemaFromExcelDto>>> PreviewExcel([FromForm] IFormFile? file, [FromQuery] int deptCode, [FromQuery] int sheetIndex = 0, CancellationToken ct = default)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<ExcelSchemaFromExcelDto>.Fail("File Excel rỗng."));
        if (sheetIndex < 0) return BadRequest(ApiResponse<ExcelSchemaFromExcelDto>.Fail("Sheet Excel không hợp lệ."));
        await using var stream = file.OpenReadStream();
        var result = await _service.PreviewSchemaFromExcelSheetAsync(deptCode, file.FileName, stream, sheetIndex, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<ExcelSchemaFromExcelDto>.FromResult(result))
            : BadRequest(ApiResponse<ExcelSchemaFromExcelDto>.FromResult(result));
    }

    [HttpPost("from-excel")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<ExcelSchemaDto>>> FromExcel([FromForm] IFormFile? file, [FromQuery] int deptCode, [FromQuery] int sheetIndex = 0, [FromQuery] string? schemaName = null, CancellationToken ct = default)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<ExcelSchemaDto>.Fail("File Excel rỗng."));
        if (sheetIndex < 0) return BadRequest(ApiResponse<ExcelSchemaDto>.Fail("Sheet Excel không hợp lệ."));
        await using var stream = file.OpenReadStream();
        var result = await _service.CreateSchemaFromExcelSheetAsync(deptCode, file.FileName, stream, sheetIndex, schemaName, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<ExcelSchemaDto>.FromResult(result))
            : BadRequest(ApiResponse<ExcelSchemaDto>.FromResult(result));
    }

    /// <summary>Trả về lưới ô thô của một sheet để người dùng preview và tự chọn dòng tiêu đề/cột/vùng dữ liệu trước khi lấy schema.</summary>
    [HttpPost("excel-grid")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<ExcelGridDto>>> ExcelGrid([FromForm] IFormFile? file, [FromQuery] int deptCode, [FromQuery] int sheetIndex = 0, [FromQuery] int maxRows = 200, CancellationToken ct = default)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<ExcelGridDto>.Fail("File Excel rỗng."));
        if (sheetIndex < 0) return BadRequest(ApiResponse<ExcelGridDto>.Fail("Sheet Excel không hợp lệ."));
        await using var stream = file.OpenReadStream();
        var result = await _service.GetExcelGridAsync(deptCode, file.FileName, stream, sheetIndex, maxRows, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<ExcelGridDto>.FromResult(result))
            : BadRequest(ApiResponse<ExcelGridDto>.FromResult(result));
    }

    /// <summary>Suy luận schema đúng theo vùng (dòng tiêu đề + cột + vùng dữ liệu) người dùng đã chọn trên preview.</summary>
    [HttpPost("preview-excel-range")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<ExcelSchemaFromExcelDto>>> PreviewExcelRange([FromForm] IFormFile? file, [FromForm] string range, [FromQuery] int deptCode, CancellationToken ct = default)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<ExcelSchemaFromExcelDto>.Fail("File Excel rỗng."));
        var parsed = ParseRange(range);
        if (parsed == null) return BadRequest(ApiResponse<ExcelSchemaFromExcelDto>.Fail("Vùng dữ liệu đã chọn không hợp lệ."));
        await using var stream = file.OpenReadStream();
        var result = await _service.PreviewSchemaFromExcelRangeAsync(deptCode, file.FileName, stream, parsed, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<ExcelSchemaFromExcelDto>.FromResult(result))
            : BadRequest(ApiResponse<ExcelSchemaFromExcelDto>.FromResult(result));
    }

    /// <summary>Tạo mẫu dữ liệu (Draft) đúng theo vùng người dùng đã chọn trên preview.</summary>
    [HttpPost("from-excel-range")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<ExcelSchemaDto>>> FromExcelRange([FromForm] IFormFile? file, [FromForm] string range, [FromQuery] int deptCode, [FromQuery] string? schemaName = null, CancellationToken ct = default)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<ExcelSchemaDto>.Fail("File Excel rỗng."));
        var parsed = ParseRange(range);
        if (parsed == null) return BadRequest(ApiResponse<ExcelSchemaDto>.Fail("Vùng dữ liệu đã chọn không hợp lệ."));
        await using var stream = file.OpenReadStream();
        var result = await _service.CreateSchemaFromExcelRangeAsync(deptCode, file.FileName, stream, parsed, schemaName, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<ExcelSchemaDto>.FromResult(result))
            : BadRequest(ApiResponse<ExcelSchemaDto>.FromResult(result));
    }

    private static ExcelRangeRequest? ParseRange(string? rangeJson)
    {
        if (string.IsNullOrWhiteSpace(rangeJson)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<ExcelRangeRequest>(rangeJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    private async Task<bool> CanAsync(CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        return user != null && await _authorization.HasAsync(user, SecurityFunctionCodes.EquipmentImport, ct);
    }
}
