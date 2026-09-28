using System.Net.Http.Headers;
using FVN_REGISTER.Contract.Dtos.EquipmentImport;
using FVN_REGISTER.Contract.Responses;
using Microsoft.AspNetCore.Components.Forms;

namespace FVN_REGISTER.Shared.Services.Equipment;

public sealed partial class EquipmentClientService
{
    public Task<ApiResponse<EquipmentSchemaDto>> CreateSchemaVersionAsync(int schemaId, CancellationToken ct = default)
        => Post<EquipmentSchemaDto>($"api/equipment/schemas/{schemaId}/version", new { }, "create schema version", ct);

    public async Task<ApiResponse<EquipmentSchemaFromExcelDto>> PreviewSchemaFromExcelAsync(string deptCode, IBrowserFile file, CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream(25 * 1024 * 1024, ct);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", file.Name);
            return await _http.PostMultipartAsync<EquipmentSchemaFromExcelDto>($"api/equipment/schemas/preview-excel?deptCode={Uri.EscapeDataString(deptCode.Trim().ToUpperInvariant())}", content, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EQUIPMENT_CLIENT] preview schema from Excel");
            return ApiResponse<EquipmentSchemaFromExcelDto>.Fail("Không thể đọc cấu trúc Excel để tạo mẫu dữ liệu.");
        }
    }

    public async Task<ApiResponse<EquipmentSchemaDto>> CreateSchemaFromExcelAsync(string deptCode, IBrowserFile file, string? schemaName = null, CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream(25 * 1024 * 1024, ct);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", file.Name);
            var query = $"deptCode={Uri.EscapeDataString(deptCode.Trim().ToUpperInvariant())}";
            if (!string.IsNullOrWhiteSpace(schemaName)) query += $"&schemaName={Uri.EscapeDataString(schemaName.Trim())}";
            return await _http.PostMultipartAsync<EquipmentSchemaDto>($"api/equipment/schemas/from-excel?{query}", content, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EQUIPMENT_CLIENT] create schema from Excel");
            return ApiResponse<EquipmentSchemaDto>.Fail("Không thể tạo mẫu dữ liệu từ Excel.");
        }
    }

    public async Task<ApiResponse<EquipmentImportBatchDto>> StageImportAsync(string deptCode, int? schemaId, IBrowserFile file, bool assignToEmployee = false, CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream(25 * 1024 * 1024, ct);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", file.Name);
            var query = $"deptCode={Uri.EscapeDataString(deptCode.Trim().ToUpperInvariant())}&assignToEmployee={assignToEmployee}";
            if (schemaId.HasValue) query += $"&schemaId={schemaId.Value}";
            return await _http.PostMultipartAsync<EquipmentImportBatchDto>($"api/equipment/import?{query}", content, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EQUIPMENT_CLIENT] stage import with schema");
            return ApiResponse<EquipmentImportBatchDto>.Fail("Không thể staging file Excel theo mẫu dữ liệu đã chọn.");
        }
    }
}
