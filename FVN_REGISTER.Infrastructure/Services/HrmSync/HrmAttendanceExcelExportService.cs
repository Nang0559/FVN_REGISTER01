using FVN_REGISTER.Application.Interfaces.HrmSync;
using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Core.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System.Data;
using System.Globalization;

namespace FVN_REGISTER.Infrastructure.Services.HrmSync;

public sealed class HrmAttendanceExcelExportService : IHrmAttendanceExcelExportService
{
    private readonly IUnitOfWork _uow;
    private readonly IWebHostEnvironment _environment;

    public HrmAttendanceExcelExportService(IUnitOfWork uow, IWebHostEnvironment environment)
    {
        _uow = uow;
        _environment = environment;
    }

    public Task<ServiceResult<byte[]>> ExportAttendanceAsync(Guid batchId, CancellationToken ct = default)
        => ExportAsync(new[] { batchId }, false, null, ct);

    public Task<ServiceResult<byte[]>> ExportOtAsync(Guid batchId, CancellationToken ct = default)
        => ExportAsync(new[] { batchId }, true, null, ct);

    public Task<ServiceResult<byte[]>> ExportAttendanceByJobAsync(Guid jobId, CancellationToken ct = default)
        => ExportJobAsync(jobId, false, ct);

    public Task<ServiceResult<byte[]>> ExportOtByJobAsync(Guid jobId, CancellationToken ct = default)
        => ExportJobAsync(jobId, true, ct);

