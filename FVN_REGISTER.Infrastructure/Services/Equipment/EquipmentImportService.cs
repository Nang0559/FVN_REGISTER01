using System.Text.Json;
using System.Globalization;
using System.Text;
using FVN_REGISTER.Application.Interfaces.Auths;
using FVN_REGISTER.Application.Interfaces.Common;
using FVN_REGISTER.Application.Interfaces.Equipment;
using FVN_REGISTER.Application.Interfaces.Excel;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.EquipmentImport;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.Equipment;
using FVN_REGISTER.Core.Entities.Approvers;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Excel;
using FVN_REGISTER.Core.Repositories;
using FVN_REGISTER.Core.Schema;
using FVN_REGISTER.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Equipment;

public sealed class EquipmentImportService : IEquipmentImportService
{
    private const string ModuleCode="EQUIPMENT", EntityCode="EQUIPMENT_ASSET";
    private readonly FVNWEBAPPContext _db; private readonly IExcelPlatform _excel; private readonly ICurrentUserService _users;
    private readonly IAuthorizationService _auth; private readonly IUnitOfWork _uow;
    private readonly IDepartmentLookupService _departmentLookup;
    public EquipmentImportService(FVNWEBAPPContext db,IExcelPlatform excel,ICurrentUserService users,IAuthorizationService auth,IAuditService audit,IUnitOfWork uow,IDepartmentLookupService departmentLookup)
        =>(_db,_excel,_users,_auth,_uow,_departmentLookup)=(db,excel,users,auth,uow,departmentLookup);

