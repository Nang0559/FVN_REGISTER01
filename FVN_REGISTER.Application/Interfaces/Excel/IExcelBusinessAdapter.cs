using FVN_REGISTER.Core.Excel;

namespace FVN_REGISTER.Application.Interfaces.Excel;

/// <summary>
/// Module-specific business contract. Workbook parsing, schema lifecycle,
/// staging and error persistence remain in the shared Excel platform.
/// </summary>
public interface IExcelBusinessAdapter
{
    string ModuleCode { get; }
    string EntityCode { get; }

    /// <summary>Validate and normalize one already-parsed Excel row.</summary>
    Task<ExcelAdapterRowResult> NormalizeAsync(
        ExcelSchemaDefinition schema,
        ExcelRow row,
        CancellationToken cancellationToken = default);

    /// <summary>Commit normalized rows using the module's existing business rules.</summary>
    Task<ExcelAdapterCommitResult> CommitAsync(
        ExcelSchemaDefinition schema,
        IReadOnlyList<ExcelAdapterRowResult> rows,
        CancellationToken cancellationToken = default);
}

public sealed record ExcelAdapterRowResult(
    int RowNumber,
    bool IsValid,
    IReadOnlyDictionary<string, object?> Values,
    IReadOnlyList<ExcelValidationError> Errors);

public sealed record ExcelAdapterCommitResult(
    int ImportedRows,
    int FailedRows,
    IReadOnlyList<ExcelValidationError> Errors);
