using FVN_REGISTER.Core.Excel;
using FVN_REGISTER.Application.Interfaces.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FVN_REGISTER.API.Controllers;

[ApiController]
[Route("api/excel")]
[Authorize]
public sealed class ExcelController : ControllerBase
{
    private readonly IExcelPlatform _excel;
    public ExcelController(IExcelPlatform excel) => _excel = excel;

    [HttpPost("inspect")]
    public async Task<ActionResult<ExcelWorkbookInspection>> Inspect(IFormFile file,CancellationToken ct)
    {
        if(file is null||file.Length==0)return BadRequest("File Excel rỗng.");
        await using var s=file.OpenReadStream(); return Ok(await _excel.InspectAsync(s,file.FileName,ct));
    }

    [HttpGet("schemas")]
    public async Task<ActionResult<IReadOnlyList<ExcelSchemaSummary>>> Schemas([FromQuery] string moduleCode,[FromQuery] string entityCode,[FromQuery] bool includeRetired=false,CancellationToken ct=default)
        => Ok(await _excel.GetSchemasAsync(moduleCode,entityCode,includeRetired,ct));

    [HttpGet("schemas/{id:int}")]
    public async Task<ActionResult<ExcelSchemaSummary>> Schema(int id,CancellationToken ct)
    { var item=await _excel.GetSchemaAsync(id,ct); return item is null?NotFound():Ok(item); }

    [HttpPost("schemas/draft")]
    public async Task<ActionResult<ExcelSchemaSummary>> Draft([FromBody] ExcelSchemaCreateRequest request,[FromQuery] string? sourceFileName,CancellationToken ct)
    { var item=await _excel.CreateDraftSchemaAsync(request,sourceFileName,ct); return CreatedAtAction(nameof(Schema),new{id=item.Id},item); }

    [HttpPost("schemas/{id:int}/activate")]
    public async Task<ActionResult<ExcelSchemaSummary>> Activate(int id,CancellationToken ct)
    { try{return Ok(await _excel.ActivateSchemaAsync(id,ct));}catch(InvalidOperationException ex){return BadRequest(ex.Message);} }

    [HttpDelete("schemas/{id:int}")]
    public async Task<IActionResult> DeleteDraft(int id,CancellationToken ct)
    { try{await _excel.DeleteDraftSchemaAsync(id,ct);return NoContent();}catch(InvalidOperationException ex){return Conflict(ex.Message);} }
}
