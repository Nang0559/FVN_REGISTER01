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
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<ExcelWorkbookInspection>> Inspect(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn file Excel không rỗng." });

        var fileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(fileName);
        if (!new[] { ".xls", ".xlsx", ".xlsm" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "Định dạng file không được hỗ trợ. Vui lòng chọn file .xls, .xlsx hoặc .xlsm." });

        try
        {
            await using var stream = file.OpenReadStream();
            var inspection = await _excel.InspectAsync(stream, fileName, ct);
            return Ok(inspection);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (IOException)
        {
            return BadRequest(new { message = "Không thể đọc file Excel. File có thể bị hỏng hoặc đang được tạo không đúng định dạng." });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "Nội dung file không đúng định dạng Excel hoặc file bị hỏng." });
        }
        catch (Exception ex) when (ex.GetType().Namespace?.StartsWith("NPOI", StringComparison.Ordinal) == true)
        {
            return BadRequest(new { message = "Không đọc được file Excel. Vui lòng mở và lưu lại file bằng Excel rồi thử lại." });
        }
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

    [HttpPost("schemas/{id:int}/version")]
    public async Task<ActionResult<ExcelSchemaSummary>> CreateVersion(int id, CancellationToken ct)
    { try { return Ok(await _excel.CreateSchemaVersionAsync(id, ct)); } catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); } }

    [HttpPost("schemas/{id:int}/retire")]
    public async Task<ActionResult<ExcelSchemaSummary>> Retire(int id, CancellationToken ct)
    { try { await _excel.RetireSchemaAsync(id, ct); return Ok(await _excel.GetSchemaAsync(id, ct)); } catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); } }

    [HttpDelete("schemas/{id:int}")]
    public async Task<IActionResult> DeleteDraft(int id,CancellationToken ct)
    { try{await _excel.DeleteDraftSchemaAsync(id,ct);return NoContent();}catch(InvalidOperationException ex){return Conflict(ex.Message);} }
}
