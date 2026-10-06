using FVN_REGISTER.Application.Logging;
using FVN_REGISTER.Contract.Dtos.EquipmentImport;
using FVN_REGISTER.Contract.Responses;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace FVN_REGISTER.Shared.Services.Equipment;

public sealed partial class EquipmentClientService
{
    public Task<ApiResponse<ExcelSchemaDto>> CreateSchemaVersionAsync(
        int schemaId,
        CancellationToken ct = default)
        => Post<ExcelSchemaDto>(
            $"api/equipment/schemas/{schemaId}/version",
            new { },
            "create schema version",
            ct);

    public async Task<ApiResponse<ExcelWorkbookDto>> InspectExcelAsync(
        string deptCode,
        IBrowserFile file,
        CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream(25 * 1024 * 1024, ct);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", file.Name);
            var query = $"deptCode={Uri.EscapeDataString(deptCode.Trim())}";
            return await _http.PostMultipartAsync<ExcelWorkbookDto>(
                $"api/equipment/schemas/inspect-excel?{query}", content, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogErrorIf(true, ex, "[EQUIPMENT_CLIENT] Inspect Excel failed. DeptCode={DeptCode}, FileName={FileName}", deptCode, file.Name);
            return ApiResponse<ExcelWorkbookDto>.Fail("Không thể đọc danh sách sheet Excel.");
        }
    }

    public Task<ApiResponse<ExcelSchemaFromExcelDto>> PreviewSchemaFromExcelAsync(string deptCode, IBrowserFile file, CancellationToken ct)
        => PreviewSchemaFromExcelAsync(deptCode, file, 0, ct);

    public async Task<ApiResponse<ExcelSchemaFromExcelDto>> PreviewSchemaFromExcelAsync(
        string deptCode,
        IBrowserFile file,
        int sheetIndex = 0,
        CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream(25 * 1024 * 1024, ct);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", file.Name);

            var normalizedDeptCode = Uri.EscapeDataString(
                deptCode.Trim());

            return await _http.PostMultipartAsync<ExcelSchemaFromExcelDto>(
                $"api/equipment/schemas/preview-excel?deptCode={normalizedDeptCode}&sheetIndex={sheetIndex}",
                content,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorIf(
                true,
                ex,
                "[EQUIPMENT_CLIENT] Preview schema from Excel failed. DeptCode={DeptCode}, FileName={FileName}",
                deptCode,
                file.Name);

            return ApiResponse<ExcelSchemaFromExcelDto>.Fail(
                "Không thể đọc cấu trúc Excel để tạo mẫu dữ liệu.");
        }
    }

    public Task<ApiResponse<ExcelSchemaDto>> CreateSchemaFromExcelAsync(string deptCode, IBrowserFile file, string? schemaName, CancellationToken ct)
        => CreateSchemaFromExcelAsync(deptCode, file, schemaName, 0, ct);

    public async Task<ApiResponse<ExcelSchemaDto>> CreateSchemaFromExcelAsync(
        string deptCode,
        IBrowserFile file,
        string? schemaName = null,
        int sheetIndex = 0,
        CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream(25 * 1024 * 1024, ct);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", file.Name);

            var query =
                $"deptCode={Uri.EscapeDataString(deptCode.Trim())}" +
                $"&sheetIndex={sheetIndex}";

            if (!string.IsNullOrWhiteSpace(schemaName))
            {
                query += $"&schemaName={Uri.EscapeDataString(schemaName.Trim())}";
            }

            return await _http.PostMultipartAsync<ExcelSchemaDto>(
                $"api/equipment/schemas/from-excel?{query}",
                content,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorIf(
                true,
                ex,
                "[EQUIPMENT_CLIENT] Create schema from Excel failed. DeptCode={DeptCode}, FileName={FileName}, SchemaName={SchemaName}",
                deptCode,
                file.Name,
                schemaName);

            return ApiResponse<ExcelSchemaDto>.Fail(
                "Không thể tạo mẫu dữ liệu từ Excel.");
        }
    }

