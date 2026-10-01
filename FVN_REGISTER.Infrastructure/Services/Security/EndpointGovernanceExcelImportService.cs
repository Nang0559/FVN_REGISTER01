using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Core.Excel;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Infrastructure.Services.Excel;

namespace FVN_REGISTER.Infrastructure.Services.Security;

public sealed class EndpointGovernanceExcelImportService
{
    private static readonly string[] NameHeaders=["Name","SoftwareName","ServiceName","Tên","Tên phần mềm","Tên dịch vụ","NormalizedName"];
    private static readonly string[] PublisherHeaders=["Publisher","Nhà cung cấp","Manufacturer"];
    private static readonly string[] VersionHeaders=["VersionConstraint","Version","AllowedVersion","Phiên bản","Điều kiện phiên bản"];
    private static readonly string[] AllowedHeaders=["IsAllowed","Allowed","Allow","Được phép","Cho phép"];
    private static readonly string[] RemarkHeaders=["Remark","Note","Ghi chú","Description","Mô tả"];
    private readonly ExcelPlatform _excel;
    public EndpointGovernanceExcelImportService(ExcelPlatform excel)=>_excel=excel;

    public async Task<EndpointGovernanceExcelPreviewDto> PreviewAsync(Stream stream,string fileName,EndpointGovernanceItemType itemType,EndpointTargetType targetType,CancellationToken ct=default)
    {
        if(stream is null||!stream.CanRead)throw new ArgumentException("Excel stream không hợp lệ.");
        var inspection=await _excel.InspectAsync(stream,fileName,ct);
        if(inspection.Sheets.Count==0)return new(fileName,itemType,targetType,Path.GetFileNameWithoutExtension(fileName),0,[],["File Excel không có worksheet."]);
        var sheet=inspection.Sheets[0]; var candidate=sheet.HeaderCandidates.OrderByDescending(x=>x.Score).FirstOrDefault();
        if(candidate is null)return new(fileName,itemType,targetType,Path.GetFileNameWithoutExtension(fileName),0,[],["Worksheet không có dòng tiêu đề."]);
        var fields=new List<ExcelSchemaField>();
        AddField(fields,"Name",candidate.Values,NameHeaders,true);
        AddField(fields,"Publisher",candidate.Values,PublisherHeaders,false);
        AddField(fields,"VersionConstraint",candidate.Values,VersionHeaders,false);
        AddField(fields,"IsAllowed",candidate.Values,AllowedHeaders,false);
        AddField(fields,"Remark",candidate.Values,RemarkHeaders,false);
        if(!fields.Any(x=>x.FieldKey=="Name"))return new(fileName,itemType,targetType,Path.GetFileNameWithoutExtension(fileName),0,[],["Không tìm thấy cột Name/Tên trong dòng tiêu đề."]);
        var schema=new ExcelSchemaDefinition("ENDPOINT",itemType.ToString(),"ENDPOINT_GOVERNANCE",1,sheet.Index,sheet.Name,candidate.RowIndex,candidate.RowIndex+1,sheet.LastRowIndex,fields.Select(x=>x.SourceColumnIndex).Distinct().ToArray(),fields);
        await using var copy=new MemoryStream();
        if(stream.CanSeek)stream.Position=0; await stream.CopyToAsync(copy,ct); copy.Position=0;
        var preview=await _excel.PreviewAsync(copy,fileName,schema,ct);
        var rows=new List<EndpointGovernanceExcelRowDto>(); var errors=preview.Errors.Select(x=>$"Dòng {x.RowNumber}: {x.Message}").ToList();
        foreach(var row in preview.Rows)
        {
            string? Get(string key){var f=fields.FirstOrDefault(x=>x.FieldKey==key);return f is null?null:row.Cells.TryGetValue(f.SourceColumnIndex,out var v)?v:null;}
            var name=Get("Name")?.Trim()??string.Empty; if(string.IsNullOrWhiteSpace(name))continue;
            var allowed=true; var rawAllowed=Get("IsAllowed"); string? rowError=null;
            if(!string.IsNullOrWhiteSpace(rawAllowed)&&!TryParseBoolean(rawAllowed, out allowed))rowError=$"IsAllowed không hợp lệ: '{rawAllowed}'. Dùng true/false, 1/0, Yes/No hoặc Có/Không.";
            rows.Add(new(row.RowIndex,name,NullIfEmpty(Get("Publisher")),NullIfEmpty(Get("VersionConstraint")),allowed,NullIfEmpty(Get("Remark")),rowError));
            if(rowError!=null)errors.Add($"Dòng {row.RowIndex}: {rowError}");
        }
        var duplicate=rows.Where(x=>string.IsNullOrWhiteSpace(x.Error)).GroupBy(x=>NormalizeName(x.Name)).Where(g=>g.Count()>1).Select(g=>g.Key);
        errors.AddRange(duplicate.Select(x=>$"Trùng tên sau chuẩn hóa: {x}."));
        var suggested=Path.GetFileNameWithoutExtension(fileName).Trim();
        return new(fileName,itemType,targetType,string.IsNullOrWhiteSpace(suggested)?$"Endpoint {itemType}":suggested,rows.Count,rows,errors.Distinct().ToList());
    }

    public static EndpointGovernancePolicyUpsertRequest ToUpsert(EndpointGovernanceExcelPreviewDto preview,EndpointGovernanceExcelImportRequest request)
    {
        if(preview.Errors.Count>0)throw new InvalidOperationException("Excel còn lỗi, không thể tạo policy version.");
        var items=preview.Rows.Select(x=>new EndpointGovernancePolicyItemDto(0,request.ItemType,NormalizeName(x.Name),x.Name.Trim(),x.Publisher,x.VersionConstraint,x.IsAllowed,x.Remark)).ToList();
        return new(request.PolicyCode.Trim(),request.PolicyName.Trim(),request.ItemType,request.TargetType,request.Remark?.Trim(),items);
    }
    private static void AddField(List<ExcelSchemaField> fields,string key,IReadOnlyList<string?> headers,IEnumerable<string> candidates,bool required){for(var i=0;i<headers.Count;i++){var h=NormalizeHeader(headers[i]??string.Empty);if(candidates.Any(x=>NormalizeHeader(x)==h)){fields.Add(new ExcelSchemaField(key,ExcelFieldDataType.Text.ToString(),required,i,headers[i],TargetProperty:key));return;}}}
    private static string NormalizeHeader(string value)=>value.Trim().Replace(" ",string.Empty).Replace("_",string.Empty).ToUpperInvariant();
    private static string NormalizeName(string value)=>value.Trim().ToUpperInvariant();
    private static string? NullIfEmpty(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static bool TryParseBoolean(string value,out bool result){if(bool.TryParse(value,out result))return true;if(value is "1" or "Y" or "YES" or "TRUE" or "Có" or "CO"){result=true;return true;}if(value is "0" or "N" or "NO" or "FALSE" or "Không" or "KHONG"){result=false;return true;}result=false;return false;}
}
