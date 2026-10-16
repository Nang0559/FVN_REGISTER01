using ClosedXML.Excel;
using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Companies;
using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Services.Common;
using FVN_REGISTER.Contract.Dtos.MasterData;
using FVN_REGISTER.Core.Entities.Common;
using FVN_REGISTER.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.Infrastructure.Services.Companies;

public sealed class CompanyHolidayManagementService : BaseService<CompanyHolidayManagementService>, ICompanyHolidayManagementService
{
    private readonly IUnitOfWork _uow;
    private readonly IAttendanceSymbolRuleService _symbolRules;
    private const string ReadOnlyMessage = "Ngày nghỉ được đồng bộ từ HRM (DM_Loainghile/tblNgayNghiLe). Hãy sửa trong HRM rồi bấm \"Đồng bộ từ HRM\".";
    private static readonly bool HolidaysAreReadOnly = true;

    public CompanyHolidayManagementService(
        IUnitOfWork uow,
        IAttendanceSymbolRuleService symbolRules,
        ILogger<CompanyHolidayManagementService> logger,
        IOptionsMonitor<AuthDebugOptions> options)
        : base(logger, options)
    {
        _uow = uow;
        _symbolRules = symbolRules;
    }

    public Task<List<CompanyHolidayDto>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_uow.Repository<F03CompanyHoliday>().Query().AsNoTracking().Where(x => x.IsActive == true && x.Source == "HRM").OrderByDescending(x => x.HolidayDate).Select(ToDto).ToList());

    public async Task<CompanyHolidayDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<F03CompanyHoliday>().GetByIdAsync(id, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<List<int>> GetWorkYearsAsync(CancellationToken ct = default)
        => await _uow.Repository<F03WorkYear>().Query().AsNoTracking().Where(x => x.IsActive == true).Select(x => x.WorkYear).Distinct().OrderByDescending(x => x).ToListAsync(ct);

    public async Task<ServiceResult<CompanyHolidayDto>> CreateAsync(CompanyHolidayDto model, int userId, CancellationToken ct = default)
    {
        if (HolidaysAreReadOnly) return ServiceResult<CompanyHolidayDto>.Fail(ReadOnlyMessage);
        try
        {
            if (model.HolidayType is < 1 or > 4) return ServiceResult<CompanyHolidayDto>.Fail("Loại ngày nghỉ không hợp lệ. Chỉ nhận 1-4.");
            if (string.IsNullOrWhiteSpace(model.Description)) return ServiceResult<CompanyHolidayDto>.Fail("Mô tả ngày nghỉ không được trống.");
            var repo = _uow.Repository<F03CompanyHoliday>();
            if (await repo.Query().AnyAsync(x => x.HolidayDate.Date == model.HolidayDate.Date, ct)) return ServiceResult<CompanyHolidayDto>.Fail("Ngày nghỉ này đã tồn tại.");
            var entity = new F03CompanyHoliday { HolidayDate=model.HolidayDate.Date, Description=model.Description.Trim(), Year=model.HolidayDate.Year, TinhPhep=model.IsPaidLeave, HolidayType=model.HolidayType, CreatedBy=userId, CreatedAt=DateTime.Now };
            await repo.AddAsync(entity, ct); await _uow.SaveChangesAsync(ct);
            return ServiceResult<CompanyHolidayDto>.Ok(ToDto(entity));
        }
        catch (Exception ex) { Logger.LogError(ex, "[HOLIDAY] Create error"); return ServiceResult<CompanyHolidayDto>.Fail("Lỗi hệ thống khi tạo ngày nghỉ."); }
    }

    public async Task<ServiceResult<CompanyHolidayDto>> UpdateAsync(CompanyHolidayDto model, int userId, CancellationToken ct = default)
    {
        if (HolidaysAreReadOnly) return ServiceResult<CompanyHolidayDto>.Fail(ReadOnlyMessage);
        try
        {
            if (model.HolidayType is < 1 or > 4) return ServiceResult<CompanyHolidayDto>.Fail("Loại ngày nghỉ không hợp lệ. Chỉ nhận 1-4.");
            if (string.IsNullOrWhiteSpace(model.Description)) return ServiceResult<CompanyHolidayDto>.Fail("Mô tả ngày nghỉ không được trống.");
            var repo = _uow.Repository<F03CompanyHoliday>();
            var entity = await repo.GetByIdAsync(model.Id, ct);
            if (entity is null) return ServiceResult<CompanyHolidayDto>.Fail("Không tìm thấy thông tin.");
            if (await repo.Query().AnyAsync(x => x.Id != model.Id && x.HolidayDate.Date == model.HolidayDate.Date, ct)) return ServiceResult<CompanyHolidayDto>.Fail("Ngày nghỉ này đã tồn tại.");
            entity.HolidayDate=model.HolidayDate.Date; entity.Description=model.Description.Trim(); entity.Year=model.HolidayDate.Year; entity.TinhPhep=model.IsPaidLeave; entity.HolidayType=model.HolidayType; entity.ModifiedBy=userId; entity.ModifiedAt=DateTime.Now;
            await _uow.SaveChangesAsync(ct); return ServiceResult<CompanyHolidayDto>.Ok(ToDto(entity));
        }
        catch (Exception ex) { Logger.LogError(ex, "[HOLIDAY] Update error: {Id}", model.Id); return ServiceResult<CompanyHolidayDto>.Fail("Lỗi cập nhật."); }
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (HolidaysAreReadOnly) return ServiceResult.Fail(ReadOnlyMessage);
        var repo=_uow.Repository<F03CompanyHoliday>(); var entity=await repo.GetByIdAsync(id,ct);
        if (entity is null) return ServiceResult.Fail("Không tìm thấy.");
        repo.Remove(entity); await _uow.SaveChangesAsync(ct); return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CreateSundaysAsync(int year, int userId, CancellationToken ct = default)
    {
        if (HolidaysAreReadOnly) return ServiceResult.Fail(ReadOnlyMessage);
        var repo=_uow.Repository<F03CompanyHoliday>(); var date=new DateTime(year,1,1);
        while(date.DayOfWeek!=DayOfWeek.Sunday) date=date.AddDays(1);
        var count=0;
        while(date.Year==year)
        {
            if(!await repo.Query().AnyAsync(x=>x.HolidayDate.Date==date.Date,ct))
            {
                await repo.AddAsync(new F03CompanyHoliday { HolidayDate=date, Description="Chủ nhật", Year=year, TinhPhep=false, HolidayType=1, CreatedBy=userId, CreatedAt=DateTime.Now },ct); count++;
            }
            date=date.AddDays(7);
        }
        if(count>0){await _uow.SaveChangesAsync(ct);return ServiceResult.Ok($"Đã thêm {count} ngày chủ nhật.");}
        return ServiceResult.Fail("Dữ liệu chủ nhật đã đầy đủ.");
    }

    public Task<ServiceResult<byte[]>> DownloadTemplateAsync(CancellationToken ct = default)
    {
        if (HolidaysAreReadOnly) return Task.FromResult(ServiceResult<byte[]>.Fail(ReadOnlyMessage));
        try
        {
            ct.ThrowIfCancellationRequested(); using var workbook=new XLWorkbook(); var ws=workbook.Worksheets.Add("CompanyHolidays");
            var headers=new[]{"HolidayDate","Description","Year","HolidayType","TinhPhep"};
            for(var i=0;i<headers.Length;i++) ws.Cell(1,i+1).Value=headers[i];
            ws.Cell(2,1).Value=DateTime.Today; ws.Cell(2,2).Value="Ví dụ: Tết Dương lịch"; ws.Cell(2,3).FormulaA1="=YEAR(A2)"; ws.Cell(2,4).Value=2; ws.Cell(2,5).Value=1;
            ws.Column(1).Style.DateFormat.Format="dd/MM/yyyy"; ws.Column(3).Style.NumberFormat.Format="0"; ws.Column(4).Style.NumberFormat.Format="0"; ws.Column(5).Style.NumberFormat.Format="0";
            var header=ws.Range(1,1,1,5); header.Style.Font.Bold=true; header.Style.Fill.BackgroundColor=XLColor.FromHtml("#E8EEF7"); header.Style.Alignment.Horizontal=XLAlignmentHorizontalValues.Center;
            ws.Range(1,1,200,5).Style.Border.OutsideBorder=XLBorderStyleValues.Thin; ws.Range(1,1,200,5).Style.Border.InsideBorder=XLBorderStyleValues.Thin;
            ws.Column(1).Width=16; ws.Column(2).Width=36; ws.Column(3).Width=12; ws.Column(4).Width=14; ws.Column(5).Width=12; ws.SheetView.FreezeRows(1);
            ws.Range("D2:D200").CreateDataValidation().WholeNumber.Between(1,4); ws.Range("E2:E200").CreateDataValidation().List("1,0",true);
            var guide=workbook.Worksheets.Add("Hướng dẫn"); guide.Cell(1,1).Value="Mẫu import ngày nghỉ"; guide.Cell(1,1).Style.Font.Bold=true;
            guide.Cell(3,1).Value="HolidayDate"; guide.Cell(3,2).Value="Ngày nghỉ, định dạng dd/MM/yyyy.";
            guide.Cell(4,1).Value="Description"; guide.Cell(4,2).Value="Mô tả ngày nghỉ, bắt buộc.";
            guide.Cell(5,1).Value="Year"; guide.Cell(5,2).Value="Phải khớp YEAR(HolidayDate).";
            guide.Cell(6,1).Value="HolidayType"; guide.Cell(6,2).Value="1 = Nghỉ công ty; 2 = Nghỉ lễ quốc gia; 3 = Nghỉ bù; 4 = Nghỉ khác.";
            guide.Cell(7,1).Value="TinhPhep"; guide.Cell(7,2).Value="1 = tính phép; 0 = không tính phép. Độc lập với HolidayType.";
            guide.Columns().AdjustToContents(); using var stream=new MemoryStream(); workbook.SaveAs(stream); return Task.FromResult(ServiceResult<byte[]>.Ok(stream.ToArray()));
        }
        catch(Exception ex){Logger.LogError(ex,"[HOLIDAY] Create Excel template error");return Task.FromResult(ServiceResult<byte[]>.Fail("Không tạo được mẫu Excel."));}
    }

    public async Task<ServiceResult<string>> ImportExcelAsync(Stream content,string fileName,int userId,CancellationToken ct=default)
    {
        if (HolidaysAreReadOnly) return ServiceResult<string>.Fail(ReadOnlyMessage);
        try
        {
            using var workbook=new XLWorkbook(content); var worksheet=workbook.Worksheets.FirstOrDefault(); if(worksheet is null)return ServiceResult<string>.Fail("File Excel không có worksheet.");
            var lastRow=worksheet.LastRowUsed()?.RowNumber()??0; if(lastRow<2)return ServiceResult<string>.Fail("File Excel không có dữ liệu.");
            var lastColumn=Math.Min(worksheet.LastColumnUsed()?.ColumnNumber()??0,10); var headers=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            for(var c=1;c<=lastColumn;c++){var h=worksheet.Cell(1,c).GetString().Trim();if(!string.IsNullOrWhiteSpace(h))headers[h]=c;}
            if(!headers.TryGetValue("HolidayDate",out var dateColumn)||!headers.TryGetValue("Description",out var descriptionColumn)||!headers.TryGetValue("HolidayType",out var typeColumn)||!headers.TryGetValue("TinhPhep",out var paidColumn))return ServiceResult<string>.Fail("Thiếu cột bắt buộc: HolidayDate, Description, HolidayType, TinhPhep.");
            headers.TryGetValue("Year",out var yearColumn);
            var repo=_uow.Repository<F03CompanyHoliday>(); var importedDates=new HashSet<DateTime>(); var added=0; var skipped=0;
            for(var row=2;row<=lastRow;row++)
            {
                ct.ThrowIfCancellationRequested(); if(worksheet.Cell(row,dateColumn).IsEmpty()&&worksheet.Cell(row,descriptionColumn).IsEmpty())continue;
                if(!worksheet.Cell(row,dateColumn).TryGetValue<DateTime>(out var date))return ServiceResult<string>.Fail($"Dòng {row}: HolidayDate không hợp lệ."); date=date.Date;
                if(yearColumn>0){if(!int.TryParse(worksheet.Cell(row,yearColumn).GetString().Trim(),out var parsedYear))return ServiceResult<string>.Fail($"Dòng {row}: Year không hợp lệ.");if(parsedYear!=date.Year)return ServiceResult<string>.Fail($"Dòng {row}: Year không khớp HolidayDate.");}
                var description=worksheet.Cell(row,descriptionColumn).GetString().Trim(); if(string.IsNullOrWhiteSpace(description))return ServiceResult<string>.Fail($"Dòng {row}: thiếu Description.");
                if(!int.TryParse(worksheet.Cell(row,typeColumn).GetString().Trim(),out var type)||type is<1 or>4)return ServiceResult<string>.Fail($"Dòng {row}: HolidayType chỉ nhận 1, 2, 3 hoặc 4.");
                if(!TryParseBool(worksheet.Cell(row,paidColumn).GetString().Trim(),out var paid))return ServiceResult<string>.Fail($"Dòng {row}: TinhPhep chỉ nhận 1/0 hoặc Có/Không.");
                if(!importedDates.Add(date)) { skipped++; continue; }
                if(await repo.Query().AnyAsync(x=>x.HolidayDate.Date==date,ct)) { skipped++; continue; }
                await repo.AddAsync(new F03CompanyHoliday
                {
                    HolidayDate = date,
                    Description = description,
                    Year = date.Year,
                    TinhPhep = paid,
                    HolidayType = (byte)type,
                    CreatedBy = userId,
                    CreatedAt = DateTime.Now
                }, ct);
            }
            await _uow.SaveChangesAsync(ct); return ServiceResult<string>.Ok($"Đã thêm {added} ngày nghỉ; bỏ qua {skipped} dòng trùng.");
        }
        catch(Exception ex){Logger.LogError(ex,"[HOLIDAY] Import Excel error: {FileName}",fileName);return ServiceResult<string>.Fail("Import Excel thất bại.");}
    }

    private static bool TryParseBool(string value,out bool result)
    {
        if(value.Equals("1",StringComparison.OrdinalIgnoreCase)||value.Equals("true",StringComparison.OrdinalIgnoreCase)||value.Equals("yes",StringComparison.OrdinalIgnoreCase)||value.Equals("có",StringComparison.OrdinalIgnoreCase)){result=true;return true;}
        if(value.Equals("0",StringComparison.OrdinalIgnoreCase)||value.Equals("false",StringComparison.OrdinalIgnoreCase)||value.Equals("no",StringComparison.OrdinalIgnoreCase)||value.Equals("không",StringComparison.OrdinalIgnoreCase)){result=false;return true;}
        result=false;return false;
    }

    private static CompanyHolidayDto ToDto(F03CompanyHoliday entity) => new()
    {
        Id = entity.Id,
        HolidayDate = entity.HolidayDate,
        Description = entity.Description,
        Year = entity.Year,
        IsPaidLeave = entity.TinhPhep,
        HolidayType = entity.HolidayType,
        DayType = entity.DayType,
        Source = entity.Source
    };

    public async Task<ServiceResult<string>> SyncFromHrmAsync(CancellationToken ct = default)
    {
        try
        {
            var rows = await _uow.SqlQueryRawAsync<HrmHolidaySyncRow>("EXEC dbo.usp_SyncHrmHolidays", ct);
            var r = rows.FirstOrDefault();
            try
            {
                // Holiday classification may have changed. Re-resolve DayType and
                // regenerate current symbols immediately instead of waiting for the
                // next scheduled attendance calculation.
                await _symbolRules.RefreshSymbolsAsync(ct);
            }
            catch (Exception refreshException)
            {
                Logger.LogError(refreshException, "[HOLIDAY] HRM sync succeeded but attendance symbol refresh failed.");
                return ServiceResult<string>.Ok(
                    "ok",
                    "Đã đồng bộ ngày nghỉ HRM nhưng chưa cập nhật được ký hiệu chấm công. Vui lòng chạy tính lại chấm công.");
            }

            return ServiceResult<string>.Ok(
                "ok",
                r is null
                    ? "Đã đồng bộ ngày nghỉ từ HRM."
                    : $"Đã đồng bộ từ HRM: {r.HolidayCount} ngày nghỉ (cập nhật {r.HolidaysUpserted}, xoá {r.HolidaysDeleted}), {r.TypesUpserted} loại ngày nghỉ.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[HOLIDAY] Sync from HRM failed");
            return ServiceResult<string>.Fail("Đồng bộ ngày nghỉ từ HRM thất bại.");
        }
    }

    private sealed class HrmHolidaySyncRow
    {
        public int TypesUpserted { get; set; }
        public int HolidaysUpserted { get; set; }
        public int HolidaysDeleted { get; set; }
        public int HolidayCount { get; set; }
    }
}