    public async Task<List<ExcelSchemaSummaryDto>> GetSchemasAsync(int? departmentCode,CancellationToken ct=default)
    {
        var u=User();var dept=Dept(departmentCode,u.DeptCode);await Scope(u,dept,ct);var result=new List<ExcelSchemaSummaryDto>();
        foreach(var s in await _excel.GetSchemasAsync(ModuleCode,EntityCode,true,ct)){var m=await Meta(s.Id,ct);if(m.DepartmentCode!=dept)continue;var owner=m.CreatedBy==u.UserId||u.IsAdmin;var ownerName=await Owner(m.CreatedBy,ct);result.Add(new(){Id=s.Id,ModuleCode=s.ModuleCode,EntityCode=s.EntityCode,SchemaCode=s.SchemaKey,SchemaName=s.SchemaName,DepartmentCode=m.DepartmentCode,Version=s.Version,Status=s.Status.ToString(),IsActive=s.Status==ExcelSchemaStatus.Active,FieldCount=m.FieldCount,CreatedByUserId=m.CreatedBy,CreatedByUserName=ownerName,CreatedAt=s.CreatedAt,UpdatedAt=s.UpdatedAt,SourceFileName=s.SourceFileName,CreatedFromExcel=!string.IsNullOrWhiteSpace(s.SourceFileName),IsOwner=owner,CanEdit=owner&&s.Status==ExcelSchemaStatus.Draft,CanCreateVersion=owner,CanClone=true});}return result;
    }
    public async Task<ExcelSchemaDto?> GetSchemaAsync(int id,CancellationToken ct=default){var u=User();var d=await _excel.GetSchemaDefinitionAsync(id,ct);if(d is null)return null;var m=await Meta(id,ct);await Scope(u,m.DepartmentCode,ct);var dto=Map(id,d,m,u);dto.CreatedByUserName=await Owner(m.CreatedBy,ct);return dto;}
    public async Task<ServiceResult<ExcelSchemaDto>> SaveSchemaAsync(ExcelSchemaUpsertRequest r,CancellationToken ct=default){try{var u=User();var dept=Dept(r.DepartmentCode,u.DeptCode);await Scope(u,dept,ct);if(string.IsNullOrWhiteSpace(r.SchemaName))return ServiceResult<ExcelSchemaDto>.Fail("Tên schema là bắt buộc.");if(r.Id is null or <=0){var s=await _excel.CreateDraftSchemaAsync(new ExcelSchemaCreateRequest(ModuleCode,EntityCode,$"{dept}:{Guid.NewGuid():N}",await OwnedSchemaName(r.SchemaName,u.UserId,ct),0,"Sheet1",0,1,null,Array.Empty<int>(),Array.Empty<ExcelSchemaField>()),null,ct);await OwnerSet(s.Id,u.UserId,ct);return ServiceResult<ExcelSchemaDto>.Ok((await GetSchemaAsync(s.Id,ct))!,"Đã tạo schema bản nháp.");}var m=await Meta(r.Id.Value,ct);if(m.CreatedBy!=u.UserId&&!u.IsAdmin)return ServiceResult<ExcelSchemaDto>.Fail("Bạn không có quyền sửa schema.");if(r.Status.Equals("Active",StringComparison.OrdinalIgnoreCase)){var s=await _excel.ActivateSchemaAsync(r.Id.Value,ct);return ServiceResult<ExcelSchemaDto>.Ok((await GetSchemaAsync(s.Id,ct))!,"Đã kích hoạt schema.");}await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.F03ExcelSchemas SET SchemaName={r.SchemaName.Trim()},UpdatedBy={u.UserId.ToString()},UpdatedAt=SYSUTCDATETIME() WHERE Id={r.Id.Value} AND Status=0",ct);return ServiceResult<ExcelSchemaDto>.Ok((await GetSchemaAsync(r.Id.Value,ct))!,"Đã lưu schema.");}catch(Exception e)when(e is UnauthorizedAccessException or ArgumentException or InvalidOperationException or KeyNotFoundException){return ServiceResult<ExcelSchemaDto>.Fail(e.Message);}}
    public async Task<ServiceResult<ExcelSchemaDto>> CloneSchemaAsync(int id,ExcelSchemaCloneRequest r,CancellationToken ct=default){try{var u=User();var d=await _excel.GetSchemaDefinitionAsync(id,ct)??throw new KeyNotFoundException("Không tìm thấy schema.");var m=await Meta(id,ct);await Scope(u,m.DepartmentCode,ct);var s=await _excel.CreateDraftSchemaAsync(new ExcelSchemaCreateRequest(ModuleCode,EntityCode,$"{m.DepartmentCode}:{Guid.NewGuid():N}",await OwnedSchemaName(string.IsNullOrWhiteSpace(r.SchemaName)?$"{m.SchemaName} - Bản sao":r.SchemaName!,u.UserId,ct),d.SheetIndex,d.SheetName,d.HeaderRowIndex,d.DataStartRowIndex,d.DataEndRowIndex,d.SelectedColumnIndexes,d.Fields,d.Culture),m.SourceFileName,ct);await OwnerSet(s.Id,u.UserId,ct);return ServiceResult<ExcelSchemaDto>.Ok((await GetSchemaAsync(s.Id,ct))!,"Đã nhân bản schema.");}catch(Exception e)when(e is UnauthorizedAccessException or ArgumentException or InvalidOperationException or KeyNotFoundException){return ServiceResult<ExcelSchemaDto>.Fail(e.Message);}}
    public async Task<ServiceResult<ExcelSchemaDto>> CreateVersionAsync(int id,CancellationToken ct=default){try{var u=User();var d=await _excel.GetSchemaDefinitionAsync(id,ct)??throw new KeyNotFoundException("Không tìm thấy schema.");var m=await Meta(id,ct);if(m.CreatedBy!=u.UserId&&!u.IsAdmin)throw new UnauthorizedAccessException("Bạn không có quyền tạo phiên bản.");var v=await _db.Database.SqlQueryRaw<int>("SELECT ISNULL(MAX(VersionNo),0)+1 AS Value FROM dbo.F03ExcelSchemaVersions WHERE SchemaId={0}",id).SingleAsync(ct);await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.F03ExcelSchemaVersions(SchemaId,VersionNo,SheetIndex,SheetName,HeaderRowIndex,DataStartRowIndex,DataEndRowIndex,Status,SourceFileName,SelectedColumnsJson,Culture,CreatedAt) VALUES({id},{v},{d.SheetIndex},{d.SheetName},{d.HeaderRowIndex},{d.DataStartRowIndex},{d.DataEndRowIndex},0,{m.SourceFileName},{JsonSerializer.Serialize(d.SelectedColumnIndexes)},{d.Culture},SYSUTCDATETIME())",ct);var vid=await _db.Database.SqlQueryRaw<int>("SELECT TOP 1 Id AS Value FROM dbo.F03ExcelSchemaVersions WHERE SchemaId={0} ORDER BY Id DESC",id).SingleAsync(ct);foreach(var f in d.Fields)await InsertField(vid,f,ct);await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.F03ExcelSchemas SET CurrentVersionId={vid},UpdatedAt=SYSUTCDATETIME() WHERE Id={id}",ct);return ServiceResult<ExcelSchemaDto>.Ok((await GetSchemaAsync(id,ct))!,$"Đã tạo phiên bản v{v}.");}catch(Exception e)when(e is UnauthorizedAccessException or ArgumentException or InvalidOperationException or KeyNotFoundException){return ServiceResult<ExcelSchemaDto>.Fail(e.Message);}}
    public async Task<ServiceResult<ExcelSchemaFieldDto>> SaveSchemaFieldAsync(SaveExcelSchemaFieldRequest r,CancellationToken ct=default){try{var u=User();var allowedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EquipmentCode", "EquipmentName", "ManagedByEmployeeCode", "ResponsibleEmployeeCode", "OperatingResponsibleEmployeeCode", "OperatingResponsibleDeptCode", "AssetCode", "SerialNumber", "Specification", "PurchasePrice", "PurchaseDate", "ExpectedDepreciationDate", "Location", "Note" }; if (!string.IsNullOrWhiteSpace(r.TargetProperty) && !allowedTargets.Contains(r.TargetProperty.Trim())) throw new ArgumentException($"Liên kết nghiệp vụ '{r.TargetProperty}' không được hỗ trợ."); var m=await Meta(r.SchemaId,ct);if(m.CreatedBy!=u.UserId&&!u.IsAdmin)throw new UnauthorizedAccessException("Bạn không có quyền sửa schema.");var vid=await _db.Database.SqlQueryRaw<int?>("SELECT CurrentVersionId AS Value FROM dbo.F03ExcelSchemas WHERE Id={0} AND Status=0",r.SchemaId).SingleOrDefaultAsync(ct)??throw new InvalidOperationException("Schema phải ở trạng thái Draft."); if (!string.IsNullOrWhiteSpace(r.TargetProperty)) { var duplicate = await _db.Database.SqlQueryRaw<int>("SELECT COUNT(1) AS Value FROM dbo.F03ExcelSchemaFields WHERE SchemaVersionId={0} AND TargetProperty={1} AND FieldKey<>{2}", vid, r.TargetProperty.Trim(), r.FieldKey.Trim()).SingleAsync(ct); if (duplicate > 0) throw new InvalidOperationException($"Trường nghiệp vụ '{r.TargetProperty}' đã được liên kết với cột khác trong Schema."); }var f=new ExcelSchemaField(r.FieldKey.Trim(),r.DataType.Trim(),r.IsRequired,r.SourceColumnIndex,r.HeaderName,r.Format,r.ResourceKey,r.TargetProperty,r.DisplayOrder,r.AllowEmpty,r.MaxLength,r.DefaultValue,r.ValidationRule);var old=await _db.Database.SqlQueryRaw<int?>("SELECT TOP 1 Id AS Value FROM dbo.F03ExcelSchemaFields WHERE SchemaVersionId={0} AND FieldKey={1}",vid,f.FieldKey).SingleOrDefaultAsync(ct);if(old.HasValue)await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.F03ExcelSchemaFields SET DataType={f.DataType},SourceColumnIndex={f.SourceColumnIndex},HeaderName={f.HeaderName},ResourceKey={f.ResourceKey},TargetProperty={f.TargetProperty},Format={f.Format},ValidationRule={f.ValidationRule},DefaultValue={f.DefaultValue},IsRequired={f.Required},AllowEmpty={f.AllowEmpty},MaxLength={f.MaxLength},DisplayOrder={f.DisplayOrder} WHERE Id={old.Value}",ct);else await InsertField(vid,f,ct);var d=await _excel.GetSchemaDefinitionAsync(r.SchemaId,ct)??throw new InvalidOperationException("Không tải được schema.");return ServiceResult<ExcelSchemaFieldDto>.Ok(Map(d.Fields.First(x=>x.FieldKey.Equals(f.FieldKey,StringComparison.OrdinalIgnoreCase))),"Đã lưu field.");}catch(Exception e)when(e is UnauthorizedAccessException or ArgumentException or InvalidOperationException or KeyNotFoundException){return ServiceResult<ExcelSchemaFieldDto>.Fail(e.Message);}}
    public async Task<ServiceResult<ExcelWorkbookDto>> InspectExcelAsync(int dept,string file,Stream content,CancellationToken ct=default){try{var u=User();await Scope(u,Dept(dept,u.DeptCode),ct);var x=await _excel.InspectAsync(content,file,ct);return ServiceResult<ExcelWorkbookDto>.Ok(new(){FileName=x.FileName,Sheets=x.Sheets.Select(s=>new ExcelSheetDto{Index=s.Index,Name=s.Name,RowCount=s.LastColumnIndex < s.FirstColumnIndex ? 0 : Math.Max(0,s.LastRowIndex+1),ColumnCount=s.LastColumnIndex < s.FirstColumnIndex ? 0 : Math.Max(0,s.LastColumnIndex+1),HasData=s.LastRowIndex>=s.FirstRowIndex&&s.LastColumnIndex>=s.FirstColumnIndex}).ToList()});}catch(Exception e)when(e is UnauthorizedAccessException or ArgumentException or InvalidOperationException){return ServiceResult<ExcelWorkbookDto>.Fail(e.Message);}}
    public async Task<ServiceResult<ExcelSchemaFromExcelDto>> PreviewSchemaFromExcelSheetAsync(
        int departmentCode, string fileName, Stream content, int sheetIndex, CancellationToken ct = default)
    {
        try
        {
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            await using var gridStream = new MemoryStream(bytes);
            var grid = await _excel.ReadGridAsync(gridStream, fileName, sheetIndex, 1000, ct);
            var header = grid.Rows.FirstOrDefault(row => row.Cells.Any(cell => !string.IsNullOrWhiteSpace(cell)));
            if (header is null)
                return ServiceResult<ExcelSchemaFromExcelDto>.Fail("Không tìm thấy dòng tiêu đề trong sheet Excel.");

            await using var previewStream = new MemoryStream(bytes);
            return await PreviewRange(departmentCode, fileName, previewStream,
                new ExcelRangeRequest { SheetIndex = sheetIndex, HeaderRowIndex = header.RowIndex, DataStartRowIndex = header.RowIndex + 1 },
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return ServiceResult<ExcelSchemaFromExcelDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<ExcelSchemaDto>> CreateSchemaFromExcelSheetAsync(
        int departmentCode, string fileName, Stream content, int sheetIndex, string? schemaName, CancellationToken ct = default)
    {
        try
        {
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            await using var gridStream = new MemoryStream(bytes);
            var grid = await _excel.ReadGridAsync(gridStream, fileName, sheetIndex, 1000, ct);
            var header = grid.Rows.FirstOrDefault(row => row.Cells.Any(cell => !string.IsNullOrWhiteSpace(cell)));
            if (header is null)
                return ServiceResult<ExcelSchemaDto>.Fail("Không tìm thấy dòng tiêu đề trong sheet Excel.");

            await using var previewStream = new MemoryStream(bytes);
            return await CreateRange(departmentCode, fileName, previewStream,
                new ExcelRangeRequest { SheetIndex = sheetIndex, HeaderRowIndex = header.RowIndex, DataStartRowIndex = header.RowIndex + 1 },
                schemaName, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return ServiceResult<ExcelSchemaDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<ExcelGridDto>> GetExcelGridAsync(
        int dept,
        string file,
        Stream content,
        int sheet,
        int maxRows = 200,
        CancellationToken ct = default)
    {
        try
        {
            var user = User();
            await Scope(user, Dept(dept, user.DeptCode), ct);
            var grid = await _excel.ReadGridAsync(content, file, sheet, Math.Clamp(maxRows, 1, 1000), ct);
            return ServiceResult<ExcelGridDto>.Ok(new ExcelGridDto
            {
                FileName = Path.GetFileName(file),
                SheetIndex = grid.SheetIndex,
                TotalRowCount = grid.TotalRowCount,
                ColumnCount = grid.ColumnCount,
                Rows = grid.Rows.Select(row => new ExcelGridRowDto
                {
                    RowIndex = row.RowIndex,
                    Cells = row.Cells.ToList()
                }).ToList()
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ServiceResult<ExcelGridDto>.Fail(ex.Message);
        }
    }
    public Task<ServiceResult<ExcelSchemaFromExcelDto>> PreviewSchemaFromExcelRangeAsync(int d,string f,Stream c,ExcelRangeRequest r,CancellationToken ct=default)=>PreviewRange(d,f,c,r,ct);
    public Task<ServiceResult<ExcelSchemaDto>> CreateSchemaFromExcelRangeAsync(int d,string f,Stream c,ExcelRangeRequest r,string? n,CancellationToken ct=default)=>CreateRange(d,f,c,r,n,ct);
    private async Task<ServiceResult<ExcelSchemaFromExcelDto>> PreviewRange(
        int departmentCode,
        string fileName,
        Stream content,
        ExcelRangeRequest request,
        CancellationToken ct)
    {
        try
        {
            var user = User();
            await Scope(user, Dept(departmentCode, user.DeptCode), ct);
            if (request.SheetIndex < 0)
                return ServiceResult<ExcelSchemaFromExcelDto>.Fail("Sheet Excel không hợp lệ.");

            var requiredRows = request.DataEndRowIndex.HasValue
                ? request.DataEndRowIndex.Value + 1
                : Math.Max(200, request.HeaderRowIndex + 2);
            var maxRows = Math.Clamp(requiredRows, 1, 10000);
            var grid = await _excel.ReadGridAsync(content, fileName, request.SheetIndex, maxRows, ct);
            if (grid.TotalRowCount == 0 || grid.ColumnCount == 0)
                return ServiceResult<ExcelSchemaFromExcelDto>.Fail("Sheet Excel không có dữ liệu.");

            if (request.HeaderRowIndex < 0 || request.HeaderRowIndex >= grid.TotalRowCount)
                return ServiceResult<ExcelSchemaFromExcelDto>.Fail("Dòng tiêu đề nằm ngoài phạm vi sheet Excel.");

            var header = grid.Rows.FirstOrDefault(row => row.RowIndex == request.HeaderRowIndex);
            if (header is null)
                return ServiceResult<ExcelSchemaFromExcelDto>.Fail("Không đọc được dòng tiêu đề đã chọn.");

            var dataStart = request.DataStartRowIndex ?? request.HeaderRowIndex + 1;
            var dataEnd = request.DataEndRowIndex ?? grid.TotalRowCount - 1;
            if (dataStart <= request.HeaderRowIndex)
                return ServiceResult<ExcelSchemaFromExcelDto>.Fail("Dòng dữ liệu bắt đầu phải nằm sau dòng tiêu đề.");
            if (dataStart < 0 || dataStart >= grid.TotalRowCount || dataEnd < dataStart || dataEnd >= grid.TotalRowCount)
                return ServiceResult<ExcelSchemaFromExcelDto>.Fail("Phạm vi dòng dữ liệu không hợp lệ.");

            var cols = (request.SelectedColumnIndexes
                    ?? Enumerable.Range(0, Math.Min(header.Cells.Count, grid.ColumnCount))
                        .Where(index => !string.IsNullOrWhiteSpace(header.Cells[index]))
                        .ToList())
                .Where(index => index >= 0 && index < grid.ColumnCount)
                .Distinct()
                .OrderBy(index => index)
                .ToList();
            if (cols.Count == 0)
                return ServiceResult<ExcelSchemaFromExcelDto>.Fail("Hãy chọn ít nhất một cột có tiêu đề.");

            var inputColumns = cols.Select(index =>
            {
                var headerName = header.Cells[index]?.Trim();
                if (string.IsNullOrWhiteSpace(headerName))
                    headerName = $"Column_{index + 1}";
                var samples = grid.Rows
                    .Where(row => row.RowIndex >= dataStart && row.RowIndex <= dataEnd)
                    .Select(row => index < row.Cells.Count ? row.Cells[index] : null)
                    .ToList();
                return new ExcelSchemaInference.InputColumn(headerName, samples);
            }).ToList();

            var inferred = ExcelSchemaInference.Infer(inputColumns);
            var sampleRowCount = grid.Rows
                .Where(row => row.RowIndex >= dataStart && row.RowIndex <= dataEnd)
                .Count(row => cols.Any(index => index < row.Cells.Count && !string.IsNullOrWhiteSpace(row.Cells[index])));

            return ServiceResult<ExcelSchemaFromExcelDto>.Ok(new ExcelSchemaFromExcelDto
            {
                FileName = Path.GetFileName(fileName),
                SheetName = grid.SheetName,
                SuggestedSchemaName = Path.GetFileNameWithoutExtension(fileName),
                ColumnCount = inferred.Count,
                SampleRowCount = sampleRowCount,
                Fields = inferred.Select((field, index) => new ExcelSchemaFieldDto
                {
                    FieldKey = field.FieldKey,
                    FieldLabel = field.FieldLabel,
                    DataType = field.DataType,
                    IsRequired = field.IsRequired,
                    AllowEmpty = !field.IsRequired,
                    SourceColumnIndex = cols[index],
                    HeaderName = inputColumns[index].Header,
                    TargetProperty = MapEquipmentTargetProperty(inputColumns[index].Header),
                    MaxLength = field.MaxLength,
                    DisplayOrder = index + 1
                }).ToList()
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ServiceResult<ExcelSchemaFromExcelDto>.Fail(ex.Message);
        }
    }

    private async Task<ServiceResult<ExcelSchemaDto>> CreateRange(
        int departmentCode,
        string fileName,
        Stream content,
        ExcelRangeRequest request,
        string? schemaName,
        CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var preview = await PreviewRange(departmentCode, fileName, buffer, request, ct);
        if (!preview.IsSuccess || preview.Data is null)
            return ServiceResult<ExcelSchemaDto>.Fail(preview.Message ?? "Không thể suy luận schema.");

        var user = User();
        var department = Dept(departmentCode, user.DeptCode);
        var fields = preview.Data.Fields.Select(field => new ExcelSchemaField(
            field.FieldKey,
            field.DataType,
            field.IsRequired,
            field.SourceColumnIndex,
            field.HeaderName,
            field.Format,
            field.ResourceKey,
            field.TargetProperty,
            field.DisplayOrder,
            field.AllowEmpty,
            field.MaxLength,
            field.DefaultValue,
            field.ValidationRule)).ToList();

        var selectedColumns = fields.Select(field => field.SourceColumnIndex).ToList();
        var created = await _excel.CreateDraftSchemaAsync(new ExcelSchemaCreateRequest(
            ModuleCode,
            EntityCode,
            $"{department}:{Guid.NewGuid():N}",
            await OwnedSchemaName(string.IsNullOrWhiteSpace(schemaName) ? preview.Data.SuggestedSchemaName : schemaName.Trim(), user.UserId, ct),
            request.SheetIndex,
            preview.Data.SheetName,
            request.HeaderRowIndex,
            request.DataStartRowIndex ?? request.HeaderRowIndex + 1,
            request.DataEndRowIndex,
            selectedColumns,
            fields), Path.GetFileName(fileName), ct);

        await OwnerSet(created.Id, user.UserId, ct);
        return ServiceResult<ExcelSchemaDto>.Ok(
            (await GetSchemaAsync(created.Id, ct))!,
            "Đã tạo schema từ Excel.");
    }

    public async Task<ExcelSchemaFromExcelDto> PreviewSchemaFromExcelAsync(int departmentCode, string fileName, Stream content, CancellationToken ct = default)
    {
        var result = await PreviewSchemaFromExcelSheetAsync(departmentCode, fileName, content, 0, ct);
        if (!result.IsSuccess || result.Data is null)
            throw new InvalidOperationException(result.Message ?? "Không thể suy luận schema từ Excel.");
        return result.Data;
    }

    public async Task<ExcelSchemaDto> CreateSchemaFromExcelAsync(int departmentCode, string fileName, Stream content, string? schemaName, CancellationToken ct = default)
    {
        var result = await CreateSchemaFromExcelSheetAsync(departmentCode, fileName, content, 0, schemaName, ct);
        if (!result.IsSuccess || result.Data is null)
            throw new InvalidOperationException(result.Message ?? "Không thể tạo schema từ Excel.");
        return result.Data;
    }

    public async Task<ServiceResult<ExcelImportBatchDto>> StageExcelSheetAsync(int dept,int? schemaId,string file,Stream content,int sheetIndex,bool assignToEmployee=false,CancellationToken ct=default)
    {
        var u = User();
        dept = Dept(dept, u.DeptCode);
        await Scope(u, dept, ct);

        var sid = schemaId.GetValueOrDefault();
        if (sid <= 0)
        {
            foreach (var candidate in await _excel.GetSchemasAsync(ModuleCode, EntityCode, false, ct))
            {
                if (candidate.Status != ExcelSchemaStatus.Active) continue;
                var candidateMeta = await Meta(candidate.Id, ct);
                if (candidateMeta.DepartmentCode == dept)
                {
                    sid = candidate.Id;
                    break;
                }
            }
        }

        if (sid <= 0)
            return ServiceResult<ExcelImportBatchDto>.Fail("Phòng ban chưa có schema Excel ở trạng thái Active.");

        var metadata = await Meta(sid, ct);
        await Scope(u, metadata.DepartmentCode, ct);
        if (metadata.DepartmentCode != dept)
            return ServiceResult<ExcelImportBatchDto>.Fail("Schema Excel không thuộc phòng ban đang import.");

        var summary = await _excel.GetSchemaAsync(sid, ct);
        if (summary is null || summary.Status != ExcelSchemaStatus.Active)
            return ServiceResult<ExcelImportBatchDto>.Fail("Chỉ được import bằng schema đang Active.");

        var d = await _excel.GetSchemaDefinitionAsync(sid, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy cấu hình schema.");

        // The schema is the single source of truth for business mapping. Do not silently
        // infer a user/department mapping from column names at import time.
        var mappedTargets = d.Fields
            .Where(field => !string.IsNullOrWhiteSpace(field.TargetProperty))
            .GroupBy(field => field.TargetProperty!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (mappedTargets.Count > 0)
            return ServiceResult<ExcelImportBatchDto>.Fail(
                $"Schema có nhiều cột cùng ánh xạ vào: {string.Join(", ", mappedTargets)}. Mỗi trường nghiệp vụ chỉ được ánh xạ từ một cột.");

        var missingTargets = new List<string>();
        if (!d.Fields.Any(field => string.Equals(field.TargetProperty, "EquipmentCode", StringComparison.OrdinalIgnoreCase)))
            missingTargets.Add("Mã thiết bị (EquipmentCode)");
        if (!d.Fields.Any(field => string.Equals(field.TargetProperty, "EquipmentName", StringComparison.OrdinalIgnoreCase)))
            missingTargets.Add("Tên thiết bị (EquipmentName)");
        if (!d.Fields.Any(field => string.Equals(field.TargetProperty, "OperatingResponsibleDeptCode", StringComparison.OrdinalIgnoreCase)))
            missingTargets.Add("Phòng ban sử dụng (OperatingResponsibleDeptCode)");
        if (missingTargets.Count > 0)
            return ServiceResult<ExcelImportBatchDto>.Fail(
                $"Schema chưa cấu hình liên kết dữ liệu bắt buộc: {string.Join(", ", missingTargets)}. Hãy sửa Schema và tạo phiên bản Active mới.");

        var r = await _excel.ImportAsync(content, file, d, ct);await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.F03ExcelImportBatches(SchemaId,SchemaVersionId,ModuleCode,EntityCode,FileName,Status,TotalRows,ValidRows,InvalidRows,ImportedRows,FailedRows,CreatedBy,CreatedAt) VALUES({sid},(SELECT CurrentVersionId FROM dbo.F03ExcelSchemas WHERE Id={sid}),{ModuleCode},{EntityCode},{file},40,{r.TotalRows},{r.ValidRows},{r.InvalidRows},0,0,{u.UserId.ToString()},SYSUTCDATETIME())",ct);var bid=await _db.Database.SqlQueryRaw<long>("SELECT TOP 1 Id AS Value FROM dbo.F03ExcelImportBatches WHERE SchemaId={0} AND FileName={1} ORDER BY Id DESC",sid,file).SingleAsync(ct);var activeDepartments = await _departmentLookup.GetActiveDepartmentsAsync(ct);
        var departmentCodes = activeDepartments.Select(x => x.DeptCode).ToHashSet();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in r.Rows)
        {
            var normalized = d.Fields.ToDictionary(field => field.FieldKey, field => row.Cells.TryGetValue(field.SourceColumnIndex, out var value) ? value : null);
            // Persist a canonical business-property view alongside the original schema keys.
            // This lets the approval/apply stage consume exactly the same mappings used by validation.
            foreach (var mappedField in d.Fields.Where(field => !string.IsNullOrWhiteSpace(field.TargetProperty)))
            {
                if (normalized.TryGetValue(mappedField.FieldKey, out var mappedValue))
                    normalized[mappedField.TargetProperty!.Trim()] = mappedValue;
            }
            // Technician is a role, not the end user. If no explicit column/value is supplied,
            // retain the authenticated operator's employee code as the default for approval.
            if (string.IsNullOrWhiteSpace(normalized.GetValueOrDefault("ManagedByEmployeeCode")))
                normalized["ManagedByEmployeeCode"] = u.EmployeeCode;
            var errors = r.Errors.Where(error => error.RowNumber == row.RowIndex).ToList();
            var domainErrors = new List<(string Field, string Message, string? Raw)>();
            string? Value(string property) => GetEquipmentFieldValue(normalized, d.Fields, property)?.Trim();
            var equipmentCode = Value("EquipmentCode");
            if (string.IsNullOrWhiteSpace(equipmentCode))
                domainErrors.Add(("EquipmentCode", "Thiếu mã thiết bị.", equipmentCode));
            else if (!seenCodes.Add(equipmentCode))
                domainErrors.Add(("EquipmentCode", $"Mã thiết bị '{equipmentCode}' bị lặp trong file Excel.", equipmentCode));
            else
            {
                // Existing codes are change candidates, not invalid rows. Persist the
                // resolved asset identity in the staged snapshot; commit must re-check it
                // under transaction before creating an approved change request.
                var existingAsset = await _db.Database.SqlQueryRaw<ExistingAssetRow>(
                    "SELECT TOP (1) Id, EquipmentCode FROM dbo.F03EquipmentAssets WHERE EquipmentCode = {0} AND IsActive = 1 ORDER BY Id",
                    equipmentCode).SingleOrDefaultAsync(ct);
                if (existingAsset is not null)
                {
                    normalized["ImportAction"] = "AssetChange";
                    normalized["ExistingAssetId"] = existingAsset.Id.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    normalized["ImportAction"] = "Registration";
                    normalized["ExistingAssetId"] = null;
                }
            }

            var departmentText = Value("OperatingResponsibleDeptCode") ?? Value("SourceDepartment") ?? Value("DeptCode") ?? Value("DepartmentCode") ?? Value("Department");
            if (string.IsNullOrWhiteSpace(departmentText))
                domainErrors.Add(("DepartmentCode", "Thiếu phòng ban của thiết bị; không thể xác định phạm vi lookup.", departmentText));
            else
            {
                var department = activeDepartments.FirstOrDefault(item =>
                    string.Equals(item.DeptCode.ToString(CultureInfo.InvariantCulture), departmentText, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.DeptName?.Trim(), departmentText, StringComparison.OrdinalIgnoreCase));
                if (department is null)
                    domainErrors.Add(("DepartmentCode", $"Phòng ban '{departmentText}' không tồn tại hoặc không còn hoạt động.", departmentText));
                // SourceDepartment is the operating department selected by schema mapping.
                // It is independent from the department that owns the schema/import workspace.
            }

            // Technician and equipment user are different roles; validate them independently
            // against the same HRM-synchronized employee catalog.
            // If no technician column is mapped (or the cell is blank), default to the operator.
            var technicianText = Value("ManagedByEmployeeCode") ?? u.EmployeeCode;
            if (!string.IsNullOrWhiteSpace(technicianText))
            {
                var technician = await _db.Employees.AsNoTracking()
                    .Where(item => item.IsActive == true && item.EndWorkingDate == null)
                    .Where(item => item.EmployeeCode == technicianText || item.EmployeeName == technicianText)
                    .Select(item => new { item.EmployeeCode, item.EmployeeName, item.DeptCode })
                    .FirstOrDefaultAsync(ct);
                if (technician is null)
                    domainErrors.Add(("ManagedByEmployeeCode", $"Kỹ thuật viên '{technicianText}' không tồn tại hoặc đã nghỉ việc.", technicianText));
                // Technicians can manage assets across departments; validate identity/activity only.
            }

            var employeeText = Value("OperatingResponsibleEmployeeCode");
            if (!string.IsNullOrWhiteSpace(employeeText))
            {
                var employeeQuery = _db.Employees.AsNoTracking()
                    .Where(item => item.IsActive == true && item.EndWorkingDate == null)
                    .Where(item => item.EmployeeCode == employeeText || item.EmployeeName == employeeText);
                // Resolve the employee against the device's selected department first,
                // so duplicate names in other departments cannot bind to the wrong person.
                var selectedDepartment = string.IsNullOrWhiteSpace(departmentText) ? null : activeDepartments.FirstOrDefault(item =>
                    string.Equals(item.DeptCode.ToString(CultureInfo.InvariantCulture), departmentText, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.DeptName?.Trim(), departmentText, StringComparison.OrdinalIgnoreCase));
                if (selectedDepartment is not null)
                    employeeQuery = employeeQuery.Where(item => item.DeptCode == selectedDepartment.DeptCode);
                var employee = await employeeQuery
                    .Select(item => new { item.EmployeeCode, item.EmployeeName, item.DeptCode })
                    .FirstOrDefaultAsync(ct);
                if (employee is null)
                    domainErrors.Add(("OperatingResponsibleEmployeeCode", $"Người sử dụng '{employeeText}' không tồn tại hoặc không thuộc phòng ban sử dụng đã chọn.", employeeText));
            }

            foreach (var field in d.Fields.Where(field => field.Required && !field.AllowEmpty))
            {
                if (!normalized.TryGetValue(field.FieldKey, out var fieldValue) || string.IsNullOrWhiteSpace(fieldValue))
                    domainErrors.Add((field.FieldKey, $"Trường bắt buộc '{field.HeaderName ?? field.FieldKey}' chưa có dữ liệu.", fieldValue));
            }

            await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.F03ExcelImportRows(BatchId,RowNumber,Status,RawDataJson,NormalizedDataJson,ErrorCount) VALUES({bid},{row.RowIndex},{(errors.Count + domainErrors.Count > 0 ? 20 : 30)},{JsonSerializer.Serialize(row.Cells)},{JsonSerializer.Serialize(normalized)},{errors.Count + domainErrors.Count})",ct);
            foreach (var error in errors)
                await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.F03ExcelImportErrors(BatchId,RowNumber,ColumnIndex,FieldKey,ErrorCode,Severity,ErrorMessage,RawValue,CreatedAt) VALUES({bid},{error.RowNumber},{error.ColumnIndex},{error.FieldKey},{error.Code},{(int)error.Severity},{error.Message},{error.RawValue},SYSUTCDATETIME())",ct);
            foreach (var error in domainErrors)
                await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.F03ExcelImportErrors(BatchId,RowNumber,ColumnIndex,FieldKey,ErrorCode,Severity,ErrorMessage,RawValue,CreatedAt) VALUES({bid},{row.RowIndex},NULL,{error.Field},N'EQUIPMENT_LOOKUP',2,{error.Message},{error.Raw},SYSUTCDATETIME())",ct);
        }
        // Keep batch counts consistent with domain validation errors, not just Excel parser errors.
        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.F03ExcelImportBatches SET ValidRows=(SELECT COUNT(*) FROM dbo.F03ExcelImportRows WHERE BatchId={bid} AND Status=30),InvalidRows=(SELECT COUNT(*) FROM dbo.F03ExcelImportRows WHERE BatchId={bid} AND Status=20) WHERE Id={bid}",ct);
        return ServiceResult<ExcelImportBatchDto>.Ok((await GetBatchAsync(bid,ct))!,"Đã staging file Excel và kiểm tra lookup thiết bị, phòng ban, nhân viên.");}
    public async Task<List<ExcelImportBatchDto>> GetMyBatchesAsync(CancellationToken ct = default)
    {
        var user = User();
        // F03ExcelImportBatches is the single parent record. Its existing staged rows
        // and F03EquipmentImportRequestLinks are the children; do not create a parallel group table.
        var candidates = await _db.Database.SqlQueryRaw<Batch>(
            @"SELECT TOP (200) Id,SchemaId,ModuleCode,EntityCode,FileName,Status,TotalRows,ValidRows,InvalidRows,ImportedRows
              FROM dbo.F03ExcelImportBatches ORDER BY Id DESC").ToListAsync(ct);
        var result = new List<ExcelImportBatchDto>();
        foreach (var candidate in candidates)
        {
            var metadata = await Meta(candidate.SchemaId, ct);
            if (!user.IsAdmin && metadata.CreatedBy != user.UserId) continue;
            await Scope(user, metadata.DepartmentCode, ct);
            var batch = await GetBatchAsync(candidate.Id, ct);
            if (batch != null) result.Add(batch);
        }
        return result;
    }

    public async Task<ExcelImportBatchDto?> GetBatchAsync(long id, CancellationToken ct = default)
    {
        var user = User();
        var batch = await _db.Database.SqlQueryRaw<Batch>(
            "SELECT Id,SchemaId,ModuleCode,EntityCode,FileName,Status,TotalRows,ValidRows,InvalidRows,ImportedRows FROM dbo.F03ExcelImportBatches WHERE Id={0}",
            id).SingleOrDefaultAsync(ct);
        if (batch is null) return null;

        var metadata = await Meta(batch.SchemaId, ct);
        await Scope(user, metadata.DepartmentCode, ct);

        // Return row-level validation details with the batch. Previously the DTO exposed
        // Rows but this method never populated it, so users could not correct bad rows
        // before submitting the inventory for approval.
        var rows = await _db.Database.SqlQueryRaw<ImportRow>(
            @"SELECT r.Id, r.RowNumber, r.Status,
                     COALESCE(JSON_VALUE(r.NormalizedDataJson, '$.ImportAction'), N'') AS ActionKind,
                     link.RequestId AS RequestId, req.AssetId AS AssetId,
                     req.RequestStatus AS RequestStatus,
                     r.NormalizedDataJson AS NormalizedDataJson,
                     COALESCE(
                       STUFF((
                         SELECT N'; ' + CONVERT(nvarchar(max), e.ErrorMessage)
                         FROM dbo.F03ExcelImportErrors e
                         WHERE e.BatchId = r.BatchId AND e.RowNumber = r.RowNumber
                         ORDER BY e.Id
                         FOR XML PATH(''), TYPE
                       ).value('.', 'nvarchar(max)'), 1, 2, N''),
                       N'') AS ErrorMessage
              FROM dbo.F03ExcelImportRows r
              LEFT JOIN dbo.F03EquipmentImportRequestLinks link ON link.BatchId=r.BatchId AND link.ImportRowId=r.Id
              LEFT JOIN dbo.F03EquipmentRequests req ON req.Id=link.RequestId
              WHERE r.BatchId = {0}
              ORDER BY r.RowNumber", id).ToListAsync(ct);

        return new ExcelImportBatchDto
        {
            Id = batch.Id,
            SchemaId = batch.SchemaId,
            ModuleCode = batch.ModuleCode,
            EntityCode = batch.EntityCode,
            FileName = batch.FileName,
            Status = ((ExcelImportBatchStatus)batch.Status).ToString(),
            TotalRows = batch.TotalRows,
            ValidRows = batch.ValidRows,
            InvalidRows = batch.InvalidRows,
            ImportedRows = batch.ImportedRows,
            Rows = rows.Select(row => new ExcelImportRowDto
            {
                Id = row.Id,
                RowNumber = row.RowNumber,
                Status = row.Status == 20 ? "Invalid" : row.Status == 30 ? "Valid" : row.Status.ToString(),
                ActionKind = row.ActionKind,
                ErrorMessage = row.ErrorMessage,
                RequestId = row.RequestId,
                AssetId = row.AssetId,
                RequestStatus = row.RequestStatus.HasValue ? ((ApprovalStatus)row.RequestStatus.Value).ToString() : null,
                IsLocked = row.RequestStatus.HasValue && row.RequestStatus.Value != (int)ApprovalStatus.Draft && row.RequestStatus.Value != (int)ApprovalStatus.NeedsRevision,
                Values = ParseStagedValues(row.NormalizedDataJson)
            }).ToList()
        };
    }
    public async Task<ExcelImportCommitResultDto> CommitAsync(long id, CancellationToken ct = default)
    {
        var user = User();
        var batch = await _db.Database.SqlQueryRaw<Batch>(
            "SELECT Id,SchemaId,ModuleCode,EntityCode,FileName,Status,TotalRows,ValidRows,InvalidRows,ImportedRows FROM dbo.F03ExcelImportBatches WHERE Id={0}", id)
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Không tìm thấy import batch.");
        var metadata = await Meta(batch.SchemaId, ct);
        await Scope(user, metadata.DepartmentCode, ct);

        var result = new ExcelImportCommitResultDto { BatchId = id };
        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            // Idempotency is enforced by a unique batch/row link, not only by UI state.
            await _db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.F03EquipmentImportRequestLinks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.F03EquipmentImportRequestLinks(
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_F03EquipmentImportRequestLinks PRIMARY KEY,
        BatchId bigint NOT NULL,
        ImportRowId bigint NOT NULL,
        RequestId int NOT NULL,
        ActionKind nvarchar(30) NOT NULL,
        CreatedBy int NOT NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_F03EquipmentImportRequestLinks_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_F03EquipmentImportRequestLinks_BatchRow UNIQUE(BatchId, ImportRowId),
        CONSTRAINT UQ_F03EquipmentImportRequestLinks_Request UNIQUE(RequestId)
    );
END", ct);

            var rows = await _db.Database.SqlQueryRaw<CommitSourceRow>(
                @"SELECT r.Id, r.RowNumber, r.Status, r.NormalizedDataJson,
                         COALESCE(JSON_VALUE(r.NormalizedDataJson,'$.ImportAction'),N'') AS ActionKind
                  FROM dbo.F03ExcelImportRows r WITH (UPDLOCK,HOLDLOCK)
                  WHERE r.BatchId={0} ORDER BY r.RowNumber", id).ToListAsync(ct);
            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var linked = await _db.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(1) AS Value FROM dbo.F03EquipmentImportRequestLinks WITH (UPDLOCK,HOLDLOCK) WHERE BatchId={0} AND ImportRowId={1}",
                    id, row.Id).SingleAsync(ct);
                if (linked > 0) { result.SkippedRows++; continue; }
                if (row.Status != 30) { result.SkippedRows++; result.Errors.Add($"Dòng {row.RowNumber}: dòng staging không hợp lệ, đã bỏ qua."); continue; }

                Dictionary<string, string?> values;
                try { values = JsonSerializer.Deserialize<Dictionary<string, string?>>(row.NormalizedDataJson ?? "{}") ?? new(StringComparer.OrdinalIgnoreCase); }
                catch (JsonException) { result.SkippedRows++; result.Errors.Add($"Dòng {row.RowNumber}: dữ liệu staging không đọc được."); continue; }
                string? V(string key) => values.TryGetValue(key, out var v) ? v?.Trim() : null;
                var code = V("EquipmentCode");
                var name = V("EquipmentName");
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                { result.SkippedRows++; result.Errors.Add($"Dòng {row.RowNumber}: thiếu mã hoặc tên thiết bị."); continue; }

                var deptText = V("OperatingResponsibleDeptCode") ?? V("SourceDepartment") ?? V("DeptCode") ?? V("DepartmentCode");
                var department = await _departmentLookup.GetActiveDepartmentsAsync(ct);
                var dept = department.FirstOrDefault(d => string.Equals(d.DeptCode.ToString(CultureInfo.InvariantCulture), deptText, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(d.DeptName?.Trim(), deptText, StringComparison.OrdinalIgnoreCase));
                if (dept is null)
                { result.SkippedRows++; result.Errors.Add($"Dòng {row.RowNumber}: phòng ban sử dụng '{deptText}' không tồn tại hoặc không hoạt động."); continue; }

                var employeeCode = V("OperatingResponsibleEmployeeCode");
                if (!string.IsNullOrWhiteSpace(employeeCode))
                {
                    var employee = await _db.Employees.AsNoTracking()
                        .Where(e => e.IsActive == true && e.EndWorkingDate == null &&
                            (e.EmployeeCode == employeeCode || e.EmployeeName == employeeCode))
                        .Select(e => new { e.EmployeeCode, e.DeptCode }).FirstOrDefaultAsync(ct);
                    if (employee is null) { result.SkippedRows++; result.Errors.Add($"Dòng {row.RowNumber}: người sử dụng '{employeeCode}' không tồn tại hoặc đã nghỉ việc."); continue; }
                    employeeCode = employee.EmployeeCode;
                    if (V("OperatingResponsibleDeptCode") is not null && employee.DeptCode != dept.DeptCode)
                    { result.SkippedRows++; result.Errors.Add($"Dòng {row.RowNumber}: phòng ban sử dụng không khớp phòng ban của nhân viên '{employeeCode}'."); continue; }
                }

                var technicianCode = V("ManagedByEmployeeCode") ?? user.EmployeeCode;
                if (string.IsNullOrWhiteSpace(technicianCode) ||
                    !await _db.Employees.AsNoTracking().AnyAsync(e => e.EmployeeCode == technicianCode && e.IsActive == true && e.EndWorkingDate == null, ct))
                { result.SkippedRows++; result.Errors.Add($"Dòng {row.RowNumber}: kỹ thuật viên quản lý không tồn tại hoặc đã nghỉ việc."); continue; }

                var price = ParseSchemaDecimal(V("PurchasePrice")) ?? 0m;
                var purchaseDate = ParseSchemaDate(V("PurchaseDate")) ?? DateTime.UtcNow.Date;
                var depreciationDate = ParseSchemaDate(V("ExpectedDepreciationDate")) ?? purchaseDate;
                if (price < 0 || depreciationDate < purchaseDate)
                { result.SkippedRows++; result.Errors.Add($"Dòng {row.RowNumber}: nguyên giá hoặc ngày khấu hao không hợp lệ."); continue; }

                var existing = await _db.Set<F03EquipmentAsset>().FromSqlInterpolated(
                    $"SELECT * FROM dbo.F03EquipmentAssets WITH (UPDLOCK,HOLDLOCK) WHERE EquipmentCode={code} AND IsActive=1")
                    .AsNoTracking().FirstOrDefaultAsync(ct);
                var action = existing is null ? "Registration" : "AssetChange";
                // Do not trust the staging-time classification. Re-evaluate code and identity under serializable transaction.
                var stagedAssetId = int.TryParse(V("ExistingAssetId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAssetId) ? parsedAssetId : (int?)null;
                if (action == "AssetChange" &&
                    (!string.Equals(row.ActionKind, "AssetChange", StringComparison.OrdinalIgnoreCase) || stagedAssetId != existing!.Id))
                { result.SkippedRows++; result.Errors.Add($"Dòng {row.RowNumber}: mã thiết bị hoặc danh tính tài sản đã thay đổi kể từ staging; hãy staging lại để tránh áp dụng snapshot cũ."); continue; }
                if (action == "Registration" &&
                    (string.Equals(row.ActionKind, "AssetChange", StringComparison.OrdinalIgnoreCase) || stagedAssetId.HasValue))
                { result.SkippedRows++; result.Errors.Add($"Dòng {row.RowNumber}: trạng thái thiết bị đã thay đổi kể từ staging; hãy staging lại."); continue; }

                var approver = await _db.Set<F03Approver>().AsNoTracking()
                    .Where(x => x.RequestType == RequestModule.Equipment && x.IsActive == true &&
                        (x.ApproveForDeptCode == dept.DeptCode || x.ApproveForDeptCode == ApproveForDept.All))
                    .OrderBy(x => x.Level).ThenBy(x => x.ApproverCode)
                    .Select(x => x.ApproverCode).FirstOrDefaultAsync(ct);
                if (string.IsNullOrWhiteSpace(approver))
                { result.SkippedRows++; result.Errors.Add($"Dòng {row.RowNumber}: chưa cấu hình người phê duyệt thiết bị cho phòng ban {dept.DeptCode}."); continue; }

                var request = new F03EquipmentRequest
                {
                    RequestKind = action == "Registration" ? FVN_REGISTER.Core.Enums.EquipmentRequestKind.Registration : FVN_REGISTER.Core.Enums.EquipmentRequestKind.AssetChange,
                    AssetId = existing?.Id,
                    RequestStatus = FVN_REGISTER.Core.Enums.ApprovalStatus.Draft,
                    EmployeeCode = user.EmployeeCode ?? technicianCode,
                    DeptCode = existing?.DeptCode ?? dept.DeptCode,
                    OperatorUserId = user.UserId,
                    SelectedApproverCode = approver,
                    QrToken = existing?.QrToken ?? Guid.NewGuid().ToString("N"),
                    EquipmentName = name,
                    Specification = V("Specification"),
                    SerialNumber = V("SerialNumber"),
                    AssetCode = V("AssetCode"),
                    PurchasePrice = price,
                    PurchaseDate = purchaseDate,
                    ExpectedDepreciationDate = depreciationDate,
                    Location = V("Location"),
                    Note = V("Note"),
                    ResponsibleEmployeeCode = V("ResponsibleEmployeeCode") ?? employeeCode,
                    CreatedBy = user.UserId
                };
                if (action == "AssetChange")
                {
                    var before = new { existing!.EquipmentCode, existing.EquipmentName, existing.AssetCode, existing.SerialNumber, existing.Specification, existing.PurchasePrice, existing.PurchaseDate, existing.ExpectedDepreciationDate, existing.Location, existing.Note, existing.CustomDataJson, existing.ManagedByEmployeeCode, existing.ResponsibleEmployeeCode, existing.ResponsibleApproverEmployeeCode, existing.OperatingResponsibleDeptCode, existing.OperatingResponsibleEmployeeCode };
                    var after = new
                    {
                        EquipmentCode = code, EquipmentName = name, AssetCode = request.AssetCode, SerialNumber = request.SerialNumber,
                        Specification = request.Specification, PurchasePrice = price, PurchaseDate = (DateTime?)purchaseDate,
                        ExpectedDepreciationDate = (DateTime?)depreciationDate, Location = request.Location, Note = request.Note,
                        CustomDataJson = existing.CustomDataJson, ManagedByEmployeeCode = technicianCode,
                        ResponsibleEmployeeCode = request.ResponsibleEmployeeCode,
                        ResponsibleApproverEmployeeCode = existing.ResponsibleApproverEmployeeCode,
                        OperatingResponsibleDeptCode = (int?)dept.DeptCode, OperatingResponsibleEmployeeCode = employeeCode
                    };
                    request.AssetBeforeSnapshotJson = JsonSerializer.Serialize(before);
                    request.AssetAfterSnapshotJson = JsonSerializer.Serialize(after);
                    request.AssetBeforeHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(before))));
                    request.AssetSnapshotCapturedAtUtc = DateTime.UtcNow;
                }
                _db.Set<F03EquipmentRequest>().Add(request);
                await _db.SaveChangesAsync(ct);
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO dbo.F03EquipmentImportRequestLinks(BatchId,ImportRowId,RequestId,ActionKind,CreatedBy) VALUES({id},{row.Id},{request.Id},{action},{user.UserId})", ct);
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE dbo.F03ExcelImportRows SET Status=40 WHERE Id={row.Id} AND BatchId={id}", ct);
                result.ImportedRows++;
            }
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.F03ExcelImportBatches SET ImportedRows=(SELECT COUNT(1) FROM dbo.F03EquipmentImportRequestLinks WHERE BatchId={id}) WHERE Id={id}", ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult<bool>> DeleteDraftSchemaAsync(int id,CancellationToken ct=default){try{var u=User();var m=await Meta(id,ct);if(m.CreatedBy!=u.UserId&&!u.IsAdmin)throw new UnauthorizedAccessException("Bạn không có quyền xóa schema.");await _excel.DeleteDraftSchemaAsync(id,ct);return ServiceResult<bool>.Ok(true,"Đã xóa schema.");}catch(Exception e)when(e is UnauthorizedAccessException or InvalidOperationException or KeyNotFoundException){return ServiceResult<bool>.Fail(e.Message);}}
    private static Dictionary<string,string?> ParseStagedValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new(StringComparer.OrdinalIgnoreCase);
            // Staging JSON may contain duplicate keys that differ only by casing
            // (for example EquipmentCode and equipmentcode). ToDictionary throws for
            // those payloads when using an ordinal-ignore-case comparer. Keep the first
            // key slot and deterministically let the last value win, matching the
            // case-insensitive lookup semantics used by the importer.
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    _ => property.Value.ToString()
                };
                values[property.Name] = value;
            }
            return values;
        }
        catch (JsonException) { return new(StringComparer.OrdinalIgnoreCase); }
    }

    private static string? MapEquipmentTargetProperty(string? header)
    {
        var normalized = NormalizeEquipmentHeader(header);
        if (normalized is "equipmentcode" or "assetcode" or "machietbi" or "mataisan" or "masothietbi" or "masanpham" or "code")
            return "EquipmentCode";
        if (normalized is "equipmentname" or "assetname" or "tenthietbi" or "tentaisan" or "tensanpham" or "name")
            return "EquipmentName";
        // Keep technician manager separate from the asset's end user.
        if (normalized is "managedbyemployeecode" or "technicianemployeecode" or "techniciancode" or "kythuatvienquanly" or "kythuatvien")
            return "ManagedByEmployeeCode";
        // Legacy Excel UserID is treated as an employee code only for the user/responsible role,
        // never as F03Users.Id and never as the technician manager.
        if (normalized is "userid" or "operatingresponsibleemployeecode" or "nguoisudung" or "nhanviensudung")
            return "OperatingResponsibleEmployeeCode";
        if (normalized is "employeecode" or "employeeid" or "manhanvien" or "nguoiphutrach" or "manhanvienphutrach" or "responsibleemployeecode")
            return "ResponsibleEmployeeCode";
        if (normalized is "sourcedepartment" or "operatingresponsibledeptcode" or "departmentuser")
            return "OperatingResponsibleDeptCode";
        return null;
    }

    private static bool IsEquipmentField(ExcelSchemaField field, string targetProperty)
    {
        if (targetProperty.Equals(field.TargetProperty, StringComparison.OrdinalIgnoreCase) ||
            targetProperty.Equals(field.FieldKey, StringComparison.OrdinalIgnoreCase))
            return true;

        var normalized = NormalizeEquipmentHeader(field.HeaderName ?? field.FieldKey);
        if (string.Equals(field.TargetProperty, targetProperty, StringComparison.OrdinalIgnoreCase))
            return true;

        return targetProperty switch
        {
            "EquipmentCode" => normalized is "equipmentcode" or "assetcode" or "machietbi" or "mataisan" or "masothietbi" or "masanpham" or "code",
            "EquipmentName" => normalized is "equipmentname" or "assetname" or "tenthietbi" or "tentaisan" or "tensanpham" or "name",
            "ManagedByEmployeeCode" => normalized is "managedbyemployeecode" or "technicianemployeecode" or "techniciancode" or "kythuatvienquanly" or "kythuatvien",
            "OperatingResponsibleDeptCode" => normalized is "sourcedepartment" or "operatingresponsibledeptcode" or "departmentuser",
            "OperatingResponsibleEmployeeCode" => normalized is "operatingresponsibleemployeecode" or "userid" or "nguoisudung" or "nhanviensudung",
            "ResponsibleEmployeeCode" => normalized is "responsibleemployeecode" or "employeecode" or "employeeid" or "manhanvien" or "nguoiphutrach" or "manhanvienphutrach",
            "AssetCode" => normalized is "assettag" or "assetcode" or "tagtaisan" or "mataisan",
            "SerialNumber" => normalized is "serialnumber" or "serialno" or "serial" or "soserial",
            "Specification" => normalized is "specification" or "thongso" or "cauhinh",
            "PurchasePrice" => normalized is "purchaseprice" or "nguyengia" or "giatrimua" or "price",
            "PurchaseDate" => normalized is "purchasedate" or "ngaymua",
            "ExpectedDepreciationDate" => normalized is "expecteddepreciationdate" or "ngaykhauhao" or "ngayhetkhauhao",
            "Location" => normalized is "location" or "vitri" or "noisudung",
            "Note" => normalized is "note" or "notes" or "ghichu",
            _ => false
        };
    }

    private static string? GetEquipmentFieldValue(
        IReadOnlyDictionary<string, string?> values,
        IReadOnlyList<ExcelSchemaField> fields,
        string targetProperty)
    {
        // Resolve values only through the explicit schema mapping. Header-name heuristics
        // are intentionally not used because UserId/SourceDepartment semantics are configured
        // by the schema owner and must not vary between UI and server.
        var field = fields.FirstOrDefault(candidate =>
            string.Equals(candidate.TargetProperty?.Trim(), targetProperty, StringComparison.OrdinalIgnoreCase));
        if (field is not null && values.TryGetValue(field.FieldKey, out var value))
            return value;

        return null;
    }

    private static DateTime? ParseSchemaDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsed) ||
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            return parsed;
        if (double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var oaDate))
        {
            try { return DateTime.FromOADate(oaDate); }
            catch (ArgumentException) { }
        }
        return null;
    }

    private static decimal? ParseSchemaDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed) ||
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            return parsed;
        return null;
    }

    private static string NormalizeEquipmentHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character)) normalized.Append(char.ToLowerInvariant(character));
        }
        return normalized.ToString().Normalize(NormalizationForm.FormC);
    }

