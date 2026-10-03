using FVN_REGISTER.Core.Excel;

namespace FVN_REGISTER.Application.Interfaces.Excel;

/// <summary>
/// Module-specific business rules sit behind this contract. Workbook parsing,
/// schema lifecycle, staging and Excel validation mechanics remain in IExcelPlatform.
/// </summary>
public interface IExcelBusinessAdapter
{
    string ModuleCode { get; }
    string EntityCode { get; }
    Task<IReadOnlyList<ExcelValidationError>> ValidateBusinessAsync(
        ExcelSchemaDefinition schema,
        IReadOnlyList<ExcelRow> rows,
        CancellationToken ct = default);
    Task<int> CommitAsync(
        ExcelSchemaDefinition schema,
        IReadOnlyList<ExcelRow> rows,
        CancellationToken ct = default);
}