    public async Task<ApiResponse<ExcelGridDto>> GetExcelGridAsync(
        string deptCode,
        IBrowserFile file,
        int sheetIndex,
        int maxRows = 200,
        CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream(25 * 1024 * 1024, ct);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", file.Name);

            var query =
                $"deptCode={Uri.EscapeDataString(deptCode.Trim())}" +
                $"&sheetIndex={sheetIndex}" +
                $"&maxRows={maxRows}";

            return await _http.PostMultipartAsync<ExcelGridDto>(
                $"api/equipment/schemas/excel-grid?{query}",
                content,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorIf(
                true,
                ex,
                "[EQUIPMENT_CLIENT] Get Excel grid failed. DeptCode={DeptCode}, FileName={FileName}, SheetIndex={SheetIndex}",
                deptCode,
                file.Name,
                sheetIndex);

            return ApiResponse<ExcelGridDto>.Fail(
                "Không thể tải preview dữ liệu Excel.");
        }
    }

    public async Task<ApiResponse<ExcelSchemaFromExcelDto>> PreviewSchemaFromExcelRangeAsync(
        string deptCode,
        IBrowserFile file,
        ExcelRangeRequest range,
        CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream(25 * 1024 * 1024, ct);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", file.Name);
            content.Add(new StringContent(System.Text.Json.JsonSerializer.Serialize(range)), "range");

            var query = $"deptCode={Uri.EscapeDataString(deptCode.Trim())}";

            return await _http.PostMultipartAsync<ExcelSchemaFromExcelDto>(
                $"api/equipment/schemas/preview-excel-range?{query}",
                content,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorIf(
                true,
                ex,
                "[EQUIPMENT_CLIENT] Preview schema from Excel range failed. DeptCode={DeptCode}, FileName={FileName}",
                deptCode,
                file.Name);

            return ApiResponse<ExcelSchemaFromExcelDto>.Fail(
                "Không thể đọc cấu trúc Excel theo vùng đã chọn.");
        }
    }

    public async Task<ApiResponse<ExcelSchemaDto>> CreateSchemaFromExcelRangeAsync(
        string deptCode,
        IBrowserFile file,
        ExcelRangeRequest range,
        string? schemaName = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream(25 * 1024 * 1024, ct);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", file.Name);
            content.Add(new StringContent(System.Text.Json.JsonSerializer.Serialize(range)), "range");

            var query = $"deptCode={Uri.EscapeDataString(deptCode.Trim())}";
            if (!string.IsNullOrWhiteSpace(schemaName))
            {
                query += $"&schemaName={Uri.EscapeDataString(schemaName.Trim())}";
            }

            return await _http.PostMultipartAsync<ExcelSchemaDto>(
                $"api/equipment/schemas/from-excel-range?{query}",
                content,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorIf(
                true,
                ex,
                "[EQUIPMENT_CLIENT] Create schema from Excel range failed. DeptCode={DeptCode}, FileName={FileName}, SchemaName={SchemaName}",
                deptCode,
                file.Name,
                schemaName);

            return ApiResponse<ExcelSchemaDto>.Fail(
                "Không thể tạo mẫu dữ liệu theo vùng Excel đã chọn.");
        }
    }

    public Task<ApiResponse<ExcelImportBatchDto>> StageImportAsync(string deptCode, int? schemaId, IBrowserFile file, bool assignToEmployee, CancellationToken ct)
        => StageImportAsync(deptCode, schemaId, file, assignToEmployee, 0, ct);

    public async Task<ApiResponse<ExcelImportBatchDto>> StageImportAsync(
        string deptCode,
        int? schemaId,
        IBrowserFile file,
        bool assignToEmployee = false,
        int sheetIndex = 0,
        CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream(25 * 1024 * 1024, ct);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", file.Name);

            var query =
                $"deptCode={Uri.EscapeDataString(deptCode.Trim())}" +
                $"&assignToEmployee={assignToEmployee}" +
                $"&sheetIndex={sheetIndex}";

            if (schemaId.HasValue)
            {
                query += $"&schemaId={schemaId.Value}";
            }

            return await _http.PostMultipartAsync<ExcelImportBatchDto>(
                $"api/equipment/import?{query}",
                content,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogErrorIf(
                true,
                ex,
                "[EQUIPMENT_CLIENT] Stage import failed. DeptCode={DeptCode}, SchemaId={SchemaId}, FileName={FileName}, AssignToEmployee={AssignToEmployee}",
                deptCode,
                schemaId,
                file.Name,
                assignToEmployee);

            return ApiResponse<ExcelImportBatchDto>.Fail(
                "Không thể staging file Excel theo mẫu dữ liệu đã chọn.");
        }
    }
}