    private async Task InsertField(int v,ExcelSchemaField f,CancellationToken ct)=>await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.F03ExcelSchemaFields(SchemaVersionId,FieldKey,DataType,SourceColumnIndex,HeaderName,ResourceKey,TargetProperty,Format,ValidationRule,DefaultValue,IsRequired,AllowEmpty,MaxLength,DisplayOrder) VALUES({v},{f.FieldKey},{f.DataType},{f.SourceColumnIndex},{f.HeaderName},{f.ResourceKey},{f.TargetProperty},{f.Format},{f.ValidationRule},{f.DefaultValue},{f.Required},{f.AllowEmpty},{f.MaxLength},{f.DisplayOrder})",ct);
    private async Task OwnerSet(int id,int user,CancellationToken ct)=>await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.F03ExcelSchemas SET CreatedBy={user.ToString()},UpdatedBy={user.ToString()} WHERE Id={id}",ct);
    private async Task<(int DepartmentCode,int CreatedBy,string SchemaName,int FieldCount,string? SourceFileName,int Status)> Meta(int id,CancellationToken ct){var r=await _db.Database.SqlQueryRaw<MetaRow>("SELECT s.SchemaCode AS [Code],s.SchemaName AS [Name],ISNULL(TRY_CONVERT(int,s.CreatedBy),0) AS [ById],(SELECT COUNT(*) FROM dbo.F03ExcelSchemaFields f WHERE f.SchemaVersionId=s.CurrentVersionId) AS [Fields],v.SourceFileName AS [File],s.Status AS [Status] FROM dbo.F03ExcelSchemas s LEFT JOIN dbo.F03ExcelSchemaVersions v ON v.Id=s.CurrentVersionId WHERE s.Id={0}",id).SingleOrDefaultAsync(ct)??throw new KeyNotFoundException("Không tìm thấy schema.");var p=r.Code.IndexOf(':');return(p>0&&int.TryParse(r.Code[..p],out var deptFromCode)?deptFromCode:0,r.ById,r.Name,r.Fields,r.File,r.Status);}
    private async Task Scope(UserIdentityDto u,int dept,CancellationToken ct){if(!u.IsAdmin&&u.DeptCode != dept&&!await _auth.HasAsync(u,SecurityFunctionCodes.EquipmentManage,ct))throw new UnauthorizedAccessException("Bạn không có quyền với bộ phận này.");}
    private async Task<string> Owner(int id,CancellationToken ct){if(id<=0)return string.Empty;var name=await _db.Database.SqlQueryRaw<string>("SELECT TOP (1) EmployeeName AS Value FROM dbo.F03Employees WHERE Id={0}",id).FirstOrDefaultAsync(ct);return string.IsNullOrWhiteSpace(name)?id.ToString():name;}
    private async Task<string> OwnedSchemaName(string name,int userId,CancellationToken ct){var clean=name.Trim();var ownerName=await Owner(userId,ct);if(string.IsNullOrWhiteSpace(ownerName))return clean;var prefix=ownerName.Trim()+"_";return clean.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)?clean:prefix+clean;}
    private UserIdentityDto User()=>_users.GetCurrentUser()??throw new UnauthorizedAccessException("Chưa đăng nhập.");
    private static int Dept(int? x,int? f)=>x??f??0;
    private static ExcelSchemaDto Map(int id,ExcelSchemaDefinition d,(int DepartmentCode,int CreatedBy,string SchemaName,int FieldCount,string? SourceFileName,int Status)m,UserIdentityDto u)=>new(){Id=id,ModuleCode=d.ModuleCode,EntityCode=d.EntityCode,SchemaCode=d.SchemaKey,SchemaName=m.SchemaName,Version=d.Version,Status=((ExcelSchemaStatus)m.Status).ToString(),IsActive=m.Status==(int)ExcelSchemaStatus.Active,DepartmentCode=m.DepartmentCode,CreatedByUserId=m.CreatedBy,SourceFileName=m.SourceFileName,CreatedFromExcel=!string.IsNullOrWhiteSpace(m.SourceFileName),IsOwner=m.CreatedBy==u.UserId||u.IsAdmin,CanEdit=m.CreatedBy==u.UserId||u.IsAdmin,CanCreateVersion=m.CreatedBy==u.UserId||u.IsAdmin,CanClone=true,CreatedByUserName="",SheetIndex=d.SheetIndex,SheetName=d.SheetName,HeaderRowIndex=d.HeaderRowIndex,DataStartRowIndex=d.DataStartRowIndex,DataEndRowIndex=d.DataEndRowIndex,Fields=d.Fields.Select(Map).ToList()};
    private static ExcelSchemaFieldDto Map(ExcelSchemaField f)=>new(){FieldKey=f.FieldKey,FieldLabel=f.HeaderName??f.FieldKey,DataType=f.DataType,IsRequired=f.Required,AllowEmpty=f.AllowEmpty,SourceColumnIndex=f.SourceColumnIndex,HeaderName=f.HeaderName,ResourceKey=f.ResourceKey,TargetProperty=f.TargetProperty,Format=f.Format,ValidationRule=f.ValidationRule,DefaultValue=f.DefaultValue,MaxLength=f.MaxLength,DisplayOrder=f.DisplayOrder};
    private sealed record CommitSourceRow(long Id,int RowNumber,int Status,string? NormalizedDataJson,string ActionKind); private sealed record ExistingAssetRow(int Id,string EquipmentCode); private sealed record MetaRow(string Code,string Name,int ById,int Fields,string? File,int Status); private sealed record Batch(long Id,int SchemaId,string ModuleCode,string EntityCode,string FileName,int Status,int TotalRows,int ValidRows,int InvalidRows,int ImportedRows); private sealed record Row(long Id,string? Data); private sealed record ImportRow(long Id,int RowNumber,int Status,string ActionKind,int? RequestId,int? AssetId,int? RequestStatus,string? NormalizedDataJson,string? ErrorMessage);
}