using System.Globalization;
using System.Text.Json;
using FVN_REGISTER.Application.Interfaces.Calendar;
using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Contract.Utils;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.Calendar;

public sealed class AttendanceSymbolRuleService : IAttendanceSymbolRuleService
{
    private readonly FVNWEBAPPContext _db;
    private List<RuleRow>? _activeRows;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public AttendanceSymbolRuleService(FVNWEBAPPContext db) => _db = db;

    public async Task<ServiceResult<IReadOnlyList<AttendanceSymbolRuleDto>>> GetAllAsync(CancellationToken ct = default)
    {
        try { var rows = await LoadRowsAsync(false, ct); return ServiceResult<IReadOnlyList<AttendanceSymbolRuleDto>>.Ok(rows.Select(ToDto).ToArray()); }
        catch (Exception ex) { return ServiceResult<IReadOnlyList<AttendanceSymbolRuleDto>>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<AttendanceSymbolRuleDto>> SaveAsync(int? id, AttendanceSymbolRuleUpsertRequest request, int actorUserId, CancellationToken ct = default)
    {
        try
        {
            Validate(request);
            var json = JsonSerializer.Serialize(new RuleDocument { BlockMinutes=request.BlockMinutes, RoundingMode=request.RoundingMode, AllocationMode=request.AllocationMode, FixedOtMinutes=request.FixedOtMinutes, Segments=request.Segments }, JsonOptions);
            if (id is null)
            {
                await _db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO dbo.F03AttendanceSymbolRules
                    (RuleCode,RuleName,DayType,ShiftCode,ShiftId,Priority,MinActualMinutes,MaxActualMinutes,CheckInFrom,CheckInTo,EffectiveFrom,EffectiveTo,RuleJson,IsActive,CreatedBy,CreatedAt,LastModifiedSource)
                    VALUES ({request.RuleCode.Trim()},{request.RuleName.Trim()},{request.DayType.Trim().ToUpperInvariant()},{Normalize(request.ShiftCode)},{request.ShiftId},{request.Priority},{request.MinActualMinutes},{request.MaxActualMinutes},{Normalize(request.CheckInFrom)},{Normalize(request.CheckInTo)},{request.EffectiveFrom?.ToDateTime(TimeOnly.MinValue)},{request.EffectiveTo?.ToDateTime(TimeOnly.MinValue)},CONVERT(nvarchar(max),{json}),{request.IsActive},{actorUserId},SYSUTCDATETIME(),N'UI')
                    """, ct);
            }
            else
            {
                await _db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE dbo.F03AttendanceSymbolRules SET RuleCode={request.RuleCode.Trim()},RuleName={request.RuleName.Trim()},DayType={request.DayType.Trim().ToUpperInvariant()},ShiftCode={Normalize(request.ShiftCode)},ShiftId={request.ShiftId},Priority={request.Priority},MinActualMinutes={request.MinActualMinutes},MaxActualMinutes={request.MaxActualMinutes},CheckInFrom={Normalize(request.CheckInFrom)},CheckInTo={Normalize(request.CheckInTo)},EffectiveFrom={request.EffectiveFrom?.ToDateTime(TimeOnly.MinValue)},EffectiveTo={request.EffectiveTo?.ToDateTime(TimeOnly.MinValue)},RuleJson=CONVERT(nvarchar(max),{json}),IsActive={request.IsActive},ModifiedBy={actorUserId},ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'UI' WHERE Id={id.Value}
                    """, ct);
            }
            _activeRows = null;
            var rows = await LoadRowsAsync(false, ct);
            var saved = rows.FirstOrDefault(x => string.Equals(x.RuleCode,request.RuleCode.Trim(),StringComparison.OrdinalIgnoreCase));
            return saved is null ? ServiceResult<AttendanceSymbolRuleDto>.Fail("Không tìm thấy rule vừa lưu.") : ServiceResult<AttendanceSymbolRuleDto>.Ok(ToDto(saved));
        }
        catch (Exception ex) { return ServiceResult<AttendanceSymbolRuleDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<object>> DeactivateAsync(int id,int actorUserId,CancellationToken ct=default)
    {
        try { await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.F03AttendanceSymbolRules SET IsActive=0,ModifiedBy={actorUserId},ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'UI' WHERE Id={id}",ct); _activeRows=null; return ServiceResult<object>.Ok(new{id,active=false}); }
        catch(Exception ex){return ServiceResult<object>.Fail(ex.Message);}
    }

    public Task<AttendanceSymbolRuleTestResultDto> TestAsync(AttendanceSymbolRuleTestRequest request,CancellationToken ct=default)
        => EvaluateAsync(request.WorkDate,request.DayType,request.ShiftCode,request.ShiftId,request.CheckIn,request.CheckOut,request.ActualOtMinutes,ct);

    public async Task<AttendanceSymbolRuleTestResultDto> EvaluateAsync(DateOnly workDate,string dayType,string? shiftCode,int? shiftId,DateTime? checkIn,DateTime? checkOut,int? actualOtMinutes=null,CancellationToken ct=default)
    {
        if(!checkIn.HasValue||!checkOut.HasValue||checkOut<=checkIn)return new(){DayType=dayType,Explanation="Thiếu In/Out hợp lệ."};
        var elapsed=(int)Math.Round((checkOut.Value-checkIn.Value).TotalMinutes,MidpointRounding.AwayFromZero); var normalizedShift=NormalizeShift(shiftCode); var rows=await LoadRowsAsync(true,ct);
        var rule=rows.Where(x=>string.Equals(x.DayType,dayType,StringComparison.OrdinalIgnoreCase))
            .Where(x=>x.ShiftId is null||shiftId is null||x.ShiftId==shiftId)
            .Where(x=>string.IsNullOrWhiteSpace(x.ShiftCode)||string.Equals(NormalizeShift(x.ShiftCode),normalizedShift,StringComparison.OrdinalIgnoreCase))
            .Where(x=>!x.MinActualMinutes.HasValue||elapsed>=x.MinActualMinutes.Value).Where(x=>!x.MaxActualMinutes.HasValue||elapsed<=x.MaxActualMinutes.Value)
            .Where(x=>InTimeMatches(checkIn.Value.TimeOfDay,x.CheckInFrom,x.CheckInTo)).Where(x=>EffectiveDateMatches(workDate,x.EffectiveFrom,x.EffectiveTo))
            .OrderBy(x=>x.Priority).ThenByDescending(x=>x.ShiftId.HasValue).ThenByDescending(x=>!string.IsNullOrWhiteSpace(x.ShiftCode)).FirstOrDefault();
        if(rule is null)return new(){Matched=false,DayType=dayType,RawElapsedMinutes=elapsed,Explanation="Không tìm thấy rule phù hợp."};
        var doc=Parse(rule.RuleJson);var result=Calculate(doc,workDate,checkIn.Value,checkOut.Value,elapsed);
        return new(){Matched=true,RuleCode=rule.RuleCode,DayType=rule.DayType,WorkSymbol=result.WorkSymbol,OtSymbol=result.OtSymbol,WorkMinutesForSymbol=result.WorkMinutes,OtMinutesForSymbol=result.OtMinutes,RawElapsedMinutes=elapsed,RoundedOtMinutes=result.OtMinutes,Explanation=$"Rule {rule.RuleCode}: công {result.WorkMinutes/60m:0.##}h, OT {result.OtMinutes/60m:0.##}h theo block {doc.BlockMinutes} phút / {doc.RoundingMode}."};
    }

    private async Task<List<RuleRow>> LoadRowsAsync(bool activeOnly,CancellationToken ct)
    {
        if(activeOnly&&_activeRows is not null)return _activeRows;
        var rows=await _db.Database.SqlQuery<RuleRow>($"""
            SELECT Id,RuleCode,RuleName,DayType,ShiftCode,ShiftId,Priority,MinActualMinutes,MaxActualMinutes,CheckInFrom,CheckInTo,EffectiveFrom,EffectiveTo,RuleJson,IsActive
            FROM dbo.F03AttendanceSymbolRules WHERE ({activeOnly}=0 OR IsActive=1) ORDER BY Priority,Id
            """).ToListAsync(ct);
        if(activeOnly)_activeRows=rows; return rows;
    }

    private static AttendanceSymbolRuleDto ToDto(RuleRow row){var d=Parse(row.RuleJson);return new(){Id=row.Id,RuleCode=row.RuleCode,RuleName=row.RuleName,DayType=row.DayType,ShiftCode=row.ShiftCode,ShiftId=row.ShiftId,Priority=row.Priority,MinActualMinutes=row.MinActualMinutes,MaxActualMinutes=row.MaxActualMinutes,CheckInFrom=row.CheckInFrom,CheckInTo=row.CheckInTo,IsActive=row.IsActive,EffectiveFrom=row.EffectiveFrom?.ToString("yyyy-MM-dd"),EffectiveTo=row.EffectiveTo?.ToString("yyyy-MM-dd"),Segments=d.Segments};}
    private static RuleDocument Parse(string json)=>JsonSerializer.Deserialize<RuleDocument>(json,JsonOptions)??new();

    private static Calculation Calculate(RuleDocument doc,DateOnly workDate,DateTime checkIn,DateTime checkOut,int elapsed)
    {
        int work=0,ot=0;
        if(string.Equals(doc.AllocationMode,"FIXED_OT",StringComparison.OrdinalIgnoreCase)&&doc.FixedOtMinutes is >0){ot=ApplyBlock(Math.Min(elapsed,doc.FixedOtMinutes.Value),doc.BlockMinutes,doc.RoundingMode);work=Math.Max(0,elapsed-doc.FixedOtMinutes.Value);}
        else foreach(var s in doc.Segments){var start=At(workDate,s.Start);var end=At(workDate,s.End);if(end<=start)end=end.AddDays(1);var overlapStart = checkIn > start ? checkIn : start; var overlapEnd = checkOut < end ? checkOut : end; var overlap = overlapEnd > overlapStart ? (int)Math.Round((overlapEnd - overlapStart).TotalMinutes, MidpointRounding.AwayFromZero) : 0;var minutes=s.FixedMinutes is >0?Math.Min(overlap,s.FixedMinutes.Value):overlap;minutes=ApplyBlock(minutes,doc.BlockMinutes,doc.RoundingMode);if(string.Equals(s.SegmentType,"OT",StringComparison.OrdinalIgnoreCase))ot+=minutes;else work+=minutes;}
        var ws=doc.Segments.FirstOrDefault(x=>string.Equals(x.SegmentType,"WORK",StringComparison.OrdinalIgnoreCase));var os=doc.Segments.FirstOrDefault(x=>string.Equals(x.SegmentType,"OT",StringComparison.OrdinalIgnoreCase));return new(work,ot,Render(ws?.SymbolTemplate,work,doc.BlockMinutes,doc.RoundingMode),Render(os?.SymbolTemplate,ot,doc.BlockMinutes,doc.RoundingMode));
    }
    private static string? Render(string? template,int minutes,int block,string rounding){if(string.IsNullOrWhiteSpace(template)||minutes<=0)return null;var rounded=ApplyBlock(minutes,block,rounding);if(rounded<=0)return null;var hours=(rounded/60m).ToString("0.##",CultureInfo.InvariantCulture);return template.Replace("{hours}",hours,StringComparison.OrdinalIgnoreCase).Replace("{minutes}",rounded.ToString(CultureInfo.InvariantCulture),StringComparison.OrdinalIgnoreCase);}
    private static int ApplyBlock(int minutes,int blockMinutes,string rounding){if(minutes<=0)return 0;var block=blockMinutes<=0?15:blockMinutes;var value=minutes/(decimal)block;var blocks=rounding.ToUpperInvariant() switch{"CEILING"=>(int)Math.Ceiling(value),"ROUND"=>(int)Math.Round(value,MidpointRounding.AwayFromZero),_=>(int)Math.Floor(value)};return Math.Max(0,blocks*block);}
    private static DateTime At(DateOnly date,string value)=>date.ToDateTime(TimeOnly.Parse(value,CultureInfo.InvariantCulture));
    private static bool EffectiveDateMatches(DateOnly date,DateTime? from,DateTime? to)=>(!from.HasValue||date>=DateOnly.FromDateTime(from.Value))&&(!to.HasValue||date<=DateOnly.FromDateTime(to.Value));
    private static bool InTimeMatches(TimeSpan time,string? fromText,string? toText){if(!TimeSpan.TryParse(fromText,CultureInfo.InvariantCulture,out var from)||!TimeSpan.TryParse(toText,CultureInfo.InvariantCulture,out var to))return true;return from<=to?time>=from&&time<=to:time>=from||time<=to;}
    private static string? Normalize(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();private static string NormalizeShift(string? value)=>(value??string.Empty).Trim().ToUpperInvariant().Replace(" ",string.Empty);
    private static void Validate(AttendanceSymbolRuleUpsertRequest request){if(string.IsNullOrWhiteSpace(request.RuleCode))throw new ArgumentException("RuleCode là bắt buộc.");if(string.IsNullOrWhiteSpace(request.RuleName))throw new ArgumentException("RuleName là bắt buộc.");if(request.BlockMinutes<=0)throw new ArgumentException("BlockMinutes phải lớn hơn 0.");if(request.Segments.Count==0)throw new ArgumentException("Rule phải có ít nhất một segment.");if(request.Segments.Any(x=>!TimeSpan.TryParse(x.Start,CultureInfo.InvariantCulture,out _)||!TimeSpan.TryParse(x.End,CultureInfo.InvariantCulture,out _)))throw new ArgumentException("Giờ segment không hợp lệ.");}
    private sealed class RuleRow{public int Id{get;set;}public string RuleCode{get;set;}=string.Empty;public string RuleName{get;set;}=string.Empty;public string DayType{get;set;}=string.Empty;public string? ShiftCode{get;set;}public int? ShiftId{get;set;}public int Priority{get;set;}public int? MinActualMinutes{get;set;}public int? MaxActualMinutes{get;set;}public string? CheckInFrom{get;set;}public string? CheckInTo{get;set;}public DateTime? EffectiveFrom{get;set;}public DateTime? EffectiveTo{get;set;}public string RuleJson{get;set;}=string.Empty;public bool IsActive{get;set;}}
    private sealed class RuleDocument{public int BlockMinutes{get;set;}=15;public string RoundingMode{get;set;}="FLOOR";public string AllocationMode{get;set;}="STANDARD";public int? FixedOtMinutes{get;set;}public List<AttendanceSymbolRuleSegmentDto> Segments{get;set;}=new();}
    private readonly record struct Calculation(int WorkMinutes,int OtMinutes,string? WorkSymbol,string? OtSymbol);
}
