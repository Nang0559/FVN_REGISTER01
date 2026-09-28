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
        return Ok(await _service.SaveSchemaAsync(request, ct));
    }

    [HttpPost("{schemaId:int}/clone")]
    public async Task<ActionResult<EquipmentSchemaDto>> Clone(int schemaId, [FromBody] EquipmentSchemaCloneRequest request, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        return Ok(await _service.CloneSchemaAsync(schemaId, request, ct));
    }

    [HttpPost("{schemaId:int}/version")]
    public async Task<ActionResult<EquipmentSchemaDto>> CreateVersion(int schemaId, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        return Ok(await _service.CreateVersionAsync(schemaId, ct));
    }

    [HttpPut("fields")]
    public async Task<ActionResult<EquipmentFieldDefinitionDto>> SaveField([FromBody] SaveEquipmentFieldDefinitionRequest request, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        return Ok(await _service.SaveFieldDefinitionAsync(request, ct));
    }

    [HttpPost("preview-excel")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<EquipmentSchemaFromExcelDto>> PreviewExcel([FromForm] IFormFile file, [FromQuery] string deptCode, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest("File Excel rỗng.");
        await using var stream = file.OpenReadStream();
        return Ok(await _service.PreviewSchemaFromExcelAsync(deptCode, file.FileName, stream, ct));
    }

    [HttpPost("from-excel")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<EquipmentSchemaDto>> FromExcel([FromForm] IFormFile file, [FromQuery] string deptCode, [FromQuery] string? schemaName, CancellationToken ct)
    {
        if (!await CanAsync(ct)) return Forbid();
        if (file == null || file.Length == 0) return BadRequest("File Excel rỗng.");
        await using var stream = file.OpenReadStream();
        return Ok(await _service.CreateSchemaFromExcelAsync(deptCode, file.FileName, stream, schemaName, ct));
    }

    private async Task<bool> CanAsync(CancellationToken ct)
    {
        var user = _currentUser.GetCurrentUser();
        return user != null && await _authorization.HasAsync(user, SecurityFunctionCodes.EquipmentImport, ct);
    }
}
