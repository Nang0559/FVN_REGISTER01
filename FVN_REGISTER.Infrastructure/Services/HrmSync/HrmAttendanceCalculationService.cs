using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Application.Interfaces.HrmSync;
using FVN_REGISTER.Contract.Dtos.HrmSync;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Repositories;
using Microsoft.Data.SqlClient;
using FVN_REGISTER.Infrastructure.Models.Data;
using Microsoft.EntityFrameworkCore;
using FVN_REGISTER.Core.Utils;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text.Json;

namespace FVN_REGISTER.Infrastructure.Services.HrmSync;

public sealed class HrmAttendanceCalculationService : IHrmAttendanceCalculationService
{
 private readonly IUnitOfWork _uow;
 private readonly ILogger<HrmAttendanceCalculationService> _logger;
 private readonly FVNWEBAPPContext _db;
 private readonly IAttendanceSymbolRuleService _symbolRules;

 public HrmAttendanceCalculationService(IUnitOfWork uow,ILogger<HrmAttendanceCalculationService> logger,FVNWEBAPPContext db,IAttendanceSymbolRuleService symbolRules)
 { _uow=uow; _logger=logger; _db=db; _symbolRules=symbolRules; }

 public async Task<ServiceResult<HrmAttendanceCalculationResultDto>> CalculateAsync(
  HrmAttendanceCalculationRequestDto request,string? triggeredBy=null,CancellationToken ct=default)
 {
  if(request.FromDate.Date>request.ToDate.Date)
   return ServiceResult<HrmAttendanceCalculationResultDto>.Fail("FromDate không được lớn hơn ToDate.");

  var dept=request.DeptCode == null?null:request.DeptCode;
  var employeeCode=string.IsNullOrWhiteSpace(request.EmployeeCode)?null:request.EmployeeCode.Trim();

  if(employeeCode is not null)
  {
   var exists=await _db.Employees.AsNoTracking()
    .AnyAsync(x=>x.IsActive!=false && x.EmployeeCode!=null && x.EmployeeCode.Trim()==employeeCode,ct);
   if(!exists)
    return ServiceResult<HrmAttendanceCalculationResultDto>.Fail($"Không tìm thấy nhân viên '{employeeCode}'.");
  }

  var pDept=new SqlParameter("@DeptCode",SqlDbType.Int){Value=(object?)dept??DBNull.Value};
  var pEmployee=new SqlParameter("@EmployeeCode",SqlDbType.NVarChar,50){Value=(object?)employeeCode??DBNull.Value};
  var pFrom=new SqlParameter("@FromDate",SqlDbType.Date){Value=request.FromDate.Date};
  var pTo=new SqlParameter("@ToDate",SqlDbType.Date){Value=request.ToDate.Date};
  var pBy=new SqlParameter("@TriggeredBy",SqlDbType.NVarChar,100){Value=(object?)triggeredBy??"SYSTEM"};

  _uow.SetCommandTimeout(1800);
  try
  {
   var rows=await _uow.SqlQueryRawAsync<HrmAttendanceCalculationResultDto>(
    "EXEC dbo.usp_CalculateHrmAttendance @DeptCode,@EmployeeCode,@FromDate,@ToDate,@TriggeredBy",
    ct,pDept,pEmployee,pFrom,pTo,pBy);
   var result=rows.FirstOrDefault();

   // Nối rule ký hiệu (F03AttendanceSymbolRules) vào kết quả tính công thật:
   // lịch, xuất chấm công và xuất OT đều đọc các cột WorkSymbol/OtSymbol/SymbolRuleCode/DayType này.
   if(result != null)
    await ApplySymbolRulesAsync(result.CalculationBatchId,ct);

   if(result != null && request.ReopenPayrollPeriod)
   {
    var from=DateOnly.FromDateTime(request.FromDate.Date);
    var to=DateOnly.FromDateTime(request.ToDate.Date);
    var periods=await _db.PayrollCalculationPeriods
      .Where(x=>x.IsActive!=false && x.Status=="Calculated" && x.FromDate<=to && x.ToDate>=from)
      .ToListAsync(ct);
    foreach(var period in periods)
    {
     period.Status="Open";
     period.ModifiedAt=DateTime.Now;
     period.ModifiedBy=0;
     period.LastModifiedSource="HRM_ATTENDANCE_RECALC";
    }
    if(periods.Count>0) await _db.SaveChangesAsync(ct);
   }

   return result==null
    ? ServiceResult<HrmAttendanceCalculationResultDto>.Fail("Không nhận được kết quả tính giờ.")
    : ServiceResult<HrmAttendanceCalculationResultDto>.Ok(result);
  }
  catch(SqlException ex) when(ex.Number==51322)
  {
   _logger.LogWarning(ex,"HRM-compatible attendance calculation skipped: another calculation holds the same date. Dept={Dept}, EmployeeCode={EmployeeCode}, From={From}, To={To}",
    dept,employeeCode,request.FromDate,request.ToDate);
   return ServiceResult<HrmAttendanceCalculationResultDto>.Fail(ex.Message);
  }
  catch(Exception ex)
  {
   _logger.LogError(ex,"HRM-compatible attendance calculation failed. Dept={Dept}, EmployeeCode={EmployeeCode}, From={From}, To={To}",
    dept,employeeCode,request.FromDate,request.ToDate);
   return ServiceResult<HrmAttendanceCalculationResultDto>.Fail("Tính giờ HRM-compatible thất bại.");
  }
  finally { _uow.SetCommandTimeout(30); }
 }

