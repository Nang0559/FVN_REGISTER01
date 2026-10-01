namespace FVN_REGISTER.Core.Excel;

public enum ExcelSchemaStatus { Draft = 0, Active = 1, Retired = 2 }
public enum ExcelImportBatchStatus { Draft = 0, Uploaded = 10, Previewed = 20, Validated = 30, Staged = 40, Committed = 100, Failed = 900, Cancelled = 910 }
public sealed record ExcelSchemaField(string FieldKey, string DataType, bool Required, int SourceColumnIndex, string? HeaderName = null);
public sealed record ExcelSchemaDefinition(string ModuleCode, string EntityCode, int SheetIndex, int HeaderRowIndex, int DataStartRowIndex, int? DataEndRowIndex, IReadOnlyList<ExcelSchemaField> Fields);
public sealed record ExcelRow(int RowIndex, IReadOnlyDictionary<int, string?> Cells);
public sealed record ExcelValidationError(int RowNumber, int? ColumnIndex, string? FieldKey, string Code, string Message, string? RawValue = null);
