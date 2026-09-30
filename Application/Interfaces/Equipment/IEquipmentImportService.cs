using FVN_REGISTER.Contract.Dtos.Equipment;
using FVN_REGISTER.Contract.Dtos.EquipmentImport;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.Equipment;

public interface IEquipmentImportService
{
    Task<List<EquipmentFieldDefinitionDto>> GetFieldDefinitionsAsync(string deptCode, CancellationToken ct = default);
    Task<EquipmentFieldDefinitionDto> SaveFieldDefinitionAsync(SaveEquipmentFieldDefinitionRequest request, CancellationToken ct = default);

    Task<List<EquipmentSchemaSummaryDto>> GetSchemasAsync(string? deptCode, CancellationToken ct = default);
    Task<EquipmentSchemaDto?> GetSchemaAsync(int schemaId, CancellationToken ct = default);
    Task<EquipmentSchemaDto> SaveSchemaAsync(EquipmentSchemaUpsertRequest request, CancellationToken ct = default);
    Task<EquipmentSchemaDto> CloneSchemaAsync(int schemaId, EquipmentSchemaCloneRequest request, CancellationToken ct = default);
    Task<EquipmentSchemaDto> CreateVersionAsync(int schemaId, CancellationToken ct = default);
    Task<ServiceResult<EquipmentExcelWorkbookDto>> InspectExcelAsync(string deptCode, string fileName, Stream content, CancellationToken ct = default);
    Task<ServiceResult<EquipmentSchemaFromExcelDto>> PreviewSchemaFromExcelSheetAsync(string deptCode, string fileName, Stream content, int sheetIndex, CancellationToken ct = default);
    Task<ServiceResult<EquipmentSchemaDto>> CreateSchemaFromExcelSheetAsync(string deptCode, string fileName, Stream content, int sheetIndex, string? schemaName, CancellationToken ct = default);
    Task<ServiceResult<EquipmentImportBatchDto>> StageExcelSheetAsync(string deptCode, int? schemaId, string fileName, Stream content, int sheetIndex, bool assignToEmployee = false, CancellationToken ct = default);

    /// <summary>Trả về lưới ô thô (rows x columns) của một sheet để người dùng preview và tự chọn dòng tiêu đề/cột/vùng dữ liệu.</summary>
    Task<ServiceResult<EquipmentExcelGridDto>> GetExcelGridAsync(string deptCode, string fileName, Stream content, int sheetIndex, int maxRows = 200, CancellationToken ct = default);
    /// <summary>Suy luận schema đúng theo vùng (dòng tiêu đề + cột + vùng dữ liệu) người dùng đã chọn trên preview.</summary>
    Task<ServiceResult<EquipmentSchemaFromExcelDto>> PreviewSchemaFromExcelRangeAsync(string deptCode, string fileName, Stream content, EquipmentExcelRangeRequest range, CancellationToken ct = default);
    /// <summary>Tạo mẫu dữ liệu (Draft) đúng theo vùng người dùng đã chọn trên preview.</summary>
    Task<ServiceResult<EquipmentSchemaDto>> CreateSchemaFromExcelRangeAsync(string deptCode, string fileName, Stream content, EquipmentExcelRangeRequest range, string? schemaName, CancellationToken ct = default);

    Task<EquipmentSchemaFromExcelDto> PreviewSchemaFromExcelAsync(string deptCode, string fileName, Stream content, CancellationToken ct = default);
    Task<EquipmentSchemaDto> CreateSchemaFromExcelAsync(string deptCode, string fileName, Stream content, string? schemaName, CancellationToken ct = default);

    Task<EquipmentImportBatchDto> StageExcelAsync(string deptCode, int? schemaId, string fileName, Stream content, bool assignToEmployee = false, CancellationToken ct = default);
    Task<EquipmentImportBatchDto?> GetBatchAsync(int batchId, CancellationToken ct = default);
    Task<EquipmentImportCommitResultDto> CommitAsync(int batchId, CancellationToken ct = default);
}
