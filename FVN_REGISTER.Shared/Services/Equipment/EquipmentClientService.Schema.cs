using FVN_REGISTER.Application.Logging;
using FVN_REGISTER.Contract.Dtos.EquipmentImport;
using FVN_REGISTER.Contract.Responses;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace FVN_REGISTER.Shared.Services.Equipment;

public sealed partial class EquipmentClientService
{
    public Task<ApiResponse<EquipmentSchemaDto>> CreateSchemaVersionAsync(
        int schemaId,
        CancellationToken ct = default)
        => Post<EquipmentSchemaDto>(
            $"api/equipment/schemas/{schemaId}/version",
            new { },
            "create schema version",
            ct);

    public async Task<ApiResponse<EquipmentExcelWorkbookDto>> InspectExcelAsync(
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
            var query = $"deptCode={Uri.EscapeDataString(deptCode.Trim().ToUpperInvariant())}";
            return await _http.PostMultipartAsync<EquipmentExcelWorkbookDto>(
                $"api/equipment/schemas/inspect-excel?{query}", content, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogErrorIf(true, ex, "[EQUIPMENT_CLIENT] Inspect Excel failed. DeptCode={DeptCode}, FileName={FileName}", deptCode, file.Name);
            return ApiResponse<EquipmentExcelWorkbookDto>.Fail("Không thể đọc danh sách sheet Excel.");
        }
    }

    public async Task<ApiResponse<EquipmentSchemaFromExcelDto>> PreviewSchemaFromExcelAsync(
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
                deptCode.Trim().ToUpperInvariant());

            return await _http.PostMultipartAsync<EquipmentSchemaFromExcelDto>(
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

            return ApiResponse<EquipmentSchemaFromExcelDto>.Fail(
                "Không thể đọc cấu trúc Excel để tạo mẫu dữ liệu.");
        }
    }

    public async Task<ApiResponse<EquipmentSchemaDto>> CreateSchemaFromExcelAsync(
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
                $"deptCode={Uri.EscapeDataString(deptCode.Trim().ToUpperInvariant())}" +
                $"&sheetIndex={sheetIndex}";

            if (!string.IsNullOrWhiteSpace(schemaName))
            {
                query += $"&schemaName={Uri.EscapeDataString(schemaName.Trim())}";
            }

            return await _http.PostMultipartAsync<EquipmentSchemaDto>(
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

            return ApiResponse<EquipmentSchemaDto>.Fail(
                "Không thể tạo mẫu dữ liệu từ Excel.");
        }
    }

    public async Task<ApiResponse<EquipmentImportBatchDto>> StageImportAsync(
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
                $"deptCode={Uri.EscapeDataString(deptCode.Trim().ToUpperInvariant())}" +
                $"&assignToEmployee={assignToEmployee}" +
                $"&sheetIndex={sheetIndex}";

            if (schemaId.HasValue)
            {
                query += $"&schemaId={schemaId.Value}";
            }

            return await _http.PostMultipartAsync<EquipmentImportBatchDto>(
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

            return ApiResponse<EquipmentImportBatchDto>.Fail(
                "Không thể staging file Excel theo mẫu dữ liệu đã chọn.");
        }
    }
}