 /// <summary>
 /// Áp rule ký hiệu cho mọi dòng đã có In/Out của batch. DayType đã được usp_CalculateHrmAttendance ghi
 /// (dbo.fn_ResolveDayType: ngày nghỉ HRM > cờ HRM > CN > T7 > thường). Dòng không khớp rule nào để SymbolRuleCode = NULL
 /// (lịch/xuất không có ký hiệu OT cho dòng đó) và được log để bổ sung rule.
 /// </summary>
 private async Task ApplySymbolRulesAsync(Guid batchId,CancellationToken ct)
 {
  var inputs=await _db.Database.SqlQuery<SymbolInputRow>($"""
   SELECT Id,WorkDate,ShiftId,ShiftAbbr,CheckInTime,CheckOutTime,DayType
   FROM dbo.F03HrmAttendanceCalculated
   WHERE CalculationBatchId={batchId} AND CheckInTime IS NOT NULL AND CheckOutTime IS NOT NULL
   """).ToListAsync(ct);

  var matched=new List<SymbolOutput>(inputs.Count);
  var unmatched=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
  foreach(var row in inputs)
  {
   ct.ThrowIfCancellationRequested();
   var dayType=string.IsNullOrWhiteSpace(row.DayType)?"NORMAL":row.DayType!;
   var eval=await _symbolRules.EvaluateAsync(DateOnly.FromDateTime(row.WorkDate),dayType,row.ShiftAbbr,row.ShiftId,row.CheckInTime,row.CheckOutTime,null,ct);
   if(eval.Matched&&!string.IsNullOrWhiteSpace(eval.RuleCode))
    matched.Add(new SymbolOutput(row.Id,eval.WorkSymbol,eval.OtSymbol,eval.RuleCode));
   else
    unmatched[$"{dayType}/{row.ShiftAbbr}"]=unmatched.GetValueOrDefault($"{dayType}/{row.ShiftAbbr}")+1;
  }

  _db.Database.SetCommandTimeout(600);
  foreach(var chunk in matched.Chunk(1000))
  {
   var json=JsonSerializer.Serialize(chunk.Select(x=>new{id=x.Id,ws=x.WorkSymbol,os=x.OtSymbol,rc=x.RuleCode}));
   await _db.Database.ExecuteSqlInterpolatedAsync($"""
    UPDATE a SET
        a.WorkSymbol=j.ws,a.OtSymbol=j.os,a.SymbolRuleCode=j.rc,
        a.OtDisplayValue=COALESCE(j.os,a.OtDisplayValue),
        a.AttendanceDisplayValue=CASE WHEN j.ws IS NOT NULL AND a.DayType<>N'NORMAL' AND ISNULL(a.LeaveTotal,0)=0 THEN j.ws ELSE a.AttendanceDisplayValue END
    FROM dbo.F03HrmAttendanceCalculated AS a
    INNER JOIN OPENJSON({json}) WITH (id bigint '$.id',ws nvarchar(50) '$.ws',os nvarchar(50) '$.os',rc nvarchar(100) '$.rc') AS j ON j.id=a.Id
    """,ct);
  }

  if(unmatched.Count>0)
   _logger.LogWarning("Symbol rules: {Count} dòng không khớp rule nào (DayType/Ca → số dòng): {Detail}. Bổ sung rule ở /admin/attendance-symbol-rules.",
    unmatched.Values.Sum(),string.Join(", ",unmatched.OrderByDescending(x=>x.Value).Take(10).Select(x=>$"{x.Key}={x.Value}")));
 }

 private sealed record SymbolInputRow(long Id,DateTime WorkDate,int? ShiftId,string? ShiftAbbr,DateTime? CheckInTime,DateTime? CheckOutTime,string? DayType);
 private sealed record SymbolOutput(long Id,string? WorkSymbol,string? OtSymbol,string RuleCode);

