using FVN_REGISTER.Application.Interfaces.Excel;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Core.Excel;
using FVN_REGISTER.Core.Enums;
namespace FVN_REGISTER.Infrastructure.Services.Security;
/// <summary>Endpoint validation over the shared Excel platform; selected schema is the only column mapping source.</summary>
public sealed class EndpointGovernanceExcelImportService
{
 private readonly IExcelPlatform _excel;
 public EndpointGovernanceExcelImportService(IExcelPlatform excel)=>_excel=excel;
 public async Task<EndpointGovernanceExcelPreviewDto> PreviewAsync(Stream stream,string fileName,int schemaId,EndpointGovernanceItemType itemType,EndpointTargetType targetType,CancellationToken ct=default)
 {
  if(stream is null||!stream.CanRead)throw new ArgumentException("Excel stream không hợp lệ.");
  var summary=await _excel.GetSchemaAsync(schemaId,ct)??throw new InvalidOperationException("Không tìm thấy Excel Schema đã chọn.");
  if(!string.Equals(summary.ModuleCode,"ENDPOINT",StringComparison.OrdinalIgnoreCase)||!string.Equals(summary.EntityCode,itemType.ToString(),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Schema không thuộc đúng module/loại Catalog Endpoint đang nhập.");
  if(summary.Status!=ExcelSchemaStatus.Active)throw new InvalidOperationException("Chỉ được import bằng Excel Schema đang hoạt động.");
  var schema=await _excel.GetSchemaDefinitionAsync(schemaId,ct)??throw new InvalidOperationException("Không đọc được định nghĩa Excel Schema.");
  ValidateSchema(schema);if(stream.CanSeek)stream.Position=0;
  var preview=await _excel.PreviewAsync(stream,fileName,schema,ct);
  var errors=preview.Errors.Where(x=>x.Severity==ExcelSeverity.Error).Select(x=>$"Dòng {x.RowNumber}: {x.Message}").ToList();
  var rows=new List<EndpointGovernanceExcelRowDto>();
  foreach(var row in preview.Rows){
   string? Read(string prop){var f=schema.Fields.FirstOrDefault(x=>string.Equals(x.TargetProperty??x.FieldKey,prop,StringComparison.OrdinalIgnoreCase));return f is not null&&row.Cells.TryGetValue(f.SourceColumnIndex,out var v)?v?.Trim():null;}
   var name=Read("Name")??string.Empty;if(string.IsNullOrWhiteSpace(name)){errors.Add($"Dòng {row.RowIndex}: thiếu trường Name.");continue;}
   var raw=Read("IsAllowed");var allowed=true;string? rowError=null;if(!string.IsNullOrWhiteSpace(raw)&&!TryBool(raw,out allowed))rowError=$"IsAllowed không hợp lệ: '{raw}'.";
   if(rowError is not null)errors.Add($"Dòng {row.RowIndex}: {rowError}");
   rows.Add(new(row.RowIndex,name,Empty(Read("Publisher")),Empty(Read("VersionConstraint")),allowed,Empty(Read("Remark")),rowError));
  }
  foreach(var d in rows.Where(x=>string.IsNullOrWhiteSpace(x.Error)).GroupBy(x=>Norm(x.Name)).Where(x=>x.Count()>1).Select(x=>x.Key))errors.Add($"Trùng tên sau chuẩn hóa: {d}.");
  var suggested=Path.GetFileNameWithoutExtension(fileName).Trim();
  return new(fileName,schemaId,itemType,targetType,string.IsNullOrWhiteSpace(suggested)?$"Endpoint {itemType}":suggested,rows.Count,rows,errors.Distinct().ToList());
 }
 public static EndpointGovernancePolicyUpsertRequest ToUpsert(EndpointGovernanceExcelPreviewDto p,EndpointGovernanceExcelImportRequest r){
  if(p.SchemaId!=r.SchemaId)throw new InvalidOperationException("Schema preview không khớp schema import.");
  if(p.Errors.Count>0)throw new InvalidOperationException("Excel còn lỗi, không thể tạo policy version.");
  var items=p.Rows.Select(x=>new EndpointGovernancePolicyItemDto(0,r.ItemType,Norm(x.Name),x.Name.Trim(),x.Publisher,x.VersionConstraint,x.IsAllowed,x.Remark)).ToList();
  return new(r.PolicyCode.Trim(),r.PolicyName.Trim(),r.ItemType,r.TargetType,r.Remark?.Trim(),items);
 }
 private static void ValidateSchema(ExcelSchemaDefinition s){
  var props=s.Fields.Select(x=>x.TargetProperty??x.FieldKey).ToList();
  if(!props.Contains("Name",StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("Schema phải ánh xạ TargetProperty = Name.");
  var allowed=new HashSet<string>(["Name","Publisher","VersionConstraint","IsAllowed","Remark"],StringComparer.OrdinalIgnoreCase);
  var unsupported=props.Where(x=>!allowed.Contains(x)).Distinct().ToArray();if(unsupported.Length>0)throw new InvalidOperationException($"Schema có ánh xạ không hỗ trợ: {string.Join(", ",unsupported)}.");
  if(!s.Fields.Any(x=>string.Equals(x.TargetProperty??x.FieldKey,"Name",StringComparison.OrdinalIgnoreCase)&&x.Required))throw new InvalidOperationException("Trường Name trong schema phải bắt buộc.");
  if(props.GroupBy(x=>x,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new InvalidOperationException("Không được ánh xạ nhiều cột vào cùng một trường Endpoint.");
 }
 private static string Norm(string s)=>s.Trim().ToUpperInvariant(); private static string? Empty(string? s)=>string.IsNullOrWhiteSpace(s)?null:s.Trim();
 private static bool TryBool(string s,out bool b){var n=s.Trim().ToUpperInvariant();if(bool.TryParse(n,out b))return true;if(n is "1" or "Y" or "YES" or "TRUE" or "CÓ" or "CO"){b=true;return true;}if(n is "0" or "N" or "NO" or "FALSE" or "KHÔNG" or "KHONG"){b=false;return true;}b=false;return false;}
}