    private async Task<ServiceResult<byte[]>> ExportJobAsync(Guid jobId, bool ot, CancellationToken ct)
    {
        try
        {
            var job = (await _uow.SqlQueryRawAsync<JobExportRangeRow>(@"
SELECT FromDate,ToDate,Status
FROM dbo.F03HrmAttendanceCalculationJob
WHERE JobId=@JobId;", ct, JobParam(jobId))).FirstOrDefault();

            if (job is null)
                return ServiceResult<byte[]>.Fail("Không tìm thấy job tính công để xuất Excel.");
            if (!string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                return ServiceResult<byte[]>.Fail($"Chỉ có thể xuất Excel khi job đã hoàn tất. Trạng thái hiện tại: {job.Status}.");

            var runs = await _uow.SqlQueryRawAsync<RunBatchRow>(@"
SELECT CalculationBatchId,FromDate,ToDate
FROM dbo.F03HrmAttendanceCalculationRun
WHERE Status=N'Succeeded'
  AND TriggeredBy LIKE CONCAT(N'HRM-JOB:',REPLACE(CONVERT(nvarchar(36),@JobId),N'-',N''),N':%')
ORDER BY FromDate;", ct, JobParam(jobId));

            var batchIds = runs.Select(x => x.CalculationBatchId).Distinct().ToList();
            if (batchIds.Count == 0)
                return ServiceResult<byte[]>.Fail("Job hoàn tất nhưng không có batch chấm công nào để xuất.");

            return await ExportAsync(
                batchIds,
                ot,
                new DateRange(job.FromDate.Date, job.ToDate.Date),
                ct);
        }
        catch (Exception ex)
        {
            return ServiceResult<byte[]>.Fail($"Không thể chuẩn bị dữ liệu xuất Excel cho job: {ex.Message}");
        }
    }

    private const int MaxDayColumns = 31;

    private async Task<ServiceResult<byte[]>> ExportAsync(
        IReadOnlyList<Guid> batchIds,
        bool ot,
        DateRange? requestedRange,
        CancellationToken ct)
    {
        try
        {
            if (batchIds.Count == 0)
                return ServiceResult<byte[]>.Fail("Không có batch chấm công để xuất.");

            _uow.SetCommandTimeout(120);
            var filter = BuildBatchFilter(batchIds, out var batchParameters);

            if (ot)
            {
                // Use the same F03HrmAttendanceCalculated rows as the calendar, across every daily
                // batch belonging to the durable job. This preserves the selected job date range.
                var rows = await _uow.SqlQueryRawAsync<OtRow>($@"
SELECT WorkDate,EmployeeCode,FullName,DeptCode,ShiftAbbr,
       CheckInTime,CheckOutTime,
       WorkMinutesDay,WorkMinutesNight,
       OTMinutesDay,OTMinutesNight,OTMinutesDayTC,OTMinutesNightTC,
       OTRecognizedMinutesDay,OTRecognizedMinutesNight,
       AttendanceDisplayValue,DayType,WorkSymbol,OtSymbol,SymbolRuleCode
FROM dbo.F03HrmAttendanceCalculated
WHERE CalculationBatchId IN ({filter})
ORDER BY DeptCode,EmployeeCode,WorkDate;", ct, batchParameters);

                if (rows.Count == 0)
                    return ServiceResult<byte[]>.Fail("Không có dữ liệu tính OT của batch/job này.");

                var range = requestedRange is null
                    ? await ResolveRangeAsync(batchIds, rows.Min(x => x.WorkDate), rows.Max(x => x.WorkDate), ct)
                    : ValidateRange(requestedRange.From, requestedRange.To);
                if (!range.IsSuccess)
                    return ServiceResult<byte[]>.Fail(range.Message ?? "Khoảng ngày không hợp lệ.");

                var holidays = await LoadHolidaysAsync(range.Data!.From, range.Data!.To, ct);
                var employees = await LoadEmployeesAsync(batchIds, ct);
                return ServiceResult<byte[]>.Ok(BuildOtWorkbook(rows, range.Data!.From, range.Data!.To, holidays, employees));
            }

            var attendance = await _uow.SqlQueryRawAsync<AttendanceRow>($@"
SELECT WorkDate,EmployeeCode,FullName,DeptCode,ShiftAbbr,
       WorkMinutesDay,WorkMinutesNight,
       CheckInTime,CheckOutTime,
       LeaveTotal,LeaveTypeCode,LeaveReason,
       HrmHoliday,HrmEmployeeHoliday,IsLocked,
       AttendanceDisplayValue
FROM dbo.F03HrmAttendanceCalculated
WHERE CalculationBatchId IN ({filter})
ORDER BY DeptCode,EmployeeCode,WorkDate;", ct, batchParameters);

            if (attendance.Count == 0)
                return ServiceResult<byte[]>.Fail("Không có dữ liệu chấm công của batch/job này.");

            var attendanceRange = requestedRange is null
                ? await ResolveRangeAsync(batchIds, attendance.Min(x => x.WorkDate), attendance.Max(x => x.WorkDate), ct)
                : ValidateRange(requestedRange.From, requestedRange.To);
            if (!attendanceRange.IsSuccess)
                return ServiceResult<byte[]>.Fail(attendanceRange.Message ?? "Khoảng ngày không hợp lệ.");

            var attendanceEmployees = await LoadEmployeesAsync(batchIds, ct);
            return ServiceResult<byte[]>.Ok(BuildAttendanceWorkbook(
                attendance,
                attendanceRange.Data!.From,
                attendanceRange.Data!.To,
                attendanceEmployees));
        }
        catch (Exception ex)
        {
            return ServiceResult<byte[]>.Fail($"Xuất Excel thất bại: {ex.Message}");
        }
        finally
        {
            _uow.SetCommandTimeout(30);
        }
    }

    private static SqlParameter JobParam(Guid jobId)
        => new("@JobId", SqlDbType.UniqueIdentifier) { Value = jobId };

    private static string BuildBatchFilter(IReadOnlyList<Guid> batchIds, out SqlParameter[] parameters)
    {
        var names = new string[batchIds.Count];
        parameters = new SqlParameter[batchIds.Count];
        for (var i = 0; i < batchIds.Count; i++)
        {
            names[i] = $"@BatchId{i}";
            parameters[i] = new SqlParameter(names[i], SqlDbType.UniqueIdentifier) { Value = batchIds[i] };
        }
        return string.Join(",", names);
    }

    private async Task<ServiceResult<DateRange>> ResolveRangeAsync(
        IReadOnlyList<Guid> batchIds,
        DateTime fallbackFrom,
        DateTime fallbackTo,
        CancellationToken ct)
    {
        var filter = BuildBatchFilter(batchIds, out var parameters);
        var runs = await _uow.SqlQueryRawAsync<RunRangeRow>($@"
SELECT FromDate,ToDate
FROM dbo.F03HrmAttendanceCalculationRun
WHERE CalculationBatchId IN ({filter});", ct, parameters);

        var from = runs.Count == 0 ? fallbackFrom.Date : runs.Min(x => x.FromDate.Date);
        var to = runs.Count == 0 ? fallbackTo.Date : runs.Max(x => x.ToDate.Date);
        return ValidateRange(from, to);
    }

    private static ServiceResult<DateRange> ValidateRange(DateTime from, DateTime to)
    {
        from = from.Date;
        to = to.Date;
        if (to < from)
            return ServiceResult<DateRange>.Fail("Khoảng ngày xuất không hợp lệ.");

        var days = (to - from).Days + 1;
        if (days > MaxDayColumns)
            return ServiceResult<DateRange>.Fail(
                $"Khoảng ngày {from:dd/MM/yyyy} - {to:dd/MM/yyyy} gồm {days} ngày, vượt quá {MaxDayColumns} cột của template Excel. Vui lòng xuất theo khoảng tối đa {MaxDayColumns} ngày.");

        return ServiceResult<DateRange>.Ok(new DateRange(from, to));
    }

    /// <summary>Thông tin nhân viên (ngày sinh, ngày vào, chức vụ) từ danh mục nhân viên của FVN.</summary>
    private async Task<Dictionary<string, EmployeeInfoRow>> LoadEmployeesAsync(
        IReadOnlyList<Guid> batchIds,
        CancellationToken ct)
    {
        var filter = BuildBatchFilter(batchIds, out var parameters);
        var rows = await _uow.SqlQueryRawAsync<EmployeeInfoRow>($@"
SELECT LTRIM(RTRIM(e.EmployeeCode)) AS EmployeeCode,
       e.BirthDate,
       e.FirstWorkingDate,
       p.PositionName
FROM dbo.F03Employees e
LEFT JOIN dbo.F03Positions p ON p.PositionCode=e.PositionCode
WHERE LTRIM(RTRIM(e.EmployeeCode)) IN
      (SELECT DISTINCT LTRIM(RTRIM(c.EmployeeCode))
       FROM dbo.F03HrmAttendanceCalculated c
       WHERE c.CalculationBatchId IN ({filter}))
ORDER BY CASE WHEN e.IsActive=0 THEN 1 ELSE 0 END;", ct, parameters);

        var result = new Dictionary<string, EmployeeInfoRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = row.EmployeeCode?.Trim();
            if (!string.IsNullOrEmpty(code))
                result.TryAdd(code, row);
        }
        return result;
    }

    /// <summary>Ngày nghỉ của công ty - cùng nguồn WorkCalendarService (không lọc IsActive) để khớp lịch.</summary>
    private async Task<Dictionary<DateTime, CalendarHolidayInfoDto>> LoadHolidaysAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var pFrom = new SqlParameter("@From", SqlDbType.Date) { Value = from.Date };
        var pTo = new SqlParameter("@To", SqlDbType.Date) { Value = to.Date };
        var rows = await _uow.SqlQueryRawAsync<HolidayRow>(@"
SELECT HolidayDate,Description,HolidayType
FROM dbo.F03CompanyHolidays
WHERE HolidayDate>=@From AND HolidayDate<=@To
ORDER BY HolidayDate;", ct, pFrom, pTo);

        var result = new Dictionary<DateTime, CalendarHolidayInfoDto>();
        foreach (var row in rows)
        {
            var date = row.HolidayDate.Date;
            if (result.ContainsKey(date)) continue;

            var type = GetHolidayTypeCode(row.HolidayType);
            result[date] = new CalendarHolidayInfoDto
            {
                Code = $"HOL-{type}-{date:yyyyMMdd}",
                Name = $"[{type}]|{row.Description}",
                Type = type
            };
        }
        return result;
    }

    private static string GetHolidayTypeCode(byte holidayType) => DayTypeCatalog.HolidayTypeCode(holidayType);

    private byte[] BuildAttendanceWorkbook(
        List<AttendanceRow> rows,
        DateTime from,
        DateTime to,
        IReadOnlyDictionary<string, EmployeeInfoRow> employees)
    {
        using var input = File.OpenRead(GetTemplate("GA.xls"));
        var workbook = new HSSFWorkbook(input);
        var sheet = workbook.GetSheetAt(0);

        ApplyAttendanceHeader(sheet, from, to);

        var grouped = rows
            .GroupBy(x => new { x.EmployeeCode, x.FullName, x.DeptCode })
            .OrderBy(x => x.Key.DeptCode)
            .ThenBy(x => x.Key.EmployeeCode)
            .ToList();

        var firstDataRow = 5;
        EnsureRows(sheet, firstDataRow, grouped.Count, 38);

        for (var i = 0; i < grouped.Count; i++)
        {
            var row = sheet.GetRow(firstDataRow + i) ?? sheet.CreateRow(firstDataRow + i);
            var item = grouped[i];
            var info = FindEmployee(employees, item.Key.EmployeeCode);

            SetText(row, 0, (i + 1).ToString());
            SetText(row, 1, item.Key.EmployeeCode);
            SetText(row, 2, item.Key.FullName?.Trim());
            // Cột 3 (Ngày Sinh), 4 (Ngày Vào), 5 (Chức Vụ) lấy từ danh mục nhân viên FVN.
            // Không có dữ liệu thì để trống (template chứa dữ liệu mẫu của nhân viên khác nên luôn phải ghi đè).
            SetText(row, 3, FormatDate(info?.BirthDate));
            SetText(row, 4, FormatDate(info?.FirstWorkingDate));
            SetText(row, 5, info?.PositionName?.Trim());
            SetText(row, 6, item.Key.DeptCode?.ToString());

            var byDate = item.ToDictionary(x => x.WorkDate.Date);
            for (var day = 0; day < MaxDayColumns; day++)
            {
                var date = from.Date.AddDays(day);
                var cell = row.GetCell(7 + day) ?? row.CreateCell(7 + day);
                cell.SetCellValue(date <= to.Date ? GetAttendanceMark(byDate.GetValueOrDefault(date)) : string.Empty);
            }
        }

        ClearUnusedRows(sheet, firstDataRow + grouped.Count, 38);
        return WriteWorkbook(workbook);
    }

    private byte[] BuildOtWorkbook(
        List<OtRow> rows,
        DateTime from,
        DateTime to,
        IReadOnlyDictionary<DateTime, CalendarHolidayInfoDto> holidays,
        IReadOnlyDictionary<string, EmployeeInfoRow> employees)
    {
        using var input = File.OpenRead(GetTemplate("OT - GA.xls"));
        var workbook = new HSSFWorkbook(input);
        var sheet = workbook.GetSheetAt(0);

        ApplyOtHeader(sheet, from, to);

        var grouped = rows
            .GroupBy(x => new { x.EmployeeCode, x.FullName, x.DeptCode })
            .OrderBy(x => x.Key.DeptCode)
            .ThenBy(x => x.Key.EmployeeCode)
            .ToList();

        var firstDataRow = 6;
        EnsureRows(sheet, firstDataRow, grouped.Count, 35);

        for (var i = 0; i < grouped.Count; i++)
        {
            var row = sheet.GetRow(firstDataRow + i) ?? sheet.CreateRow(firstDataRow + i);
            var item = grouped[i];

            SetText(row, 0, (i + 1).ToString());
            SetText(row, 1, item.Key.EmployeeCode);
            SetText(row, 2, item.Key.FullName?.Trim());
            SetText(row, 3, item.Key.DeptCode?.ToString());

            var byDate = item.ToDictionary(x => x.WorkDate.Date);
            for (var day = 0; day < MaxDayColumns; day++)
            {
                var date = from.Date.AddDays(day);
                var cell = row.GetCell(4 + day) ?? row.CreateCell(4 + day);

                var value = date <= to.Date
                    ? GetOtValue(byDate.GetValueOrDefault(date), holidays)
                    : string.Empty;
                cell.SetCellValue(value);
            }
        }

        ClearUnusedRows(sheet, firstDataRow + grouped.Count, 35);
        return WriteWorkbook(workbook);
    }

    private static string GetAttendanceMark(AttendanceRow? row)
    {
        // The calculation SQL owns HRM display semantics. The exporter only renders it.
        return row?.AttendanceDisplayValue?.Trim() ?? string.Empty;
    }

    private static string GetOtValue(OtRow? row, IReadOnlyDictionary<DateTime, CalendarHolidayInfoDto> holidays)
    {
        // Only the persisted rule result (OtSymbol/RuleCode) is used; nothing is recalculated here.
        if (row is null) return string.Empty;

        holidays.TryGetValue(row.WorkDate.Date, out var holiday);

        var day = new CalendarDayDto
        {
            Date = DateOnly.FromDateTime(row.WorkDate),
            Holiday = holiday,
            Shift = new CalendarShiftInfoDto { ShiftAbbr = row.ShiftAbbr },
            Attendance = new CalendarAttendanceInfoDto
            {
                CheckIn = row.CheckInTime,
                CheckOut = row.CheckOutTime,
                WorkMinutes = Math.Max(0, row.WorkMinutesDay) + Math.Max(0, row.WorkMinutesNight),
                ActualOtMinutes = Math.Max(0, row.OTMinutesDay)
                    + Math.Max(0, row.OTMinutesNight)
                    + Math.Max(0, row.OTMinutesDayTC)
                    + Math.Max(0, row.OTMinutesNightTC),
                RecognizedOtMinutes = Math.Max(0, row.OTRecognizedMinutesDay)
                    + Math.Max(0, row.OTRecognizedMinutesNight),
                DisplayValue = row.AttendanceDisplayValue,
                // Ký hiệu đã ghi theo rule lúc tính công.
                RuleWorkSymbol = row.WorkSymbol,
                RuleOtSymbol = row.OtSymbol,
                RuleDayType = row.DayType,
                RuleCode = row.SymbolRuleCode
            }
        };

        return AttendanceSymbolCalculator.Resolve(day).OtSymbol?.Trim() ?? string.Empty;
    }

    private static EmployeeInfoRow? FindEmployee(IReadOnlyDictionary<string, EmployeeInfoRow> employees, string? code)
    {
        var key = code?.Trim();
        return !string.IsNullOrEmpty(key) && employees.TryGetValue(key, out var info) ? info : null;
    }

    private static string FormatDate(DateTime? value)
        => value.HasValue ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty;

    private static string DayLabel(DateTime date)
        => date.Day == 1 ? date.ToString("dd/MM", CultureInfo.InvariantCulture) : date.Day.ToString(CultureInfo.InvariantCulture);

    private static string FormatOtHours(int minutes)
    {
        var hours = Math.Round(minutes / 60d, 2, MidpointRounding.AwayFromZero);
        return hours.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;

    private string GetTemplate(string name)
    {
        var path = Path.Combine(_environment.ContentRootPath, "Templates", "HRM", name);
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "Templates", "HRM", name);

        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Không tìm thấy template Excel: {name} (thư mục Templates/HRM của API). Vui lòng publish lại API hoặc liên hệ quản trị hệ thống.",
                path);

        return path;
    }

    private static void ApplyAttendanceHeader(ISheet sheet, DateTime from, DateTime to)
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        var monthText = to.ToString("MM", CultureInfo.InvariantCulture);
        var yearText = to.ToString("yyyy", CultureInfo.InvariantCulture);

        SetText(sheet.GetRow(0), 0, "CÔNG TY TNHH FCC VIỆT NAM");
        SetText(sheet.GetRow(1), 0, $"THÁNG {monthText}");
        SetText(sheet.GetRow(3), 0,
            $"BẢNG CHẤM CÔNG THÁNG {monthText}/{yearText} ({from:dd/MM/yyyy} - {to:dd/MM/yyyy})");

        for (var i = 0; i < MaxDayColumns; i++)
        {
            var date = from.Date.AddDays(i);
            var inRange = date <= to.Date;
            SetText(sheet.GetRow(3), 7 + i, inRange ? DayLabel(date) : string.Empty);
            SetText(sheet.GetRow(4), 7 + i, inRange ? date.ToString("ddd", us) : string.Empty);
        }
    }

    private static void ApplyOtHeader(ISheet sheet, DateTime from, DateTime to)
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        var monthText = to.ToString("MM", CultureInfo.InvariantCulture);
        var yearText = to.ToString("yyyy", CultureInfo.InvariantCulture);

        SetText(sheet.GetRow(0), 0, "CÔNG TY TNHH FCC VIỆT NAM");
        SetText(sheet.GetRow(1), 0,
            $"                BẢNG  LÀM THÊM THÁNG {monthText} NĂM {yearText} ({from:dd/MM/yyyy} - {to:dd/MM/yyyy})");

        for (var i = 0; i < MaxDayColumns; i++)
        {
            var date = from.Date.AddDays(i);
            var inRange = date <= to.Date;
            SetText(sheet.GetRow(3), 4 + i, inRange ? DayLabel(date) : string.Empty);
            SetText(sheet.GetRow(4), 4 + i, inRange ? date.ToString("ddd", us) : string.Empty);
        }
    }

    private static void EnsureRows(ISheet sheet, int firstRow, int count, int columns)
    {
        var template = sheet.GetRow(firstRow);
        for (var i = 0; i < count; i++)
        {
            var row = sheet.GetRow(firstRow + i) ?? sheet.CreateRow(firstRow + i);
            if (template is not null && row != template)
            {
                row.Height = template.Height;
                for (var c = 0; c < columns; c++)
                {
                    var source = template.GetCell(c);
                    if (source is null) continue;
                    var target = row.GetCell(c) ?? row.CreateCell(c);
                    target.CellStyle = source.CellStyle;
                }
            }
        }
    }

    private static void ClearUnusedRows(ISheet sheet, int start, int columns)
    {
        for (var r = start; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row is null) continue;
            for (var c = 0; c < columns; c++)
                row.GetCell(c)?.SetCellValue(string.Empty);
        }
    }

    private static void SetText(IRow? row, int index, string? value)
    {
        if (row is null) return;
        var cell = row.GetCell(index) ?? row.CreateCell(index);
        cell.SetCellValue(value ?? string.Empty);
    }

    private static byte[] WriteWorkbook(HSSFWorkbook workbook)
    {
        using var output = new MemoryStream();
        workbook.Write(output, false);
        return output.ToArray();
    }

    private sealed class AttendanceRow
    {
        public DateTime WorkDate { get; set; }
        public string? EmployeeCode { get; set; }
        public string? FullName { get; set; }
        public int? DeptCode { get; set; }
        public string? ShiftAbbr { get; set; }
        public int WorkMinutesDay { get; set; }
        public int WorkMinutesNight { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public decimal? LeaveTotal { get; set; }
        public string? LeaveTypeCode { get; set; }
        public string? LeaveReason { get; set; }
        public bool? HrmHoliday { get; set; }
        public bool? HrmEmployeeHoliday { get; set; }
        public bool? IsLocked { get; set; }
        public string? AttendanceDisplayValue { get; set; }
    }

    private sealed class OtRow
    {
        public DateTime WorkDate { get; set; }
        public string? EmployeeCode { get; set; }
        public string? FullName { get; set; }
        public int? DeptCode { get; set; }
        public string? ShiftAbbr { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public int WorkMinutesDay { get; set; }
        public int WorkMinutesNight { get; set; }
        public int OTMinutesDay { get; set; }
        public int OTMinutesNight { get; set; }
        public int OTMinutesDayTC { get; set; }
        public int OTMinutesNightTC { get; set; }
        public int OTRecognizedMinutesDay { get; set; }
        public int OTRecognizedMinutesNight { get; set; }
        public string? AttendanceDisplayValue { get; set; }
        public string? DayType { get; set; }
        public string? WorkSymbol { get; set; }
        public string? OtSymbol { get; set; }
        public string? SymbolRuleCode { get; set; }
    }

    private sealed class RunRangeRow
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    private sealed class RunBatchRow
    {
        public Guid CalculationBatchId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    private sealed class JobExportRangeRow
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    private sealed class EmployeeInfoRow
    {
        public string? EmployeeCode { get; set; }
        public DateTime? BirthDate { get; set; }
        public DateTime? FirstWorkingDate { get; set; }
        public string? PositionName { get; set; }
    }

    private sealed class HolidayRow
    {
        public DateTime HolidayDate { get; set; }
        public string? Description { get; set; }
        public byte HolidayType { get; set; }
    }

    private sealed record DateRange(DateTime From, DateTime To);
}
