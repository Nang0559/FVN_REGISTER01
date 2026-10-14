using System.Text;
using FVN_REGISTER.AI.Application.Abstractions;
namespace FVN_REGISTER.AI.Infrastructure.Validation;
public sealed class UploadContentValidator:IUploadContentValidator
{
 private static readonly HashSet<string> Allowed=new(StringComparer.OrdinalIgnoreCase){".pdf",".xlsx",".csv",".txt"};
 private static readonly UTF8Encoding Utf8=new(false,true);
 public async Task<ValidatedUpload> ValidateAsync(Stream stream,string fileName,CancellationToken ct=default)
 {
  if(string.IsNullOrWhiteSpace(fileName))throw new InvalidDataException("File name required.");
  var safe=Path.GetFileName(fileName.Replace('\\','/')).Trim();var ext=Path.GetExtension(safe);
  if(!Allowed.Contains(ext))throw new InvalidDataException("Only PDF, XLSX, CSV and TXT files are accepted.");
  if(!stream.CanSeek)throw new InvalidOperationException("Upload stream must support seeking.");
  var start=stream.Position;
  try
  {
   var header=new byte[8];var count=await stream.ReadAsync(header.AsMemory(),ct);stream.Position=start;
   if(ext.Equals(".pdf",StringComparison.OrdinalIgnoreCase))
   {if(count<5||!header.AsSpan(0,5).SequenceEqual("%PDF-"u8))throw new InvalidDataException("File signature is not PDF.");return new(safe,"application/pdf",".pdf");}
   if(ext.Equals(".xlsx",StringComparison.OrdinalIgnoreCase))
   {if(count<4||header[0]!=0x50||header[1]!=0x4B||header[2] is not(0x03 or 0x05 or 0x07)||header[3] is not(0x04 or 0x06 or 0x08))throw new InvalidDataException("File signature is not an XLSX ZIP container.");return new(safe,"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",".xlsx");}
   var sample=new byte[(int)Math.Min(4096,stream.Length-start)];var n=await stream.ReadAsync(sample.AsMemory(),ct);stream.Position=start;
   if(n==0||sample.AsSpan(0,n).Contains((byte)0))throw new InvalidDataException("Text file is empty or contains binary data.");
   try{var text=Utf8.GetString(sample,0,n);if(text.Count(c=>char.IsControl(c)&&c is not('\r' or '\n' or '\t' or '\f'))>Math.Max(2,text.Length/100))throw new InvalidDataException("Text file contains binary control characters.");}
   catch(DecoderFallbackException ex){throw new InvalidDataException("CSV/TXT must be UTF-8 encoded.",ex);}
   return new(safe,ext.Equals(".csv",StringComparison.OrdinalIgnoreCase)?"text/csv":"text/plain",ext.ToLowerInvariant());
  }
  finally{stream.Position=start;}
 }
}