using FVN_REGISTER.Contract.Dtos.EquipmentImport;

namespace FVN_REGISTER.Application.Interfaces.Equipment;

/// <summary>Source compatibility helpers for callers migrating to the canonical Excel contracts.</summary>
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
        var workbook = await service.InspectExcelAsync(departmentCode, fileName, content, ct);
        if (!workbook.IsSuccess || workbook.Data is null)
            throw new InvalidOperationException(workbook.Message ?? "Không thể đọc file Excel.");

        var sheetIndex = workbook.Data.Sheets.FirstOrDefault()?.Index ?? 0;
        if (workbook.Data.Sheets.Count == 0)
            throw new InvalidOperationException("File Excel không có sheet.");

        await using var replay = new MemoryStream();
        if (content.CanSeek) content.Position = 0;
        await content.CopyToAsync(replay, ct);
        replay.Position = 0;
        var result = await service.StageExcelSheetAsync(departmentCode, schemaId, fileName, replay, sheetIndex, assignToEmployee, ct);
        if (!result.IsSuccess || result.Data is null)
            throw new InvalidOperationException(result.Message ?? "Không thể staging file Excel.");
        return result.Data;
    }
}
