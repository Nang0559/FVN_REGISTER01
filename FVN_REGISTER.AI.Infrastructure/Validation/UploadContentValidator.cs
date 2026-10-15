using System.Text;
using System.IO.Compression;
using FVN_REGISTER.AI.Application.Abstractions;
namespace FVN_REGISTER.AI.Infrastructure.Validation;
public sealed class UploadContentValidator:IUploadContentValidator
{
 private static readonly HashSet<string> Allowed=new(StringComparer.OrdinalIgnoreCase){".pdf",".xlsx",".docx",".png",".jpg",".jpeg",".csv",".txt"};
 private static readonly UTF8Encoding Utf8=new(false,true);
 public async Task<ValidatedUpload> ValidateAsync(Stream stream,string fileName,CancellationToken ct=default)
 {
  if(string.IsNullOrWhiteSpace(fileName))throw new InvalidDataException("File name required.");
  var safe=Path.GetFileName(fileName.Replace('\\','/')).Trim();var ext=Path.GetExtension(safe);
  if(!Allowed.Contains(ext))throw new InvalidDataException("PDF, XLSX, DOCX, PNG, JPEG, CSV and TXT files are accepted.");
  if(!stream.CanSeek)throw new InvalidOperationException("Upload stream must support seeking.");
  var start=stream.Position;
  try
  {
   var header=new byte[8];var count=await stream.ReadAsync(header.AsMemory(),ct);stream.Position=start;
   if(ext.Equals(".pdf",StringComparison.OrdinalIgnoreCase))
   {if(count<5||!header.AsSpan(0,5).SequenceEqual("%PDF-"u8))throw new InvalidDataException("File signature is not PDF.");return new(safe,"application/pdf",".pdf");}
   if(ext.Equals(".xlsx",StringComparison.OrdinalIgnoreCase))
   {
    if(count<4||header[0]!=0x50||header[1]!=0x4B||header[2] is not(0x03 or 0x05 or 0x07)||header[3] is not(0x04 or 0x06 or 0x08))
     throw new InvalidDataException("File signature is not an XLSX ZIP container.");
    try
    {
     using var archive=new ZipArchive(stream,ZipArchiveMode.Read,true);
     if(archive.Entries.Count > 2000) throw new InvalidDataException("XLSX archive contains too many entries.");
     long expandedLength=0;
     foreach(var entry in archive.Entries)
     {
      if(entry.Length > 25L*1024*1024) throw new InvalidDataException("XLSX archive entry exceeds the allowed expanded size.");
      expandedLength=checked(expandedLength+entry.Length);
      if(expandedLength > 100L*1024*1024) throw new InvalidDataException("XLSX archive expanded size exceeds the allowed limit.");
     }
     var hasContentTypes=archive.GetEntry("[Content_Types].xml") is not null;
     var hasWorkbook=archive.GetEntry("xl/workbook.xml") is not null;
     if(!hasContentTypes||!hasWorkbook)throw new InvalidDataException("ZIP file is not a valid XLSX workbook.");
    }
    catch(InvalidDataException){throw;}
    catch(Exception ex) when(ex is IOException or ArgumentException or OverflowException){throw new InvalidDataException("XLSX archive is corrupt or exceeds safe limits.",ex);}
    return new(safe,"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",".xlsx");
   }
   if(ext.Equals(".docx",StringComparison.OrdinalIgnoreCase))
   {
    if(count<4||header[0]!=0x50||header[1]!=0x4B||header[2] is not(0x03 or 0x05 or 0x07)||header[3] is not(0x04 or 0x06 or 0x08))
     throw new InvalidDataException("File signature is not a DOCX ZIP container.");
    try
    {
     using var archive=new ZipArchive(stream,ZipArchiveMode.Read,true);
     if(archive.Entries.Count>2000) throw new InvalidDataException("DOCX archive contains too many entries.");
     long expanded=0;
     foreach(var entry in archive.Entries)
     {
      if(entry.Length>25L*1024*1024) throw new InvalidDataException("DOCX archive entry exceeds the allowed expanded size.");
      expanded=checked(expanded+entry.Length);
      if(expanded>100L*1024*1024) throw new InvalidDataException("DOCX archive expanded size exceeds the allowed limit.");
     }
     if(archive.GetEntry("[Content_Types].xml") is null||archive.GetEntry("word/document.xml") is null)
      throw new InvalidDataException("ZIP file is not a valid DOCX document.");
    }
    catch(InvalidDataException){throw;}
    catch(Exception ex) when(ex is IOException or ArgumentException or OverflowException)
    {throw new InvalidDataException("DOCX archive is corrupt or exceeds safe limits.",ex);}
    return new(safe,"application/vnd.openxmlformats-officedocument.wordprocessingml.document",".docx");
   }
   if(ext.Equals(".png",StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpg",StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg",StringComparison.OrdinalIgnoreCase))
   {
    var isPng=count>=8&&header.AsSpan(0,8).SequenceEqual(new byte[]{0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A});
    var isJpeg=count>=3&&header[0]==0xFF&&header[1]==0xD8&&header[2]==0xFF;
    if(ext.Equals(".png",StringComparison.OrdinalIgnoreCase)&&!isPng)throw new InvalidDataException("File signature is not PNG.");
    if((ext.Equals(".jpg",StringComparison.OrdinalIgnoreCase)||ext.Equals(".jpeg",StringComparison.OrdinalIgnoreCase))&&!isJpeg)throw new InvalidDataException("File signature is not JPEG.");
    return new(safe,isPng?"image/png":"image/jpeg",ext.ToLowerInvariant());
   }
   var sample=new byte[(int)Math.Min(4096,stream.Length-start)];var n=await stream.ReadAsync(sample.AsMemory(),ct);stream.Position=start;
   if(n==0||sample.AsSpan(0,n).Contains((byte)0))throw new InvalidDataException("Text file is empty or contains binary data.");
   try{var text=Utf8.GetString(sample,0,n);if(text.Count(c=>char.IsControl(c)&&c is not('\r' or '\n' or '\t' or '\f'))>Math.Max(2,text.Length/100))throw new InvalidDataException("Text file contains binary control characters.");}
   catch(DecoderFallbackException ex){throw new InvalidDataException("CSV/TXT must be UTF-8 encoded.",ex);}
   return new(safe,ext.Equals(".csv",StringComparison.OrdinalIgnoreCase)?"text/csv":"text/plain",ext.ToLowerInvariant());
  }
  finally{stream.Position=start;}
 }
}