 public async Task<ServiceResult> EnsureEmployeeRangeAsync(
  string employeeCode,int? deptCode,DateOnly from,DateOnly to,CancellationToken ct=default)
 {
  if(from>to) return ServiceResult.Fail("Khoảng ngày chấm công không hợp lệ.");
  employeeCode=employeeCode.Trim();
  if(employeeCode.Length==0) return ServiceResult.Fail("Mã nhân viên là bắt buộc.");

  // Future dates are registration/calendar data, not attendance-calculation input.
  // Calendar may prepare data through today, but never calculate future attendance.
  var effectiveTo = to > DateOnly.FromDateTime(DateTime.Today)
      ? DateOnly.FromDateTime(DateTime.Today)
      : to;
  if(from > effectiveTo)
   return ServiceResult.Ok();

  var runs=await _db.Database.SqlQuery<CoverageRun>($"""
   SELECT r.FromDate,r.ToDate,r.DeptCode,r.EmployeeCode,r.Status
   FROM dbo.F03HrmAttendanceCalculationRun AS r
   WHERE r.Status=N'Succeeded'
     AND r.FromDate<={effectiveTo}
     AND r.ToDate>={from}
     AND (
          r.EmployeeCode={employeeCode}
          OR (r.EmployeeCode IS NULL AND r.DeptCode IS NULL)
     )
   ORDER BY r.FromDate
   """).ToListAsync(ct);

  var gaps=FindGaps(from,effectiveTo,runs);

  // Closed/exported payroll periods are immutable. They must be served from
  // F03HrmAttendanceHistory by the calendar provider and must never be reopened
  // or recalculated just because a user navigates to an old month.
  var lockedPeriods=await _db.PayrollCalculationPeriods.AsNoTracking()
   .Where(x=>x.IsActive!=false
      && (x.Status=="Locked" || x.Status=="Exported")
      && x.FromDate<=effectiveTo
      && x.ToDate>=from)
   .Select(x=>new DateRange(x.FromDate,x.ToDate))
   .ToListAsync(ct);

  foreach(var gap in SubtractRanges(gaps,lockedPeriods))
  {
   ct.ThrowIfCancellationRequested();
   var result=await CalculateAsync(
    new HrmAttendanceCalculationRequestDto
    {
     EmployeeCode=employeeCode,
     DeptCode=deptCode,
     FromDate=gap.From.ToDateTime(TimeOnly.MinValue),
     ToDate=gap.To.ToDateTime(TimeOnly.MinValue),
     ReopenPayrollPeriod=false
    },
    $"HRM-ATTENDANCE-CALENDAR-BACKFILL:{employeeCode}",
    ct);

   if(!result.IsSuccess)
    return ServiceResult.Fail(result.Message ?? $"Không thể backfill chấm công {gap.From:yyyy-MM-dd}..{gap.To:yyyy-MM-dd}.");
  }

  return ServiceResult.Ok();
 }

 private static IReadOnlyList<DateRange> FindGaps(DateOnly from,DateOnly to,IReadOnlyList<CoverageRun> runs)
 {
  var intervals=runs.Select(x=>new DateRange(
      x.FromDate<from?from:x.FromDate,
      x.ToDate>to?to:x.ToDate))
    .Where(x=>x.From<=x.To).OrderBy(x=>x.From).ToList();

  var gaps=new List<DateRange>();
  var cursor=from;
  foreach(var interval in intervals)
  {
   if(interval.From>cursor)
    gaps.Add(new DateRange(cursor,interval.From.AddDays(-1)));
   if(interval.To>=cursor)
    cursor=interval.To.AddDays(1);
   if(cursor>to) break;
  }
  if(cursor<=to) gaps.Add(new DateRange(cursor,to));
  return gaps;
 }

 private static IReadOnlyList<DateRange> SubtractRanges(
  IReadOnlyList<DateRange> source,
  IReadOnlyList<DateRange> exclusions)
 {
  var result=new List<DateRange>();
  foreach(var range in source)
  {
   var pieces=new List<DateRange>{range};
   foreach(var exclusion in exclusions)
   {
    var next=new List<DateRange>();
    foreach(var piece in pieces)
    {
     if(exclusion.To<piece.From || exclusion.From>piece.To)
     {
      next.Add(piece);
      continue;
     }

     if(piece.From<exclusion.From)
      next.Add(new DateRange(piece.From,exclusion.From.AddDays(-1)));

     if(piece.To>exclusion.To)
      next.Add(new DateRange(exclusion.To.AddDays(1),piece.To));
    }
    pieces=next;
    if(pieces.Count==0) break;
   }
   result.AddRange(pieces);
  }
  return result;
 }

 private sealed record CoverageRun(DateOnly FromDate,DateOnly ToDate,int? DeptCode,string? EmployeeCode,string Status);
 private sealed record DateRange(DateOnly From,DateOnly To);
}