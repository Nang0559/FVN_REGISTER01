namespace FVN_REGISTER.Core.Excel;

public enum ExcelSchemaStatus { Draft = 0, Active = 1, Retired = 2 }
public enum ExcelImportBatchStatus { Draft = 0, Uploaded = 10, Previewed = 20, Validated = 30, Staged = 40, Committed = 100, Failed = 900, Cancelled = 910, Deleted = 920 }
public enum ExcelImportRowStatus { Pending = 0, Valid = 10, Invalid = 20, Staged = 30, Imported = 100, Failed = 900 }
public enum ExcelFieldDataType { Text, Integer, Decimal, Date, DateTime, Boolean, Guid, Enum }
public enum ExcelSeverity { Info, Warning, Error }
public sealed record ExcelSchemaField(string FieldKey,string DataType,bool Required,int SourceColumnIndex,string? HeaderName=null,string? Format=null,string? ResourceKey=null,string? TargetProperty=null,int DisplayOrder=0,bool AllowEmpty=true);
public sealed record ExcelSchemaDefinition(string ModuleCode,string EntityCode,string SchemaKey,int Version,int SheetIndex,string SheetName,int HeaderRowIndex,int DataStartRowIndex,int? DataEndRowIndex,IReadOnlyList<int> SelectedColumnIndexes,IReadOnlyList<ExcelSchemaField> Fields,string Culture="vi-VN");
public sealed record ExcelWorkbookInspection(string FileName,string Extension,long Length,IReadOnlyList<ExcelSheetInspection> Sheets);
public sealed record ExcelSheetInspection(int Index,string Name,int FirstRowIndex,int LastRowIndex,int FirstColumnIndex,int LastColumnIndex,IReadOnlyList<ExcelHeaderCandidate> HeaderCandidates);
public sealed record ExcelHeaderCandidate(int RowIndex,IReadOnlyList<string?> Values,double Score);
public sealed record ExcelCell(int RowIndex,int ColumnIndex,string? Value,string? DisplayValue=null);
public sealed record ExcelRow(int RowIndex,IReadOnlyDictionary<int,string?> Cells);
public sealed record ExcelValidationError(int RowNumber,int? ColumnIndex,string? FieldKey,string Code,string Message,string? RawValue=null,ExcelSeverity Severity=ExcelSeverity.Error);
public sealed record ExcelPreviewResult(IReadOnlyList<ExcelRow> Rows,IReadOnlyList<ExcelValidationError> Errors,int TotalRows,int ValidRows,int InvalidRows);
public sealed record ExcelImportResult(IReadOnlyList<ExcelRow> Rows,IReadOnlyList<ExcelValidationError> Errors,int TotalRows,int ValidRows,int InvalidRows,Guid? BatchId=null);
public sealed record ExcelImportRequest(string ModuleCode,string EntityCode,int SchemaId,string FileName,Stream Content,bool Commit=false);
public sealed record ExcelSchemaCreateRequest(string ModuleCode,string EntityCode,string SchemaKey,string SchemaName,int SheetIndex,string SheetName,int HeaderRowIndex,int DataStartRowIndex,int? DataEndRowIndex,IReadOnlyList<int> SelectedColumnIndexes,IReadOnlyList<ExcelSchemaField> Fields,string Culture="vi-VN");
public sealed record ExcelSchemaSummary(int Id,string ModuleCode,string EntityCode,string SchemaKey,string SchemaName,int Version,ExcelSchemaStatus Status,string? SourceFileName,DateTime CreatedAt,DateTime? UpdatedAt);
public sealed record ExcelImportBatchSummary(Guid Id,string ModuleCode,string EntityCode,int SchemaId,string FileName,ExcelImportBatchStatus Status,int TotalRows,int ValidRows,int InvalidRows,int ImportedRows,DateTime CreatedAt,DateTime? CompletedAt);
