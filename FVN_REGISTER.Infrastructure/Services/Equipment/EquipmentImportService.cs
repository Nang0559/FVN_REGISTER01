using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NPOI.SS.UserModel;
using FVN_REGISTER.Application.Interfaces.Equipment;
using FVN_REGISTER.Application.Interfaces.Auths;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Equipment;
using FVN_REGISTER.Contract.Dtos.EquipmentImport;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.Equipment;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Entities.Security;
using FVN_REGISTER.Core.Repositories;
using FVN_REGISTER.Core.Schema;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Equipment;

public sealed class EquipmentImportService : IEquipmentImportService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditService _audit;

    public EquipmentImportService(IUnitOfWork uow, ICurrentUserService currentUser, IAuthorizationService authorization, IAuditService audit)
    { _uow = uow; _currentUser = currentUser; _authorization = authorization; _audit = audit; }

    public async Task<List<EquipmentFieldDefinitionDto>> GetFieldDefinitionsAsync(string deptCode, CancellationToken ct = default)
    {
        var user = RequireUser(); await EnsureScopeAsync(user, deptCode, ct); var schema = await GetActiveSchemaEntityAsync(deptCode, ct); if (schema == null) return new();
        return await _uow.Repository<F03EquipmentFieldDefinition>().Query().AsNoTracking().Where(x => x.SchemaId == schema.Id && x.IsActive == true && x.IsActiveField).OrderBy(x => x.DisplayOrder).ThenBy(x => x.FieldLabel).Select(MapFieldExpression()).ToListAsync(ct);
    }

    public async Task<EquipmentFieldDefinitionDto> SaveFieldDefinitionAsync(SaveEquipmentFieldDefinitionRequest request, CancellationToken ct = default)
    {
        var user = RequireUser(); if (request.SchemaId is null || request.SchemaId <= 0) throw new ArgumentException("Mẫu dữ liệu là bắt buộc.");
        var schema = await _uow.Repository<F03EquipmentSchema>().Query().FirstOrDefaultAsync(x => x.Id == request.SchemaId && x.IsActive == true, ct) ?? throw new KeyNotFoundException("Không tìm thấy mẫu dữ liệu.");
        await EnsureScopeAsync(user, schema.DeptCode, ct); EnsureOwner(user, schema); EnsureDraft(schema); var dept = schema.DeptCode.Trim(); var key = NormalizeKey(request.FieldKey); if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Mã trường là bắt buộc."); ValidateDataType(request.DataType);
        var e = await _uow.Repository<F03EquipmentFieldDefinition>().Query().FirstOrDefaultAsync(x => x.SchemaId == schema.Id && x.FieldKey == key, ct);
        if (e == null) { e = new F03EquipmentFieldDefinition { SchemaId = schema.Id, DeptCode = dept, FieldKey = key, CreatedBy = user.UserId }; await _uow.Repository<F03EquipmentFieldDefinition>().AddAsync(e, ct); }
        e.FieldLabel = request.FieldLabel.Trim(); e.DataType = request.DataType.Trim(); e.IsRequired = request.IsRequired; e.IsImportable = request.IsImportable; e.IsSearchable = request.IsSearchable; e.IsActiveField = request.IsActiveField; e.DisplayOrder = request.DisplayOrder; e.MaxLength = request.MaxLength; e.DefaultValue = request.DefaultValue; e.OptionsJson = request.OptionsJson; e.IsActive = true; e.ModifiedBy = user.UserId; e.ModifiedAt = DateTime.Now;
        await _uow.SaveChangesAsync(ct); await _audit.LogAction("EQUIPMENT_SCHEMA_FIELD_SAVED", user.UserId, $"SchemaId={schema.Id}; FieldKey={e.FieldKey}; DeptCode={schema.DeptCode}", ct: ct); return MapField(e);
    }

    public async Task<List<EquipmentSchemaSummaryDto>> GetSchemasAsync(string? deptCode, CancellationToken ct = default)
    {
        var user = RequireUser(); var dept = string.IsNullOrWhiteSpace(deptCode) ? user.DeptCode : deptCode; if (string.IsNullOrWhiteSpace(dept)) throw new ArgumentException("Không xác định được bộ phận của người dùng."); await EnsureScopeAsync(user, dept, ct);
        var schemas = await _uow.Repository<F03EquipmentSchema>().Query().AsNoTracking().Where(x => x.DeptCode == dept.Trim() && x.IsActive == true).OrderBy(x => x.SchemaName).ThenByDescending(x => x.Version).ToListAsync(ct);
        var ownerIds = schemas.Select(x => x.CreatedBy).Where(x => x > 0).Distinct().ToList(); var owners = await _uow.Repository<F03User>().Query().AsNoTracking().Where(x => ownerIds.Contains(x.Id)).Select(x => new { x.Id, x.FullName, x.EmployeeCode }).ToDictionaryAsync(x => x.Id, x => string.IsNullOrWhiteSpace(x.FullName) ? x.EmployeeCode : x.FullName!, ct);
        var schemaIds = schemas.Select(x => x.Id).ToList(); var fieldCounts = await _uow.Repository<F03EquipmentFieldDefinition>().Query().AsNoTracking().Where(x => schemaIds.Contains(x.SchemaId) && x.IsActive == true && x.IsActiveField).GroupBy(x => x.SchemaId).Select(g => new { SchemaId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.SchemaId, x => x.Count, ct);
        return schemas.Select(x => MapSummary(x, user.UserId, owners.TryGetValue(x.CreatedBy, out var n) ? n : "Chưa xác định", fieldCounts.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<EquipmentSchemaDto?> GetSchemaAsync(int schemaId, CancellationToken ct = default)
    {
        var user = RequireUser(); var schema = await _uow.Repository<F03EquipmentSchema>().Query().Include(x => x.Fields).FirstOrDefaultAsync(x => x.Id == schemaId && x.IsActive == true, ct); if (schema == null) return null; await EnsureScopeAsync(user, schema.DeptCode, ct); return await MapSchemaAsync(schema, user.UserId, ct);
    }

    public async Task<EquipmentSchemaDto> SaveSchemaAsync(EquipmentSchemaUpsertRequest request, CancellationToken ct = default)
    {
        var user = RequireUser(); var dept = request.DeptCode.Trim().ToUpperInvariant(); await EnsureScopeAsync(user, dept, ct);
        if (request.Id is null or <= 0) { var schema = new F03EquipmentSchema { DeptCode = dept, SchemaName = request.SchemaName.Trim(), SchemaKind = NormalizeSchemaKind(request.SchemaKind), SchemaKey = Guid.NewGuid().ToString("N"), Version = 1, Status = NormalizeStatus(request.Status), IsActive = true, CreatedBy = user.UserId, CreatedFromExcel = false }; ValidateSchemaName(schema.SchemaName); if (schema.Status == "Active") throw new InvalidOperationException("Mẫu dữ liệu mới phải ở trạng thái Bản nháp cho đến khi có ít nhất một trường dữ liệu."); await _uow.Repository<F03EquipmentSchema>().AddAsync(schema, ct); await _uow.SaveChangesAsync(ct); await _audit.LogAction("EQUIPMENT_SCHEMA_CREATED", user.UserId, $"SchemaId={schema.Id}; DeptCode={dept}; Name={schema.SchemaName}", ct: ct); return await MapSchemaAsync(schema, user.UserId, ct); }
        var existing = await _uow.Repository<F03EquipmentSchema>().Query().Include(x => x.Fields).FirstOrDefaultAsync(x => x.Id == request.Id && x.IsActive == true, ct) ?? throw new KeyNotFoundException("Không tìm thấy mẫu dữ liệu."); await EnsureScopeAsync(user, existing.DeptCode, ct); EnsureOwner(user, existing); EnsureDraft(existing); ValidateSchemaName(request.SchemaName); existing.SchemaName = request.SchemaName.Trim(); existing.SchemaKind = NormalizeSchemaKind(request.SchemaKind); var status = NormalizeStatus(request.Status); if (status == "Active") { if (!existing.Fields.Any(x => x.IsActive == true && x.IsActiveField)) throw new InvalidOperationException("Không thể kích hoạt mẫu dữ liệu chưa có trường dữ liệu."); await DeactivateOtherActiveVersionsAsync(existing, ct); } existing.Status = status; existing.ModifiedBy = user.UserId; existing.ModifiedAt = DateTime.Now; foreach (var field in existing.Fields) field.IsActiveField = status == "Active" && field.IsActive == true; await _uow.SaveChangesAsync(ct); await _audit.LogAction("EQUIPMENT_SCHEMA_SAVED", user.UserId, $"SchemaId={existing.Id}; Status={existing.Status}", ct: ct); return await MapSchemaAsync(existing, user.UserId, ct);
    }

    public async Task<EquipmentSchemaDto> CloneSchemaAsync(int schemaId, EquipmentSchemaCloneRequest request, CancellationToken ct = default)
    {
        var user = RequireUser(); var source = await _uow.Repository<F03EquipmentSchema>().Query().Include(x => x.Fields).FirstOrDefaultAsync(x => x.Id == schemaId && x.IsActive == true, ct) ?? throw new KeyNotFoundException("Không tìm thấy mẫu dữ liệu nguồn."); await EnsureScopeAsync(user, source.DeptCode, ct); var name = string.IsNullOrWhiteSpace(request.SchemaName) ? $"{source.SchemaName} - Bản sao" : request.SchemaName.Trim(); ValidateSchemaName(name); var clone = new F03EquipmentSchema { DeptCode = source.DeptCode, SchemaName = name, SchemaKind = source.SchemaKind, SchemaKey = Guid.NewGuid().ToString("N"), Version = 1, Status = "Draft", IsActive = true, CreatedBy = user.UserId, SourceSchemaId = source.Id, SourceFileName = source.SourceFileName, CreatedFromExcel = source.CreatedFromExcel }; await _uow.Repository<F03EquipmentSchema>().AddAsync(clone, ct); await _uow.SaveChangesAsync(ct); foreach (var field in source.Fields.Where(x => x.IsActive == true)) await _uow.Repository<F03EquipmentFieldDefinition>().AddAsync(new F03EquipmentFieldDefinition { SchemaId = clone.Id, DeptCode = clone.DeptCode, FieldKey = field.FieldKey, FieldLabel = field.FieldLabel, DataType = field.DataType, IsRequired = field.IsRequired, IsImportable = field.IsImportable, IsSearchable = field.IsSearchable, IsActiveField = true, DisplayOrder = field.DisplayOrder, MaxLength = field.MaxLength, DefaultValue = field.DefaultValue, OptionsJson = field.OptionsJson, IsActive = true, CreatedBy = user.UserId }, ct); await _uow.SaveChangesAsync(ct); await _audit.LogAction("EQUIPMENT_SCHEMA_CLONED", user.UserId, $"SourceSchemaId={source.Id}; SchemaId={clone.Id}; DeptCode={clone.DeptCode}", ct: ct); return await MapSchemaAsync(clone, user.UserId, ct);
    }

    public async Task<EquipmentSchemaDto> CreateVersionAsync(int schemaId, CancellationToken ct = default)
    {
        var user = RequireUser(); var source = await _uow.Repository<F03EquipmentSchema>().Query().Include(x => x.Fields).FirstOrDefaultAsync(x => x.Id == schemaId && x.IsActive == true, ct) ?? throw new KeyNotFoundException("Không tìm thấy mẫu dữ liệu."); await EnsureScopeAsync(user, source.DeptCode, ct); EnsureOwner(user, source); var nextVersion = await _uow.Repository<F03EquipmentSchema>().Query().AsNoTracking().Where(x => x.DeptCode == source.DeptCode && x.SchemaKey == source.SchemaKey).Select(x => (int?)x.Version).MaxAsync(ct) ?? 0; var version = new F03EquipmentSchema { DeptCode = source.DeptCode, SchemaName = source.SchemaName, SchemaKind = source.SchemaKind, SchemaKey = source.SchemaKey, Version = nextVersion + 1, Status = "Draft", IsActive = true, CreatedBy = user.UserId, SourceSchemaId = source.Id, SourceFileName = source.SourceFileName, CreatedFromExcel = source.CreatedFromExcel }; await _uow.Repository<F03EquipmentSchema>().AddAsync(version, ct); await _uow.SaveChangesAsync(ct); foreach (var field in source.Fields.Where(x => x.IsActive == true)) await _uow.Repository<F03EquipmentFieldDefinition>().AddAsync(new F03EquipmentFieldDefinition { SchemaId = version.Id, DeptCode = version.DeptCode, FieldKey = field.FieldKey, FieldLabel = field.FieldLabel, DataType = field.DataType, IsRequired = field.IsRequired, IsImportable = field.IsImportable, IsSearchable = field.IsSearchable, IsActiveField = true, DisplayOrder = field.DisplayOrder, MaxLength = field.MaxLength, DefaultValue = field.DefaultValue, OptionsJson = field.OptionsJson, IsActive = true, CreatedBy = user.UserId }, ct); await _uow.SaveChangesAsync(ct); await _audit.LogAction("EQUIPMENT_SCHEMA_VERSION_CREATED", user.UserId, $"SourceSchemaId={source.Id}; SchemaId={version.Id}; Version={version.Version}", ct: ct); return await MapSchemaAsync(version, user.UserId, ct);
    }

    public async Task<ServiceResult<EquipmentExcelWorkbookDto>> InspectExcelAsync(string deptCode, string fileName, Stream content, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            deptCode = deptCode.Trim().ToUpperInvariant();
            await EnsureScopeAsync(user, deptCode, ct);
            if (content == null || !content.CanRead) return ServiceResult<EquipmentExcelWorkbookDto>.Fail("File Excel không hợp lệ.");

            await using var buffered = new MemoryStream();
            await content.CopyToAsync(buffered, ct);
            buffered.Position = 0;
            IWorkbook workbook;
            try { workbook = WorkbookFactory.Create(buffered); }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException)
            { return ServiceResult<EquipmentExcelWorkbookDto>.Fail("Không thể đọc file Excel. Hãy kiểm tra file không bị hỏng và đúng định dạng."); }

            using (workbook)
            {
                if (workbook.NumberOfSheets <= 0) return ServiceResult<EquipmentExcelWorkbookDto>.Fail("File Excel không có sheet.");
                var sheets = new List<EquipmentExcelSheetDto>();
                var formatter = new DataFormatter();
                var evaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();
                for (var i = 0; i < workbook.NumberOfSheets; i++)
                {
                    var sheet = workbook.GetSheetAt(i);
                    var first = sheet.FirstRowNum;
                    while (first <= sheet.LastRowNum && sheet.GetRow(first) == null) first++;
                    var header = first <= sheet.LastRowNum ? sheet.GetRow(first) : null;
                    var columnCount = header?.LastCellNum > 0 ? header!.LastCellNum : 0;
                    var hasData = header != null && columnCount > 0 && Enumerable.Range(0, columnCount).Any(col => !string.IsNullOrWhiteSpace(GetCellText(header.GetCell(col), formatter, evaluator)));
                    sheets.Add(new EquipmentExcelSheetDto
                    {
                        Index = i,
                        Name = workbook.GetSheetName(i),
                        RowCount = Math.Max(0, sheet.LastRowNum - first),
                        ColumnCount = columnCount,
                        HasData = hasData
                    });
                }
                return ServiceResult<EquipmentExcelWorkbookDto>.Ok(new EquipmentExcelWorkbookDto { FileName = Path.GetFileName(fileName), Sheets = sheets });
            }
        }
        catch (ArgumentException ex) { return ServiceResult<EquipmentExcelWorkbookDto>.Fail(ex.Message); }
        catch (InvalidOperationException ex) { return ServiceResult<EquipmentExcelWorkbookDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<EquipmentSchemaFromExcelDto>> PreviewSchemaFromExcelSheetAsync(string deptCode, string fileName, Stream content, int sheetIndex, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            deptCode = deptCode.Trim().ToUpperInvariant();
            await EnsureScopeAsync(user, deptCode, ct);
            var data = await ReadExcelAsync(fileName, content, ct, sheetIndex);
            var inferred = ExcelSchemaInference.Infer(data.InferenceColumns);
            return ServiceResult<EquipmentSchemaFromExcelDto>.Ok(new EquipmentSchemaFromExcelDto
            {
                FileName = Path.GetFileName(fileName),
                SuggestedSchemaName = Path.GetFileNameWithoutExtension(fileName),
                ColumnCount = data.Headers.Count,
                SampleRowCount = data.SampleRowCount,
                Fields = inferred.Select(x => new EquipmentFieldDefinitionDto { DeptCode = deptCode, FieldKey = x.FieldKey, FieldLabel = x.FieldLabel, DataType = x.DataType, IsRequired = x.IsRequired, IsImportable = true, IsActiveField = true, DisplayOrder = x.DisplayOrder, MaxLength = x.MaxLength }).ToList()
            });
        }
        catch (ArgumentException ex) { return ServiceResult<EquipmentSchemaFromExcelDto>.Fail(ex.Message); }
        catch (InvalidOperationException ex) { return ServiceResult<EquipmentSchemaFromExcelDto>.Fail(ex.Message); }
        catch (KeyNotFoundException ex) { return ServiceResult<EquipmentSchemaFromExcelDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<EquipmentSchemaDto>> CreateSchemaFromExcelSheetAsync(string deptCode, string fileName, Stream content, int sheetIndex, string? schemaName, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            deptCode = deptCode.Trim().ToUpperInvariant();
            await EnsureScopeAsync(user, deptCode, ct);
            var data = await ReadExcelAsync(fileName, content, ct, sheetIndex);
            var inferred = ExcelSchemaInference.Infer(data.InferenceColumns);
            var name = string.IsNullOrWhiteSpace(schemaName) ? Path.GetFileNameWithoutExtension(fileName) : schemaName.Trim();
            ValidateSchemaName(name);
            var schema = new F03EquipmentSchema { DeptCode = deptCode, SchemaName = name, SchemaKind = "Equipment", SchemaKey = Guid.NewGuid().ToString("N"), Version = 1, Status = "Draft", IsActive = true, CreatedBy = user.UserId, SourceFileName = Path.GetFileName(fileName), CreatedFromExcel = true };
            await _uow.Repository<F03EquipmentSchema>().AddAsync(schema, ct);
            await _uow.SaveChangesAsync(ct);
            foreach (var field in inferred) await _uow.Repository<F03EquipmentFieldDefinition>().AddAsync(new F03EquipmentFieldDefinition { SchemaId = schema.Id, DeptCode = deptCode, FieldKey = field.FieldKey, FieldLabel = field.FieldLabel, DataType = field.DataType, IsRequired = field.IsRequired, IsImportable = true, IsActiveField = true, DisplayOrder = field.DisplayOrder, MaxLength = field.MaxLength, CreatedBy = user.UserId, IsActive = true }, ct);
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAction("EQUIPMENT_SCHEMA_CREATED_FROM_EXCEL", user.UserId, $"SchemaId={schema.Id}; DeptCode={deptCode}; FileName={Path.GetFileName(fileName)}; SheetIndex={sheetIndex}; Columns={inferred.Count}", ct: ct);
            return ServiceResult<EquipmentSchemaDto>.Ok(await MapSchemaAsync(schema, user.UserId, ct));
        }
        catch (ArgumentException ex) { return ServiceResult<EquipmentSchemaDto>.Fail(ex.Message); }
        catch (InvalidOperationException ex) { return ServiceResult<EquipmentSchemaDto>.Fail(ex.Message); }
        catch (KeyNotFoundException ex) { return ServiceResult<EquipmentSchemaDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<EquipmentImportBatchDto>> StageExcelSheetAsync(string deptCode, int? schemaId, string fileName, Stream content, int sheetIndex, bool assignToEmployee = false, CancellationToken ct = default)
    {
        try
        {
            var user = RequireUser();
            deptCode = deptCode.Trim().ToUpperInvariant();
            await EnsureScopeAsync(user, deptCode, ct);
            var data = await ReadExcelAsync(fileName, content, ct, sheetIndex);
            var schema = schemaId.HasValue && schemaId.Value > 0
                ? await _uow.Repository<F03EquipmentSchema>().Query().Include(x => x.Fields).FirstOrDefaultAsync(x => x.Id == schemaId.Value && x.DeptCode == deptCode && x.Status == "Active" && x.IsActive == true, ct)
                : await GetActiveSchemaEntityAsync(deptCode, ct);
            if (schema == null) return ServiceResult<EquipmentImportBatchDto>.Fail(schemaId.HasValue ? "Không tìm thấy mẫu dữ liệu đang sử dụng được chọn cho bộ phận này." : "Không tìm thấy mẫu dữ liệu đang sử dụng.");
            var defs = schema.Fields.Where(x => x.IsActive == true && x.IsActiveField && x.IsImportable).OrderBy(x => x.DisplayOrder).ToList();
            var batch = new F03EquipmentImportBatch { SchemaId = schema.Id, DeptCode = deptCode, FileName = Path.GetFileName(fileName), Status = "Preview", TotalRows = data.Rows.Count, AssignToEmployee = assignToEmployee, CreatedBy = user.UserId };
            await _uow.Repository<F03EquipmentImportBatch>().AddAsync(batch, ct);
            await _uow.SaveChangesAsync(ct);
            foreach (var item in data.Rows)
            {
                var error = ValidateRow(item.Values, defs);
                await _uow.Repository<F03EquipmentImportRow>().AddAsync(new F03EquipmentImportRow { BatchId = batch.Id, RowNumber = item.RowNumber, RawJson = JsonSerializer.Serialize(item.Values), Status = error == null ? "Valid" : "Invalid", ErrorMessage = error }, ct);
            }
            await _uow.SaveChangesAsync(ct);
            var valid = await _uow.Repository<F03EquipmentImportRow>().Query().CountAsync(x => x.BatchId == batch.Id && x.Status == "Valid", ct);
            batch.ValidRows = valid;
            batch.InvalidRows = data.Rows.Count - valid;
            batch.Status = batch.InvalidRows == 0 ? "Ready" : "Preview";
            await _uow.SaveChangesAsync(ct);
            var rows = await _uow.Repository<F03EquipmentImportRow>().Query().Where(x => x.BatchId == batch.Id).OrderBy(x => x.RowNumber).Select(x => new EquipmentImportRowDto { RowNumber = x.RowNumber, Status = x.Status, ErrorMessage = x.ErrorMessage }).ToListAsync(ct);
            return ServiceResult<EquipmentImportBatchDto>.Ok(MapBatch(batch, rows));
        }
        catch (ArgumentException ex) { return ServiceResult<EquipmentImportBatchDto>.Fail(ex.Message); }
        catch (InvalidOperationException ex) { return ServiceResult<EquipmentImportBatchDto>.Fail(ex.Message); }
        catch (KeyNotFoundException ex) { return ServiceResult<EquipmentImportBatchDto>.Fail(ex.Message); }
    }

    public async Task<EquipmentSchemaFromExcelDto> PreviewSchemaFromExcelAsync(string deptCode, string fileName, Stream content, CancellationToken ct = default)
    { var user = RequireUser(); await EnsureScopeAsync(user, deptCode, ct); var data = await ReadExcelAsync(fileName, content, ct); var inferred = ExcelSchemaInference.Infer(data.InferenceColumns); return new EquipmentSchemaFromExcelDto { FileName = Path.GetFileName(fileName), SuggestedSchemaName = Path.GetFileNameWithoutExtension(fileName), ColumnCount = data.Headers.Count, SampleRowCount = data.SampleRowCount, Fields = inferred.Select(x => new EquipmentFieldDefinitionDto { DeptCode = deptCode.Trim().ToUpperInvariant(), FieldKey = x.FieldKey, FieldLabel = x.FieldLabel, DataType = x.DataType, IsRequired = x.IsRequired, IsImportable = true, IsActiveField = true, DisplayOrder = x.DisplayOrder, MaxLength = x.MaxLength }).ToList() }; }

    public async Task<EquipmentSchemaDto> CreateSchemaFromExcelAsync(string deptCode, string fileName, Stream content, string? schemaName, CancellationToken ct = default)
    { var user = RequireUser(); deptCode = deptCode.Trim().ToUpperInvariant(); await EnsureScopeAsync(user, deptCode, ct); var data = await ReadExcelAsync(fileName, content, ct); var inferred = ExcelSchemaInference.Infer(data.InferenceColumns); var name = string.IsNullOrWhiteSpace(schemaName) ? Path.GetFileNameWithoutExtension(fileName) : schemaName.Trim(); ValidateSchemaName(name); var schema = new F03EquipmentSchema { DeptCode = deptCode, SchemaName = name, SchemaKind = "Equipment", SchemaKey = Guid.NewGuid().ToString("N"), Version = 1, Status = "Draft", IsActive = true, CreatedBy = user.UserId, SourceFileName = Path.GetFileName(fileName), CreatedFromExcel = true }; await _uow.Repository<F03EquipmentSchema>().AddAsync(schema, ct); await _uow.SaveChangesAsync(ct); foreach (var field in inferred) await _uow.Repository<F03EquipmentFieldDefinition>().AddAsync(new F03EquipmentFieldDefinition { SchemaId = schema.Id, DeptCode = deptCode, FieldKey = field.FieldKey, FieldLabel = field.FieldLabel, DataType = field.DataType, IsRequired = field.IsRequired, IsImportable = true, IsActiveField = true, DisplayOrder = field.DisplayOrder, MaxLength = field.MaxLength, CreatedBy = user.UserId, IsActive = true }, ct); await _uow.SaveChangesAsync(ct); await _audit.LogAction("EQUIPMENT_SCHEMA_CREATED_FROM_EXCEL", user.UserId, $"SchemaId={schema.Id}; DeptCode={deptCode}; FileName={Path.GetFileName(fileName)}; Columns={inferred.Count}", ct: ct); return await MapSchemaAsync(schema, user.UserId, ct); }

    public async Task<EquipmentImportBatchDto> StageExcelAsync(string deptCode, int? schemaId, string fileName, Stream content, bool assignToEmployee, CancellationToken ct = default)
    {
        var user = RequireUser();
        deptCode = deptCode.Trim().ToUpperInvariant();
        await EnsureScopeAsync(user, deptCode, ct);
        var data = await ReadExcelAsync(fileName, content, ct);
        var schema = schemaId.HasValue && schemaId.Value > 0
            ? await _uow.Repository<F03EquipmentSchema>().Query().Include(x => x.Fields)
                .FirstOrDefaultAsync(x => x.Id == schemaId.Value && x.DeptCode == deptCode && x.Status == "Active" && x.IsActive == true, ct)
            : await GetActiveSchemaEntityAsync(deptCode, ct);
        if (schema == null) throw new InvalidOperationException(schemaId.HasValue
            ? "Không tìm thấy mẫu dữ liệu đang sử dụng được chọn cho bộ phận này."
            : "Không tìm thấy mẫu dữ liệu đang sử dụng.");
        var defs = schema.Fields.Where(x => x.IsActive == true && x.IsActiveField && x.IsImportable).OrderBy(x => x.DisplayOrder).ToList();
        var batch = new F03EquipmentImportBatch { SchemaId = schema.Id, DeptCode = deptCode, FileName = Path.GetFileName(fileName), Status = "Preview", TotalRows = data.Rows.Count, AssignToEmployee = assignToEmployee, CreatedBy = user.UserId };
        await _uow.Repository<F03EquipmentImportBatch>().AddAsync(batch, ct);
        await _uow.SaveChangesAsync(ct);
        foreach (var item in data.Rows)
        {
            var error = ValidateRow(item.Values, defs);
            await _uow.Repository<F03EquipmentImportRow>().AddAsync(new F03EquipmentImportRow { BatchId = batch.Id, RowNumber = item.RowNumber, RawJson = JsonSerializer.Serialize(item.Values), Status = error == null ? "Valid" : "Invalid", ErrorMessage = error }, ct);
        }
        await _uow.SaveChangesAsync(ct);
        var valid = await _uow.Repository<F03EquipmentImportRow>().Query().CountAsync(x => x.BatchId == batch.Id && x.Status == "Valid", ct);
        batch.ValidRows = valid;
        batch.InvalidRows = data.Rows.Count - valid;
        batch.Status = batch.InvalidRows == 0 ? "Ready" : "Preview";
        await _uow.SaveChangesAsync(ct);
        return MapBatch(batch, await _uow.Repository<F03EquipmentImportRow>().Query().Where(x => x.BatchId == batch.Id).OrderBy(x => x.RowNumber).Select(x => new EquipmentImportRowDto { RowNumber = x.RowNumber, Status = x.Status, ErrorMessage = x.ErrorMessage }).ToListAsync(ct));
    }

    public async Task<EquipmentImportBatchDto?> GetBatchAsync(int batchId, CancellationToken ct = default)
    {
        var user = RequireUser();
        var batch = await _uow.Repository<F03EquipmentImportBatch>().Query().FirstOrDefaultAsync(x => x.Id == batchId && x.IsActive == true, ct);
        if (batch == null) return null;
        await EnsureScopeAsync(user, batch.DeptCode, ct);
        var rows = await _uow.Repository<F03EquipmentImportRow>().Query().Where(x => x.BatchId == batch.Id).OrderBy(x => x.RowNumber).Select(x => new EquipmentImportRowDto { RowNumber = x.RowNumber, Status = x.Status, ErrorMessage = x.ErrorMessage }).ToListAsync(ct);
        return MapBatch(batch, rows);
    }

    public async Task<EquipmentImportCommitResultDto> CommitAsync(int batchId, CancellationToken ct = default)
    {
        var user = RequireUser(); var b = await _uow.Repository<F03EquipmentImportBatch>().Query().FirstOrDefaultAsync(x => x.Id == batchId && x.IsActive == true, ct) ?? throw new KeyNotFoundException("Không tìm thấy lô nhập Excel."); await EnsureScopeAsync(user, b.DeptCode, ct); if (b.Status == "Imported") return new() { BatchId = b.Id, ImportedRows = b.ImportedRows }; if (b.Status != "Ready") throw new InvalidOperationException("Lô nhập Excel chưa sẵn sàng. Hãy xử lý các dòng lỗi trước.");
        var rows = await _uow.Repository<F03EquipmentImportRow>().Query().Where(x => x.BatchId == b.Id && x.Status == "Valid").OrderBy(x => x.RowNumber).ToListAsync(ct); var defs = new List<F03EquipmentFieldDefinition>(); if (b.SchemaId.HasValue) { var schema = await _uow.Repository<F03EquipmentSchema>().Query().Include(x => x.Fields).FirstOrDefaultAsync(x => x.Id == b.SchemaId.Value && x.Status == "Active" && x.IsActive == true, ct) ?? throw new InvalidOperationException("Phiên bản mẫu dữ liệu dùng cho lô nhập không còn đang sử dụng."); defs = schema.Fields.Where(x => x.IsActive == true && x.IsActiveField && x.IsImportable).ToList(); } else defs = await _uow.Repository<F03EquipmentFieldDefinition>().Query().Where(x => x.DeptCode == b.DeptCode && x.IsActive == true && x.IsActiveField && x.IsImportable).ToListAsync(ct);
        if (b.AssignToEmployee) foreach (var row in rows) { var data = DeserializeRow(row.RawJson); var employeeCode = GetValue(data, "EmployeeCode", "Mã nhân viên", "Ma nhan vien", "NguoiPhuTrach"); var employee = !string.IsNullOrWhiteSpace(employeeCode) ? await _uow.Repository<F03Employee>().Query().AsNoTracking().Where(e => e.EmployeeCode == employeeCode && e.IsActive == true && e.EndWorkingDate == null).Select(e => new { e.EmployeeCode, e.DeptCode }).FirstOrDefaultAsync(ct) : null; if (employee == null || !string.Equals(employee.DeptCode, b.DeptCode, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Mã nhân viên ở dòng {row.RowNumber} không còn hợp lệ tại thời điểm nhập."); }
        var imported = 0; foreach (var row in rows) { var data = DeserializeRow(row.RawJson); var code = GetValue(data, "EquipmentCode", "Mã thiết bị", "Mã TB", "Mã tài sản", "AssetCode"); var name = GetValue(data, "EquipmentName", "Tên thiết bị", "Tên TB", "Tên tài sản"); if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) { row.Status = "Skipped"; row.ErrorMessage = "Thiếu mã hoặc tên thiết bị."; continue; } var exists = await _uow.Repository<F03EquipmentAsset>().Query().FirstOrDefaultAsync(x => x.EquipmentCode == code && x.DeptCode == b.DeptCode, ct); if (exists != null) { row.Status = "Skipped"; row.ErrorMessage = "Mã thiết bị đã tồn tại trong bộ phận."; continue; } var employeeCode = b.AssignToEmployee ? GetValue(data, "EmployeeCode", "Mã nhân viên", "Ma nhan vien", "NguoiPhuTrach") : null; var employee = !string.IsNullOrWhiteSpace(employeeCode) ? await _uow.Repository<F03Employee>().Query().AsNoTracking().Where(e => e.EmployeeCode == employeeCode && e.IsActive == true && e.EndWorkingDate == null).Select(e => new { e.EmployeeCode, e.DeptCode }).FirstOrDefaultAsync(ct) : null; if (b.AssignToEmployee && employee == null) { row.Status = "Skipped"; row.ErrorMessage = "Mã nhân viên không còn hợp lệ tại thời điểm nhập."; continue; } var asset = new F03EquipmentAsset { EquipmentCode = code, EquipmentName = name, Specification = GetValue(data, "Specification", "Thông số", "Thông số kỹ thuật"), SerialNumber = GetValue(data, "SerialNumber", "Serial", "Số serial", "S/N"), AssetCode = GetValue(data, "AssetCode", "Mã tài sản"), PurchasePrice = ParseDecimal(GetValue(data, "PurchasePrice", "Nguyên giá", "Giá mua", "Giá")), PurchaseDate = ParseDate(GetValue(data, "PurchaseDate", "Ngày mua", "Ngày nhập")) ?? DateTime.Today, ExpectedDepreciationDate = ParseDate(GetValue(data, "ExpectedDepreciationDate", "Ngày khấu hao", "Ngày hết khấu hao")) ?? DateTime.Today, DeptCode = b.DeptCode, Location = GetValue(data, "Location", "Vị trí", "Địa điểm"), QrToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Guid.NewGuid().ToString("N"), IsQrActive = true, Note = GetValue(data, "Note", "Ghi chú"), CustomDataJson = JsonSerializer.Serialize(BuildCustomData(data, defs)), ResponsibleEmployeeCode = employee?.EmployeeCode, OperatingResponsibleEmployeeCode = employee?.EmployeeCode, OperatingResponsibleDeptCode = employee?.EmployeeCode == null ? null : employee.DeptCode, ResponsibleAssignedAt = employee == null ? null : DateTime.UtcNow, CreatedBy = user.UserId }; await _uow.Repository<F03EquipmentAsset>().AddAsync(asset, ct); await _uow.SaveChangesAsync(ct); row.AssetId = asset.Id; row.Status = "Imported"; imported++; }
        b.ImportedRows = imported; b.Status = "Imported"; b.CompletedAt = DateTime.Now; b.ModifiedBy = user.UserId; b.ModifiedAt = DateTime.Now; await _uow.SaveChangesAsync(ct); var skipped = rows.Count(x => x.Status == "Skipped"); await _audit.LogAction("EQUIPMENT_IMPORT_COMMITTED", user.UserId, $"BatchId={b.Id}; SchemaId={b.SchemaId}; DeptCode={b.DeptCode}; Imported={imported}; Skipped={skipped}", ct: ct); return new() { BatchId = b.Id, ImportedRows = imported, SkippedRows = skipped };
    }

    private async Task<F03EquipmentSchema?> GetActiveSchemaEntityAsync(string deptCode, CancellationToken ct) => await _uow.Repository<F03EquipmentSchema>().Query().Include(x => x.Fields).Where(x => x.DeptCode == deptCode.Trim().ToUpperInvariant() && x.Status == "Active" && x.IsActive == true).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
    private async Task DeactivateOtherActiveVersionsAsync(F03EquipmentSchema schema, CancellationToken ct) { var active = await _uow.Repository<F03EquipmentSchema>().Query().Where(x => x.Id != schema.Id && x.DeptCode == schema.DeptCode && x.SchemaKey == schema.SchemaKey && x.Status == "Active" && x.IsActive == true).ToListAsync(ct); foreach (var item in active) item.Status = "Inactive"; }
    private async Task EnsureScopeAsync(FVN_REGISTER.Contract.Dtos.Authentication.UserIdentityDto user, string deptCode, CancellationToken ct) { if (string.IsNullOrWhiteSpace(deptCode)) throw new ArgumentException("Bộ phận là bắt buộc."); if (!await _authorization.CanAccessAsync(user, SecurityFunctionCodes.EquipmentImport, null, deptCode.Trim().ToUpperInvariant(), ct)) throw new UnauthorizedAccessException("Bạn không có quyền quản lý hoặc nhập dữ liệu Excel cho bộ phận này."); }
    private static void EnsureOwner(FVN_REGISTER.Contract.Dtos.Authentication.UserIdentityDto user, F03EquipmentSchema schema) { if (schema.CreatedBy != user.UserId) throw new UnauthorizedAccessException("Bạn chỉ được sửa mẫu dữ liệu do chính mình tạo. Bạn có thể xem, sử dụng hoặc nhân bản mẫu của người khác."); }
    private static void EnsureDraft(F03EquipmentSchema schema) { if (!string.Equals(schema.Status, "Draft", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Chỉ được sửa mẫu dữ liệu ở trạng thái Bản nháp. Hãy tạo phiên bản mới để thay đổi mẫu đang sử dụng."); }
    private async Task<EquipmentSchemaDto> MapSchemaAsync(F03EquipmentSchema schema, int userId, CancellationToken ct) { var owner = schema.CreatedBy > 0 ? await _uow.Repository<F03User>().Query().AsNoTracking().Where(x => x.Id == schema.CreatedBy).Select(x => new { x.FullName, x.EmployeeCode }).FirstOrDefaultAsync(ct) : null; var ownerName = owner == null ? "Hệ thống" : (string.IsNullOrWhiteSpace(owner.FullName) ? owner.EmployeeCode : owner.FullName!); return new EquipmentSchemaDto { Id = schema.Id, DeptCode = schema.DeptCode, SchemaName = schema.SchemaName, SchemaKind = schema.SchemaKind, SchemaKey = schema.SchemaKey, Version = schema.Version, Status = schema.Status, IsActive = schema.IsActive == true, OwnerUserId = schema.CreatedBy, OwnerName = ownerName, SourceSchemaId = schema.SourceSchemaId, SourceFileName = schema.SourceFileName, CreatedFromExcel = schema.CreatedFromExcel, IsOwner = schema.CreatedBy == userId, CanEdit = schema.CreatedBy == userId && string.Equals(schema.Status, "Draft", StringComparison.OrdinalIgnoreCase), CanCreateVersion = schema.CreatedBy == userId, CanClone = true, Fields = schema.Fields.Where(x => x.IsActive != false).OrderBy(x => x.DisplayOrder).ThenBy(x => x.FieldLabel).Select(MapField).ToList() }; }
    private static EquipmentSchemaSummaryDto MapSummary(F03EquipmentSchema schema, int userId, string ownerName, int fieldCount) => new() { Id = schema.Id, DepartmentCode = schema.DeptCode, DepartmentName = schema.DeptCode, SchemaName = schema.SchemaName, SchemaKind = schema.SchemaKind, SchemaKey = schema.SchemaKey, Version = schema.Version, Status = schema.Status, IsActive = schema.IsActive == true, FieldCount = fieldCount, CreatedByUserId = schema.CreatedBy, CreatedByUserName = ownerName, CreatedAt = schema.CreatedAt, SourceSchemaId = schema.SourceSchemaId, SourceFileName = schema.SourceFileName, CreatedFromExcel = schema.CreatedFromExcel, IsOwner = schema.CreatedBy == userId, CanEdit = schema.CreatedBy == userId && string.Equals(schema.Status, "Draft", StringComparison.OrdinalIgnoreCase), CanCreateVersion = schema.CreatedBy == userId, CanClone = true };
    private static EquipmentFieldDefinitionDto MapField(F03EquipmentFieldDefinition x) => new() { Id = x.Id, SchemaId = x.SchemaId, DeptCode = x.DeptCode, FieldKey = x.FieldKey, FieldLabel = x.FieldLabel, DataType = x.DataType, IsRequired = x.IsRequired, IsImportable = x.IsImportable, IsSearchable = x.IsSearchable, IsActiveField = x.IsActiveField, DisplayOrder = x.DisplayOrder, MaxLength = x.MaxLength, DefaultValue = x.DefaultValue, OptionsJson = x.OptionsJson };
    private static System.Linq.Expressions.Expression<Func<F03EquipmentFieldDefinition, EquipmentFieldDefinitionDto>> MapFieldExpression() => x => new EquipmentFieldDefinitionDto { Id=x.Id, SchemaId=x.SchemaId, DeptCode=x.DeptCode, FieldKey=x.FieldKey, FieldLabel=x.FieldLabel, DataType=x.DataType, IsRequired=x.IsRequired, IsImportable=x.IsImportable, IsSearchable=x.IsSearchable, IsActiveField=x.IsActiveField, DisplayOrder=x.DisplayOrder, MaxLength=x.MaxLength, DefaultValue=x.DefaultValue, OptionsJson=x.OptionsJson };
    private static EquipmentImportBatchDto MapBatch(F03EquipmentImportBatch x, IEnumerable<F03EquipmentImportRow>? rows = null) => new() { Id=x.Id, SchemaId=x.SchemaId, DeptCode=x.DeptCode, FileName=x.FileName, Status=x.Status, TotalRows=x.TotalRows, ValidRows=x.ValidRows, InvalidRows=x.InvalidRows, ImportedRows=x.ImportedRows, AssignToEmployee=x.AssignToEmployee, Rows=rows?.Select(r => new EquipmentImportRowDto { RowNumber=r.RowNumber, Status=r.Status, ErrorMessage=r.ErrorMessage }).ToList() ?? new() };
    private static EquipmentImportBatchDto MapBatch(F03EquipmentImportBatch x, IEnumerable<EquipmentImportRowDto> rows) => new() { Id=x.Id, SchemaId=x.SchemaId, DeptCode=x.DeptCode, FileName=x.FileName, Status=x.Status, TotalRows=x.TotalRows, ValidRows=x.ValidRows, InvalidRows=x.InvalidRows, ImportedRows=x.ImportedRows, AssignToEmployee=x.AssignToEmployee, Rows=rows.ToList() };
    private static string? ValidateRow(Dictionary<string,string?> data, List<F03EquipmentFieldDefinition> defs) { var code = GetValue(data,"EquipmentCode","Mã thiết bị","Mã TB","Mã tài sản","AssetCode"); var name = GetValue(data,"EquipmentName","Tên thiết bị","Tên TB","Tên tài sản"); if (string.IsNullOrWhiteSpace(code)) return "Thiếu mã thiết bị."; if (string.IsNullOrWhiteSpace(name)) return "Thiếu tên thiết bị."; foreach (var d in defs.Where(x=>x.IsRequired)) { var v = GetValue(data,d.FieldKey,d.FieldLabel); if (string.IsNullOrWhiteSpace(v)) return $"Thiếu trường bắt buộc: {d.FieldLabel}."; if (!ValidateType(v,d.DataType)) return $"Sai kiểu dữ liệu: {d.FieldLabel} ({d.DataType})."; if (d.MaxLength.HasValue && v.Length > d.MaxLength.Value) return $"Trường {d.FieldLabel} vượt quá {d.MaxLength.Value} ký tự."; } return null; }
    private static bool ValidateType(string value, string type) => type.ToLowerInvariant() switch { "integer" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || int.TryParse(value, NumberStyles.Integer, new CultureInfo("vi-VN"), out _), "number" or "decimal" or "currency" => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _) || decimal.TryParse(value, NumberStyles.Any, new CultureInfo("vi-VN"), out _), "date" or "datetime" => ParseDate(value).HasValue, "boolean" => bool.TryParse(value,out _) || NormalizeKey(value) is "0" or "1" or "co" or "khong" or "yes" or "no", "email" => Regex.IsMatch(value.Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$"), _ => true };
    private static Dictionary<string,string?> BuildCustomData(Dictionary<string,string?> source,List<F03EquipmentFieldDefinition> defs) { var r=new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase); foreach(var p in source) { var k=defs.FirstOrDefault(x=>NormalizeKey(x.FieldKey)==NormalizeKey(p.Key)||NormalizeKey(x.FieldLabel)==NormalizeKey(p.Key))?.FieldKey; if(!string.IsNullOrWhiteSpace(k)) r[k]=p.Value; else if(!IsStandardColumn(p.Key)) r[NormalizeKey(p.Key)]=p.Value; } return r; }
    private static bool IsStandardColumn(string v)=>new[]{"equipmentcode","equipmentname","specification","serialnumber","assetcode","purchaseprice","purchasedate","expecteddepreciationdate","deptcode","location","note","matb","mattb","matasan","tenthietbi","tentb","thongso","thongsokythuat","serial","soserial","sn","ngaymua","ngaynhap","ngaykhauhao","ngayhethauhao","nguyengia","giamua","gia","vitri","diadiem","ghichu"}.Contains(NormalizeKey(v));
    private static string? GetValue(Dictionary<string,string?> data,params string[] names){foreach(var n in names){var h=data.FirstOrDefault(x=>NormalizeKey(x.Key)==NormalizeKey(n));if(!string.IsNullOrWhiteSpace(h.Key)&&!string.IsNullOrWhiteSpace(h.Value))return h.Value.Trim();}return null;}
    private static Dictionary<string,string?> DeserializeRow(string rawJson)=>JsonSerializer.Deserialize<Dictionary<string,string?>>(rawJson)??new(StringComparer.OrdinalIgnoreCase);
    private static decimal ParseDecimal(string? v){if(string.IsNullOrWhiteSpace(v))return 0;if(decimal.TryParse(v,NumberStyles.Any,CultureInfo.InvariantCulture,out var d))return d;return decimal.TryParse(v,NumberStyles.Any,new CultureInfo("vi-VN"),out d)?d:0;}
    private static DateTime? ParseDate(string? v){if(string.IsNullOrWhiteSpace(v))return null;var f=new[]{"dd/MM/yyyy","d/M/yyyy","yyyy-MM-dd","MM/dd/yyyy","M/d/yyyy","dd/MM/yyyy HH:mm","d/M/yyyy H:mm","yyyy-MM-dd HH:mm:ss"};if(DateTime.TryParseExact(v.Trim(),f,CultureInfo.InvariantCulture,DateTimeStyles.None,out var d))return d;return DateTime.TryParse(v,new CultureInfo("vi-VN"),DateTimeStyles.None,out d)?d:null;}
    private static string NormalizeSchemaKind(string? value)=>string.IsNullOrWhiteSpace(value)?"Equipment":value.Trim();
    private static string NormalizeStatus(string? value)=>string.Equals(value,"Active",StringComparison.OrdinalIgnoreCase)?"Active":string.Equals(value,"Inactive",StringComparison.OrdinalIgnoreCase)?"Inactive":"Draft";
    private static void ValidateSchemaName(string name){if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Tên mẫu dữ liệu là bắt buộc.");if(name.Length>150)throw new ArgumentException("Tên mẫu dữ liệu không được vượt quá 150 ký tự.");}
    private static void ValidateDataType(string type){var allowed=new[]{"Text","Integer","Number","Decimal","Date","DateTime","Boolean","Email","Currency","Choice"};if(!allowed.Contains(type,StringComparer.OrdinalIgnoreCase))throw new ArgumentException("Kiểu dữ liệu không được hỗ trợ.");}
    private static string NormalizeKey(string v){var d=v.Trim().Normalize(NormalizationForm.FormD);var sb=new StringBuilder(d.Length);foreach(var c in d)if(System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.NonSpacingMark)sb.Append(char.ToLowerInvariant(c));return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC),@"[^a-z0-9]+","");}
    private FVN_REGISTER.Contract.Dtos.Authentication.UserIdentityDto RequireUser()=>_currentUser.GetCurrentUser()??throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
    private sealed class ExcelData { public List<string> Headers { get; init; } = new(); public List<ExcelRow> Rows { get; init; } = new(); public int SampleRowCount { get; init; } public List<ExcelSchemaInference.InputColumn> InferenceColumns { get; init; } = new(); }
    private sealed class ExcelRow { public int RowNumber { get; init; } public Dictionary<string,string?> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase); }
    private static async Task<ExcelData> ReadExcelAsync(string fileName, Stream content, CancellationToken ct, int sheetIndex = 0)
    {
        if(content==null||!content.CanRead) throw new ArgumentException("File Excel không hợp lệ."); await using var buffered=new MemoryStream(); await content.CopyToAsync(buffered,ct); buffered.Position=0; IWorkbook wb; try{wb=WorkbookFactory.Create(buffered);}catch(Exception ex){throw new InvalidOperationException("Không thể đọc file Excel. Hãy kiểm tra file không bị mã hóa/hỏng và đúng định dạng.",ex);} using(wb)
        {
            if (sheetIndex < 0 || sheetIndex >= wb.NumberOfSheets) throw new InvalidOperationException($"Sheet Excel không hợp lệ (index={sheetIndex})."); var ws=wb.GetSheetAt(sheetIndex)??throw new InvalidOperationException("File Excel không có sheet."); var formatter=new DataFormatter(); var evaluator=wb.GetCreationHelper().CreateFormulaEvaluator(); var first=ws.FirstRowNum; while(first<=ws.LastRowNum&&ws.GetRow(first)==null)first++; if(first>ws.LastRowNum)throw new InvalidOperationException("File Excel không có dữ liệu."); var headerRow=ws.GetRow(first)!; var lastCol=headerRow.LastCellNum; if(lastCol<=0)throw new InvalidOperationException("Excel phải có tiêu đề và ít nhất một cột.");
            var headers=Enumerable.Range(0,lastCol).Select(i=>GetCellText(headerRow.GetCell(i),formatter,evaluator).Trim()).ToList(); if(headers.Any(string.IsNullOrWhiteSpace))throw new InvalidOperationException($"Sheet '{wb.GetSheetName(sheetIndex)}' có cột tiêu đề bị trống."); var normalized=headers.Select(NormalizeKey).ToList(); if(normalized.Count!=normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count())throw new InvalidOperationException("Excel có tên cột trùng nhau sau khi chuẩn hóa. Hãy đổi tên các cột trước khi tạo mẫu dữ liệu.");
            var rows=new List<ExcelRow>(); var sampleValues=headers.Select(_=>new List<string?>()).ToList(); for(var rowIndex=first+1;rowIndex<=ws.LastRowNum;rowIndex++){ct.ThrowIfCancellationRequested();var excelRow=ws.GetRow(rowIndex);if(excelRow==null)continue;var data=new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase);for(var col=0;col<headers.Count;col++){var value=GetCellText(excelRow.GetCell(col),formatter,evaluator).Trim();data[headers[col]]=value;if(sampleValues[col].Count<200)sampleValues[col].Add(value);}if(data.Values.All(string.IsNullOrWhiteSpace))continue;rows.Add(new ExcelRow{RowNumber=rowIndex+1,Values=data});}
            var inference=headers.Select((h,i)=>new ExcelSchemaInference.InputColumn(h,sampleValues[i])).ToList(); return new ExcelData{Headers=headers,Rows=rows,SampleRowCount=Math.Min(rows.Count,200),InferenceColumns=inference};
        }
    }
    private static string GetCellText(ICell? cell, DataFormatter formatter, IFormulaEvaluator evaluator)
    {
        if (cell == null) return string.Empty;
        if (cell.CellType == CellType.Formula)
        {
            try { return formatter.FormatCellValue(cell, evaluator); }
            catch (InvalidOperationException) { return formatter.FormatCellValue(cell); }
        }
        if (cell.CellType == CellType.Numeric)
        {
            try
            {
                if (DateUtil.IsCellDateFormatted(cell))
                    return string.Format(CultureInfo.InvariantCulture, "{0:dd/MM/yyyy}", cell.DateCellValue);
            }
            catch (InvalidOperationException) { }
        }
        return formatter.FormatCellValue(cell);
    }
}