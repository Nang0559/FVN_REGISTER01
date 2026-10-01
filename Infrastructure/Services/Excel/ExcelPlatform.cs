using FVN_REGISTER.Application.Interfaces.Excel;
using FVN_REGISTER.Core.Excel;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace FVN_REGISTER.Infrastructure.Services.Excel;

public sealed class ExcelPlatform : IExcelPlatform
{
    public Task<ExcelWorkbookInspection> InspectAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        using var workbook = Open(content, fileName);
        return Task.FromResult(new ExcelWorkbookInspection(Enumerable.Range(0, workbook.NumberOfSheets).Select(workbook.GetSheetName).ToArray()));
    }

    public Task<ExcelPreviewResult> PreviewAsync(Stream content, string fileName, ExcelSchemaDefinition schema, CancellationToken ct = default)
        => ReadAsync(content, fileName, schema, ct);

    public async Task<ExcelImportResult> ImportAsync(Stream content, string fileName, ExcelSchemaDefinition schema, CancellationToken ct = default)
    {
        var result = await ReadAsync(content, fileName, schema, ct);
        return new ExcelImportResult(result.Rows, result.Errors);
    }

    public Task<Stream> ExportAsync(string fileName, ExcelSchemaDefinition schema, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IWorkbook workbook = fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ? new HSSFWorkbook() : new XSSFWorkbook();
        var sheet = workbook.CreateSheet(schema.EntityCode);
        var header = sheet.CreateRow(0);
        for (var c = 0; c < schema.Fields.Count; c++)
            header.CreateCell(c).SetCellValue(schema.Fields[c].HeaderName ?? schema.Fields[c].FieldKey);
        for (var r = 0; r < rows.Count; r++)
        {
            var target = sheet.CreateRow(r + 1);
            for (var c = 0; c < schema.Fields.Count; c++)
            {
                rows[r].TryGetValue(schema.Fields[c].FieldKey, out var value);
                target.CreateCell(c).SetCellValue(value?.ToString() ?? string.Empty);
            }
        }
        var output = new MemoryStream();
        workbook.Write(output, true);
        workbook.Close();
        output.Position = 0;
        return Task.FromResult<Stream>(output);
    }

    private static Task<ExcelPreviewResult> ReadAsync(Stream content, string fileName, ExcelSchemaDefinition schema, CancellationToken ct)
    {
        using var workbook = Open(content, fileName);
        if (schema.SheetIndex < 0 || schema.SheetIndex >= workbook.NumberOfSheets)
            throw new InvalidOperationException($"Sheet index {schema.SheetIndex} is outside the workbook.");
        var sheet = workbook.GetSheetAt(schema.SheetIndex);
        var rows = new List<ExcelRow>();
        var errors = new List<ExcelValidationError>();
        var end = schema.DataEndRowIndex ?? sheet.LastRowNum;
        for (var r = Math.Max(0, schema.DataStartRowIndex); r <= Math.Min(end, sheet.LastRowNum); r++)
        {
            ct.ThrowIfCancellationRequested();
            var source = sheet.GetRow(r);
            if (source is null) continue;
            var cells = new Dictionary<int, string?>();
            foreach (var field in schema.Fields)
            {
                var value = source.GetCell(field.SourceColumnIndex)?.ToString();
                cells[field.SourceColumnIndex] = value;
                if (field.Required && string.IsNullOrWhiteSpace(value))
                    errors.Add(new ExcelValidationError(r + 1, field.SourceColumnIndex, field.FieldKey, "REQUIRED", $"{field.FieldKey} is required.", value));
            }
            rows.Add(new ExcelRow(r + 1, cells));
        }
        return Task.FromResult(new ExcelPreviewResult(rows, errors));
    }

    private static IWorkbook Open(Stream content, string fileName)
    {
        if (fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)) return new HSSFWorkbook(content);
        if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return new XSSFWorkbook(content);
        throw new InvalidOperationException("Only .xls and .xlsx files are supported.");
    }
}
