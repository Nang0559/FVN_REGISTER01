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
            await ReapplyCurrentAndMissingHistorySymbolsAsync(ct);
            var rows = await LoadRowsAsync(false, ct);
            var saved = rows.FirstOrDefault(x => string.Equals(x.RuleCode,request.RuleCode.Trim(),StringComparison.OrdinalIgnoreCase));
            return saved is null ? ServiceResult<AttendanceSymbolRuleDto>.Fail("Không tìm thấy rule vừa lưu.") : ServiceResult<AttendanceSymbolRuleDto>.Ok(ToDto(saved));
        }
        catch (Exception ex) { return ServiceResult<AttendanceSymbolRuleDto>.Fail(ex.Message); }
    }

    public async Task<ServiceResult<object>> DeactivateAsync(int id,int actorUserId,CancellationToken ct=default)
    {
        try { await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.F03AttendanceSymbolRules SET IsActive=0,ModifiedBy={actorUserId},ModifiedAt=SYSUTCDATETIME(),LastModifiedSource=N'UI' WHERE Id={id}",ct); _activeRows=null; await ReapplyCurrentAndMissingHistorySymbolsAsync(ct); return ServiceResult<object>.Ok(new{id,active=false}); }
        catch(Exception ex){return ServiceResult<object>.Fail(ex.Message);}
    }

    public Task<AttendanceSymbolRuleTestResultDto> TestAsync(AttendanceSymbolRuleTestRequest request,CancellationToken ct=default)
        => EvaluateAsync(request.WorkDate,request.DayType,request.ShiftCode,request.ShiftId,request.CheckIn,request.CheckOut,request.ActualOtMinutes,ct);

    public Task RefreshSymbolsAsync(CancellationToken ct = default)
        => ReapplyCurrentAndMissingHistorySymbolsAsync(ct);

    public async Task<AttendanceSymbolRuleTestResultDto> EvaluateAsync(DateOnly workDate,string dayType,string? shiftCode,int? shiftId,DateTime? checkIn,DateTime? checkOut,int? actualOtMinutes=null,CancellationToken ct=default)
    {
        // Ca đêm: nếu giờ ra bị ghi cùng ngày với giờ vào (06:02 < 21:51) thì giờ ra thật là ngày hôm sau.
        if(checkIn.HasValue&&checkOut.HasValue&&checkOut<=checkIn&&(checkOut.Value.AddDays(1)-checkIn.Value).TotalHours<=20)checkOut=checkOut.Value.AddDays(1);
        if(!checkIn.HasValue||!checkOut.HasValue||checkOut<=checkIn)return new(){DayType=dayType,Explanation="Thiếu In/Out hợp lệ."};
        var elapsed=(int)Math.Round((checkOut.Value-checkIn.Value).TotalMinutes,MidpointRounding.AwayFromZero); var normalizedShift=NormalizeShift(shiftCode); var rows=await LoadRowsAsync(true,ct);
        var rule=rows.Where(x=>string.Equals(x.DayType,dayType,StringComparison.OrdinalIgnoreCase))
            .Where(x=>x.ShiftId is null||shiftId is null||x.ShiftId==shiftId)
            .Where(x=>string.IsNullOrWhiteSpace(x.ShiftCode)||string.Equals(NormalizeShift(x.ShiftCode),normalizedShift,StringComparison.OrdinalIgnoreCase))
            .Where(x=>!x.MinActualMinutes.HasValue||elapsed>=x.MinActualMinutes.Value).Where(x=>!x.MaxActualMinutes.HasValue||elapsed<=x.MaxActualMinutes.Value)
            .Where(x=>InTimeMatches(checkIn.Value.TimeOfDay,x.CheckInFrom,x.CheckInTo)).Where(x=>EffectiveDateMatches(workDate,x.EffectiveFrom,x.EffectiveTo))
            .OrderBy(x=>x.Priority).ThenByDescending(x=>x.ShiftId.HasValue).ThenByDescending(x=>!string.IsNullOrWhiteSpace(x.ShiftCode)).FirstOrDefault();
        if(rule is null)return new(){Matched=false,DayType=dayType,RawElapsedMinutes=elapsed,Explanation=DescribeNoMatch(rows,workDate,dayType,normalizedShift,shiftId,checkIn.Value,elapsed)};
        var doc=Parse(rule.RuleJson);var result=Calculate(doc,workDate,checkIn.Value,checkOut.Value,elapsed);
        return new(){Matched=true,RuleCode=rule.RuleCode,DayType=rule.DayType,WorkSymbol=result.WorkSymbol,OtSymbol=result.OtSymbol,WorkMinutesForSymbol=result.WorkMinutes,OtMinutesForSymbol=result.OtMinutes,RawElapsedMinutes=elapsed,RoundedOtMinutes=result.OtMinutes,Explanation=$"Rule {rule.RuleCode}: công {result.WorkMinutes/60m:0.##}h, OT {result.OtMinutes/60m:0.##}h theo block {doc.BlockMinutes} phút / {doc.RoundingMode}."};
    }

    /// <summary>
    /// Re-evaluate current attendance whenever a rule is changed. Archived rows are
    /// intentionally backfilled only when their symbol snapshot is missing; an
    /// already-populated locked-period symbol remains an immutable historical result.
    /// </summary>
    private async Task ReapplyCurrentAndMissingHistorySymbolsAsync(CancellationToken ct)
    {
        var inputs = await _db.Database.SqlQuery<ReapplyInput>($"""
            SELECT N'CURRENT' AS Store, a.Id AS RowId, a.WorkDate, a.ShiftId, a.ShiftAbbr,
                   a.CheckInTime, a.CheckOutTime, ds.DayType AS DayType
            FROM dbo.F03HrmAttendanceCalculated AS a
            CROSS APPLY dbo.fn_ResolveDayType(a.WorkDate, a.HrmBCNgayLe, a.HrmBCNgayLeNV) AS ds
            WHERE a.WorkDate >= DATEADD(MONTH, -12, CONVERT(date, GETDATE()))
              AND a.CheckInTime IS NOT NULL AND a.CheckOutTime IS NOT NULL
            UNION ALL
            SELECT N'HISTORY' AS Store, h.HistoryId AS RowId, h.WorkDate, h.ShiftId, h.ShiftAbbr,
                   h.CheckInTime, h.CheckOutTime, COALESCE(h.DayType, ds.DayType) AS DayType
            FROM dbo.F03HrmAttendanceHistory AS h
            CROSS APPLY dbo.fn_ResolveDayType(h.WorkDate, h.HrmBCNgayLe, h.HrmBCNgayLeNV) AS ds
            WHERE h.SymbolRuleCode IS NULL
              AND h.WorkDate >= DATEADD(MONTH, -12, CONVERT(date, GETDATE()))
              AND h.CheckInTime IS NOT NULL AND h.CheckOutTime IS NOT NULL
            """).ToListAsync(ct);

        if (inputs.Count == 0) return;

        var outputs = new List<ReapplyOutput>(inputs.Count);
        foreach (var row in inputs)
        {
            ct.ThrowIfCancellationRequested();
            var dayType = row.DayType!;
            var evaluated = await EvaluateAsync(
                DateOnly.FromDateTime(row.WorkDate),
                dayType,
                row.ShiftAbbr,
                row.ShiftId,
                row.CheckInTime,
                row.CheckOutTime,
                null,
                ct);

            outputs.Add(new ReapplyOutput(
                row.Store,
                row.RowId,
                dayType,
                evaluated.Matched ? evaluated.WorkSymbol : null,
                evaluated.Matched ? evaluated.OtSymbol : null,
                evaluated.Matched ? evaluated.RuleCode : null,
                row.WorkDate));
        }

        var previousTimeout = _db.Database.GetCommandTimeout();
        _db.Database.SetCommandTimeout(600);
        try
        {
        foreach (var storeGroup in outputs.GroupBy(x => x.Store))
        {
            foreach (var chunk in storeGroup.Chunk(500))
            {
                var json = JsonSerializer.Serialize(chunk.Select(x => new
                {
                    id = x.RowId, wd = x.WorkDate.ToString("yyyy-MM-dd"), dt = x.DayType, ws = x.WorkSymbol, os = x.OtSymbol, rc = x.RuleCode
                }));

                if (storeGroup.Key == "CURRENT")
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE a
                           SET a.DayType = j.dt,
                               a.WorkSymbol = j.ws,
                               a.OtSymbol = j.os,
                               a.SymbolRuleCode = j.rc,
                               a.OtDisplayValue = COALESCE(j.os, a.OtDisplayValue),
                               a.AttendanceDisplayValue =
                                   CASE WHEN j.ws IS NOT NULL
                                             AND ISNULL(a.LeaveTotal, 0) = 0
                                             AND (j.dt <> N'NORMAL'
                                                  OR ISNULL(a.AttendanceDisplayValue, N'') = N''
                                                  OR a.AttendanceDisplayValue = a.ShiftAbbr)
                                        THEN j.ws ELSE a.AttendanceDisplayValue END
                        FROM dbo.F03HrmAttendanceCalculated AS a WITH (ROWLOCK)
                        INNER JOIN OPENJSON({json})
                            WITH (id bigint '$.id', dt nvarchar(40) '$.dt', ws nvarchar(50) '$.ws',
                                  os nvarchar(50) '$.os', rc nvarchar(100) '$.rc') AS j
                            ON j.id = a.Id
                        """, ct);
                }
                else
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE h
                           SET h.DayType = j.dt,
                               h.WorkSymbol = j.ws,
                               h.OtSymbol = j.os,
                               h.SymbolRuleCode = j.rc,
                               h.OtDisplayValue = COALESCE(j.os, h.OtDisplayValue),
                               h.AttendanceDisplayValue =
                                   CASE WHEN j.ws IS NOT NULL
                                             AND ISNULL(h.LeaveTotal, 0) = 0
                                             AND (j.dt <> N'NORMAL'
                                                  OR ISNULL(h.AttendanceDisplayValue, N'') = N''
                                                  OR h.AttendanceDisplayValue = h.ShiftAbbr)
                                        THEN j.ws ELSE h.AttendanceDisplayValue END
                        FROM dbo.F03HrmAttendanceHistory AS h WITH (ROWLOCK)
                        INNER JOIN OPENJSON({json})
                            WITH (id bigint '$.id', wd date '$.wd', dt nvarchar(40) '$.dt', ws nvarchar(50) '$.ws',
                                  os nvarchar(50) '$.os', rc nvarchar(100) '$.rc') AS j
                            ON h.WorkDate = j.wd AND h.HistoryId = j.id
                        WHERE h.SymbolRuleCode IS NULL
                        """, ct);
                }
            }
        }
        }
        finally
        {
            _db.Database.SetCommandTimeout(previousTimeout);
        }
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
    private static string? Normalize(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();private static string NormalizeShift(string? value)
    {
        var key=new string((value??string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        // "CA3" (Ca 3) và "C3" là cùng một ca: bỏ chữ A thừa của tiền tố "CA".
        return key.Length>2&&key.StartsWith("CA",StringComparison.Ordinal)?"C"+key[2..]:key;
    }

    /// <summary>Cho biết bước lọc nào loại hết rule, để màn hình Thử rule và log chỉ ra lý do thay vì chỉ báo không khớp.</summary>
    private static string DescribeNoMatch(List<RuleRow> rows,DateOnly workDate,string dayType,string normalizedShift,int? shiftId,DateTime checkIn,int elapsed)
    {
        var byDay=rows.Where(x=>string.Equals(x.DayType,dayType,StringComparison.OrdinalIgnoreCase)).ToList();
        if(byDay.Count==0)return $"Không có rule đang bật cho loại ngày {dayType}.";
        var byShift=byDay.Where(x=>x.ShiftId is null||shiftId is null||x.ShiftId==shiftId)
            .Where(x=>string.IsNullOrWhiteSpace(x.ShiftCode)||string.Equals(NormalizeShift(x.ShiftCode),normalizedShift,StringComparison.OrdinalIgnoreCase)).ToList();
        if(byShift.Count==0)return $"Có {byDay.Count} rule loại ngày {dayType} nhưng không rule nào cho ca '{normalizedShift}' (ca đang có: {string.Join(", ",byDay.Select(x=>x.ShiftCode??"(mọi ca)").Distinct())}).";
        var byElapsed=byShift.Where(x=>!x.MinActualMinutes.HasValue||elapsed>=x.MinActualMinutes.Value).Where(x=>!x.MaxActualMinutes.HasValue||elapsed<=x.MaxActualMinutes.Value).ToList();
        if(byElapsed.Count==0)return $"Rule {dayType}/{normalizedShift} có giới hạn số phút thực tế không chứa {elapsed} phút (In→Out).";
        var byIn=byElapsed.Where(x=>InTimeMatches(checkIn.TimeOfDay,x.CheckInFrom,x.CheckInTo)).ToList();
        if(byIn.Count==0)return $"Giờ vào {checkIn:HH:mm} nằm ngoài khung giờ vào của rule {dayType}/{normalizedShift}.";
        return $"Rule {dayType}/{normalizedShift} không còn hiệu lực ở ngày {workDate:yyyy-MM-dd} (EffectiveFrom/EffectiveTo).";
    }
    private static void Validate(AttendanceSymbolRuleUpsertRequest request){var dayType=request.DayType?.Trim().ToUpperInvariant();if(string.IsNullOrEmpty(dayType)||!FVN_REGISTER.Contract.Dtos.Calendar.DayTypeCatalog.Codes.Contains(dayType))throw new ArgumentException($"Loại ngày '{request.DayType}' không hợp lệ. Chỉ chấp nhận: {string.Join(", ",FVN_REGISTER.Contract.Dtos.Calendar.DayTypeCatalog.Codes)}.");if(string.IsNullOrWhiteSpace(request.RuleCode))throw new ArgumentException("RuleCode là bắt buộc.");if(string.IsNullOrWhiteSpace(request.RuleName))throw new ArgumentException("RuleName là bắt buộc.");if(request.BlockMinutes<=0)throw new ArgumentException("BlockMinutes phải lớn hơn 0.");if(request.Segments.Count==0)throw new ArgumentException("Rule phải có ít nhất một segment.");if(request.Segments.Any(x=>!TimeSpan.TryParse(x.Start,CultureInfo.InvariantCulture,out _)||!TimeSpan.TryParse(x.End,CultureInfo.InvariantCulture,out _)))throw new ArgumentException("Giờ segment không hợp lệ.");}
    private sealed class ReapplyInput
    {
        public string Store { get; set; } = string.Empty;
        public long RowId { get; set; }
        public DateTime WorkDate { get; set; }
        public int? ShiftId { get; set; }
        public string? ShiftAbbr { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string? DayType { get; set; }
    }
    private sealed record ReapplyOutput(string Store, long RowId, string DayType, string? WorkSymbol, string? OtSymbol, string? RuleCode, DateTime WorkDate);
    private sealed class RuleRow{public int Id{get;set;}public string RuleCode{get;set;}=string.Empty;public string RuleName{get;set;}=string.Empty;public string DayType{get;set;}=string.Empty;public string? ShiftCode{get;set;}public int? ShiftId{get;set;}public int Priority{get;set;}public int? MinActualMinutes{get;set;}public int? MaxActualMinutes{get;set;}public string? CheckInFrom{get;set;}public string? CheckInTo{get;set;}public DateTime? EffectiveFrom{get;set;}public DateTime? EffectiveTo{get;set;}public string RuleJson{get;set;}=string.Empty;public bool IsActive{get;set;}}
    private sealed class RuleDocument{public int BlockMinutes{get;set;}=15;public string RoundingMode{get;set;}="FLOOR";public string AllocationMode{get;set;}="STANDARD";public int? FixedOtMinutes{get;set;}public List<AttendanceSymbolRuleSegmentDto> Segments{get;set;}=new();}
    private readonly record struct Calculation(int WorkMinutes,int OtMinutes,string? WorkSymbol,string? OtSymbol);
}
