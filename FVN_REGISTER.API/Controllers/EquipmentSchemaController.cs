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
        if (!user.IsAdmin && !string.IsNullOrWhiteSpace(user.DeptCode)) query = query.Where(x => x.DeptCode == user.DeptCode);
        return Ok(await query.OrderBy(x => x.DeptCode).Select(x => new DepartmentDto { Id=x.Id, DeptCode=x.DeptCode, DeptName=x.DeptName, IsActive=x.IsActive == true, CreatedAt=x.CreatedAt }).ToListAsync(ct));
    }

    [HttpGet]
    public async Task<ActionResult<List<EquipmentSchemaSummaryDto>>> List([FromQuery] string? deptCode, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        return Ok(await _service.GetSchemasAsync(deptCode, ct));
    }

    [HttpGet("{schemaId:int}")]
    public async Task<ActionResult<EquipmentSchemaDto>> Get(int schemaId, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        var result = await _service.GetSchemaAsync(schemaId, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<EquipmentSchemaDto>> Save([FromBody] EquipmentSchemaUpsertRequest request, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        var result = await _service.SaveSchemaAsync(request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<EquipmentSchemaDto>.FromResult(result))
            : BadRequest(ApiResponse<EquipmentSchemaDto>.FromResult(result));
    }

    [HttpPost("{schemaId:int}/clone")]
    public async Task<ActionResult<EquipmentSchemaDto>> Clone(int schemaId, [FromBody] EquipmentSchemaCloneRequest request, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        var result = await _service.CloneSchemaAsync(schemaId, request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<EquipmentSchemaDto>.FromResult(result))
            : BadRequest(ApiResponse<EquipmentSchemaDto>.FromResult(result));
    }

    [HttpPost("{schemaId:int}/version")]
    public async Task<ActionResult<EquipmentSchemaDto>> CreateVersion(int schemaId, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        var result = await _service.CreateVersionAsync(schemaId, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<EquipmentSchemaDto>.FromResult(result))
            : BadRequest(ApiResponse<EquipmentSchemaDto>.FromResult(result));
    }

    [HttpDelete("{schemaId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int schemaId, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();

        var user = _currentUser.GetCurrentUser();
        if (user == null) return Unauthorized();

        var schema = await _db.EquipmentSchemas
            .FirstOrDefaultAsync(x => x.Id == schemaId && x.IsActive == true, ct);
        if (schema == null)
            return NotFound(ApiResponse<bool>.Fail("Mẫu dữ liệu không tồn tại hoặc đã được xóa.", 404));

        if (!user.IsAdmin && schema.CreatedBy != user.UserId)
            return Forbid();

        if (!user.IsAdmin && !string.IsNullOrWhiteSpace(user.DeptCode) &&
            !string.Equals(schema.DeptCode, user.DeptCode, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        if (!string.Equals(schema.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<bool>.Fail("Chỉ được xóa mẫu dữ liệu ở trạng thái Bản nháp."));

        var hasActiveVersion = await _db.EquipmentSchemas
            .AsNoTracking()
            .AnyAsync(x => x.IsActive == true && x.SourceSchemaId == schemaId, ct);
        if (hasActiveVersion)
            return BadRequest(ApiResponse<bool>.Fail("Không thể xóa mẫu vì đã có phiên bản khác được tạo từ mẫu này."));

        var hasImportHistory = await _db.EquipmentImportBatches
            .AsNoTracking()
            .AnyAsync(x => x.SchemaId == schemaId, ct);
        if (hasImportHistory)
            return BadRequest(ApiResponse<bool>.Fail("Không thể xóa mẫu vì mẫu đã được sử dụng cho lịch sử import Excel."));

        schema.IsActive = false;
        schema.ModifiedBy = user.UserId;
        schema.ModifiedAt = DateTime.Now;
        schema.LastModifiedSource = "EquipmentSchema.DeleteDraft";
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<bool>.Ok(true, "Đã xóa mẫu dữ liệu bản nháp."));
    }

    [HttpPut("fields")]
    public async Task<ActionResult<EquipmentFieldDefinitionDto>> SaveField([FromBody] SaveEquipmentFieldDefinitionRequest request, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        var result = await _service.SaveFieldDefinitionAsync(request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<EquipmentFieldDefinitionDto>.FromResult(result))
            : BadRequest(ApiResponse<EquipmentFieldDefinitionDto>.FromResult(result));
    }

    [HttpPost("inspect-excel")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<EquipmentExcelWorkbookDto>>> InspectExcel([FromForm] IFormFile? file, [FromQuery] string deptCode, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<EquipmentExcelWorkbookDto>.Fail("File Excel rỗng."));
        await using var stream = file.OpenReadStream();
        var result = await _service.InspectExcelAsync(deptCode, file.FileName, stream, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<EquipmentExcelWorkbookDto>.FromResult(result))
            : BadRequest(ApiResponse<EquipmentExcelWorkbookDto>.FromResult(result));
    }

    [HttpPost("preview-excel")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<EquipmentSchemaFromExcelDto>>> PreviewExcel([FromForm] IFormFile? file, [FromQuery] string deptCode, [FromQuery] int sheetIndex = 0, CancellationToken ct = default)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<EquipmentSchemaFromExcelDto>.Fail("File Excel rỗng."));
        if (sheetIndex < 0) return BadRequest(ApiResponse<EquipmentSchemaFromExcelDto>.Fail("Sheet Excel không hợp lệ."));
        await using var stream = file.OpenReadStream();
        var result = await _service.PreviewSchemaFromExcelSheetAsync(deptCode, file.FileName, stream, sheetIndex, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<EquipmentSchemaFromExcelDto>.FromResult(result))
            : BadRequest(ApiResponse<EquipmentSchemaFromExcelDto>.FromResult(result));
    }

    [HttpPost("from-excel")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<EquipmentSchemaDto>>> FromExcel([FromForm] IFormFile? file, [FromQuery] string deptCode, [FromQuery] int sheetIndex = 0, [FromQuery] string? schemaName = null, CancellationToken ct = default)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<EquipmentSchemaDto>.Fail("File Excel rỗng."));
        if (sheetIndex < 0) return BadRequest(ApiResponse<EquipmentSchemaDto>.Fail("Sheet Excel không hợp lệ."));
        await using var stream = file.OpenReadStream();
        var result = await _service.CreateSchemaFromExcelSheetAsync(deptCode, file.FileName, stream, sheetIndex, schemaName, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<EquipmentSchemaDto>.FromResult(result))
            : BadRequest(ApiResponse<EquipmentSchemaDto>.FromResult(result));
    }

    /// <summary>Trả về lưới ô thô của một sheet để người dùng preview và tự chọn dòng tiêu đề/cột/vùng dữ liệu trước khi lấy schema.</summary>
    [HttpPost("excel-grid")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<EquipmentExcelGridDto>>> ExcelGrid([FromForm] IFormFile? file, [FromQuery] string deptCode, [FromQuery] int sheetIndex = 0, [FromQuery] int maxRows = 200, CancellationToken ct = default)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<EquipmentExcelGridDto>.Fail("File Excel rỗng."));
        if (sheetIndex < 0) return BadRequest(ApiResponse<EquipmentExcelGridDto>.Fail("Sheet Excel không hợp lệ."));
        await using var stream = file.OpenReadStream();
        var result = await _service.GetExcelGridAsync(deptCode, file.FileName, stream, sheetIndex, maxRows, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<EquipmentExcelGridDto>.FromResult(result))
            : BadRequest(ApiResponse<EquipmentExcelGridDto>.FromResult(result));
    }

    /// <summary>Suy luận schema đúng theo vùng (dòng tiêu đề + cột + vùng dữ liệu) người dùng đã chọn trên preview.</summary>
    [HttpPost("preview-excel-range")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<EquipmentSchemaFromExcelDto>>> PreviewExcelRange([FromForm] IFormFile? file, [FromForm] string range, [FromQuery] string deptCode, CancellationToken ct = default)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<EquipmentSchemaFromExcelDto>.Fail("File Excel rỗng."));
        var parsed = ParseRange(range);
        if (parsed == null) return BadRequest(ApiResponse<EquipmentSchemaFromExcelDto>.Fail("Vùng dữ liệu đã chọn không hợp lệ."));
        await using var stream = file.OpenReadStream();
        var result = await _service.PreviewSchemaFromExcelRangeAsync(deptCode, file.FileName, stream, parsed, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<EquipmentSchemaFromExcelDto>.FromResult(result))
            : BadRequest(ApiResponse<EquipmentSchemaFromExcelDto>.FromResult(result));
    }

    /// <summary>Tạo mẫu dữ liệu (Draft) đúng theo vùng người dùng đã chọn trên preview.</summary>
    [HttpPost("from-excel-range")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<EquipmentSchemaDto>>> FromExcelRange([FromForm] IFormFile? file, [FromForm] string range, [FromQuery] string deptCode, [FromQuery] string? schemaName = null, CancellationToken ct = default)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest(ApiResponse<EquipmentSchemaDto>.Fail("File Excel rỗng."));
        var parsed = ParseRange(range);
        if (parsed == null) return BadRequest(ApiResponse<EquipmentSchemaDto>.Fail("Vùng dữ liệu đã chọn không hợp lệ."));
        await using var stream = file.OpenReadStream();
        var result = await _service.CreateSchemaFromExcelRangeAsync(deptCode, file.FileName, stream, parsed, schemaName, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<EquipmentSchemaDto>.FromResult(result))
            : BadRequest(ApiResponse<EquipmentSchemaDto>.FromResult(result));
    }

    private static EquipmentExcelRangeRequest? ParseRange(string? rangeJson)
    {
        if (string.IsNullOrWhiteSpace(rangeJson)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<EquipmentExcelRangeRequest>(rangeJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    private async Task<bool> CanAsync(CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        return user != null && await _authorization.HasAsync(user, SecurityFunctionCodes.EquipmentImport, ct);
    }
}
