using FVN_REGISTER.Core.Excel;

namespace FVN_REGISTER.Application.Interfaces.Excel;

public interface IExcelPlatform
{
    Task<ExcelWorkbookInspection> InspectAsync(Stream content, string fileName, CancellationToken ct = default);
    Task<ExcelPreviewResult> PreviewAsync(Stream content, string fileName, ExcelSchemaDefinition schema, CancellationToken ct = default);
    Task<ExcelImportResult> ImportAsync(Stream content, string fileName, ExcelSchemaDefinition schema, CancellationToken ct = default);
    Task<Stream> ExportAsync(string fileName, ExcelSchemaDefinition schema, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, CancellationToken ct = default);
}
public sealed record ExcelWorkbookInspection(IReadOnlyList<string> Sheets);
public sealed record ExcelPreviewResult(IReadOnlyList<ExcelRow> Rows, IReadOnlyList<ExcelValidationError> Errors);
public sealed record ExcelImportResult(IReadOnlyList<ExcelRow> Rows, IReadOnlyList<ExcelValidationError> Errors);
