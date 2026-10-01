using FVN_REGISTER.Core.Excel;
namespace FVN_REGISTER.Application.Interfaces.Excel;
public interface IExcelPlatform
{
 Task<ExcelWorkbookInspection> InspectAsync(Stream content,string fileName,CancellationToken ct=default);
 Task<ExcelPreviewResult> PreviewAsync(Stream content,string fileName,ExcelSchemaDefinition schema,CancellationToken ct=default);
 Task<ExcelImportResult> ImportAsync(Stream content,string fileName,ExcelSchemaDefinition schema,CancellationToken ct=default);
 Task<Stream> ExportAsync(string fileName,ExcelSchemaDefinition schema,IReadOnlyList<IReadOnlyDictionary<string,object?>> rows,CancellationToken ct=default);
 Task<IReadOnlyList<ExcelSchemaSummary>> GetSchemasAsync(string moduleCode,string entityCode,bool includeRetired=false,CancellationToken ct=default);
 Task<ExcelSchemaSummary?> GetSchemaAsync(int schemaId,CancellationToken ct=default);
 Task<ExcelSchemaDefinition?> GetSchemaDefinitionAsync(int schemaId,CancellationToken ct=default);
 Task<ExcelSchemaSummary> CreateDraftSchemaAsync(ExcelSchemaCreateRequest request,string? sourceFileName,CancellationToken ct=default);
 Task<ExcelSchemaSummary> ActivateSchemaAsync(int schemaId,CancellationToken ct=default);
 Task DeleteDraftSchemaAsync(int schemaId,CancellationToken ct=default);
}
