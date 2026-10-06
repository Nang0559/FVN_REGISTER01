using FVN_REGISTER.Contract.Dtos.EquipmentImport;

namespace FVN_REGISTER.Application.Interfaces.Equipment;

/// <summary>Source compatibility helper while callers migrate to the canonical Excel contracts.</summary>
public static class EquipmentImportServiceCompatibilityExtensions
{
    public static async Task<ExcelImportBatchDto> StageExcelAsync(
        this IEquipmentImportService service,
        string departmentCode,
        int? schemaId,
        string fileName,
        Stream content,
        bool assignToEmployee = false,
        CancellationToken ct = default)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        if (!int.TryParse(departmentCode, out var parsedDepartmentCode))
            throw new ArgumentException("Mã phòng ban phải là số.", nameof(departmentCode));

        var workbook = await service.InspectExcelAsync(parsedDepartmentCode, fileName, buffer, ct);
        if (!workbook.IsSuccess || workbook.Data is null)
            throw new InvalidOperationException(workbook.Message ?? "Không thể đọc file Excel.");
        if (workbook.Data.Sheets.Count == 0)
            throw new InvalidOperationException("File Excel không có sheet.");

        var sheetIndex = workbook.Data.Sheets[0].Index;
        buffer.Position = 0;
        var result = await service.StageExcelSheetAsync(parsedDepartmentCode, schemaId, fileName, buffer, sheetIndex, assignToEmployee, ct);
        if (!result.IsSuccess || result.Data is null)
            throw new InvalidOperationException(result.Message ?? "Không thể staging file Excel.");
        return result.Data;
    }
}
