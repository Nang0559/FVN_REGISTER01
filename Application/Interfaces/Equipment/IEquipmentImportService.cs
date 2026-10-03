using FVN_REGISTER.Contract.Dtos.EquipmentImport;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.Equipment;

/// <summary>
/// Equipment-specific business adapter over the canonical Shared Excel Platform.
/// Excel schema/version/import metadata is owned by F03Excel*; Equipment owns only domain mapping and validation.
/// </summary>
public interface IEquipmentImportService
{
    Task<List<ExcelSchemaSummaryDto>> GetSchemasAsync(string? departmentCode, CancellationToken ct = default);
    Task<ExcelSchemaDto?> GetSchemaAsync(int schemaId, CancellationToken ct = default);
    Task<ServiceResult<ExcelSchemaDto>> SaveSchemaAsync(ExcelSchemaUpsertRequest request, CancellationToken ct = default);
    Task<ServiceResult<ExcelSchemaDto>> CloneSchemaAsync(int schemaId, ExcelSchemaCloneRequest request, CancellationToken ct = default);
    Task<ServiceResult<ExcelSchemaDto>> CreateVersionAsync(int schemaId, CancellationToken ct = default);
    Task<ServiceResult<ExcelSchemaFieldDto>> SaveSchemaFieldAsync(SaveExcelSchemaFieldRequest request, CancellationToken ct = default);

    Task<ServiceResult<ExcelWorkbookDto>> InspectExcelAsync(string departmentCode, string fileName, Stream content, CancellationToken ct = default);
    Task<ServiceResult<ExcelSchemaFromExcelDto>> PreviewSchemaFromExcelSheetAsync(string departmentCode, string fileName, Stream content, int sheetIndex, CancellationToken ct = default);
    Task<ServiceResult<ExcelSchemaDto>> CreateSchemaFromExcelSheetAsync(string departmentCode, string fileName, Stream content, int sheetIndex, string? schemaName, CancellationToken ct = default);
    Task<ServiceResult<ExcelImportBatchDto>> StageExcelSheetAsync(string departmentCode, int? schemaId, string fileName, Stream content, int sheetIndex, bool assignToEmployee = false, CancellationToken ct = default);

    /// <summary>Returns raw sheet cells so the UI can choose header row, columns and data range.</summary>
    Task<ServiceResult<ExcelGridDto>> GetExcelGridAsync(string departmentCode, string fileName, Stream content, int sheetIndex, int maxRows = 200, CancellationToken ct = default);
    Task<ServiceResult<ExcelSchemaFromExcelDto>> PreviewSchemaFromExcelRangeAsync(string departmentCode, string fileName, Stream content, ExcelRangeRequest range, CancellationToken ct = default);
    Task<ServiceResult<ExcelSchemaDto>> CreateSchemaFromExcelRangeAsync(string departmentCode, string fileName, Stream content, ExcelRangeRequest range, string? schemaName, CancellationToken ct = default);

    Task<ExcelSchemaFromExcelDto> PreviewSchemaFromExcelAsync(string departmentCode, string fileName, Stream content, CancellationToken ct = default);
    Task<ExcelSchemaDto> CreateSchemaFromExcelAsync(string departmentCode, string fileName, Stream content, string? schemaName, CancellationToken ct = default);

    Task<ExcelImportBatchDto?> GetBatchAsync(long batchId, CancellationToken ct = default);
    Task<ExcelImportCommitResultDto> CommitAsync(long batchId, CancellationToken ct = default);
    Task<ServiceResult<bool>> DeleteDraftSchemaAsync(int schemaId, CancellationToken ct = default);
}
