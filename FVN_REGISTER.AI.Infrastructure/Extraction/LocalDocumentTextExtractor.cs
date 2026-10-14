using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using FVN_REGISTER.AI.Application.Abstractions;

namespace FVN_REGISTER.AI.Infrastructure.Extraction;

/// <summary>
/// Local deterministic extractor for UTF-8 TXT/CSV and XLSX OpenXML text.
/// PDF/OCR is deliberately delegated to an approved provider adapter.
/// </summary>
public sealed class LocalDocumentTextExtractor : IDocumentTextExtractor
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<string> ExtractTextAsync(Stream content, string mediaType, CancellationToken cancellationToken = default)
    {
        if (!content.CanSeek) throw new InvalidOperationException("Extraction requires a seekable document stream.");
        var start = content.Position;
        try
        {
            if (mediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
                mediaType.Equals("text/csv", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(content, StrictUtf8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                return await reader.ReadToEndAsync(cancellationToken);
            }

            if (mediaType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase))
                return ExtractXlsx(content);

            throw new NotSupportedException($"No local text extractor is configured for media type '{mediaType}'. Configure an approved PDF/OCR provider.");
        }
        finally { content.Position = start; }
    }

    private static string ExtractXlsx(Stream content)
    {
        using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        var shared = new List<string>();
        var sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedEntry is not null)
        {
            using var stream = sharedEntry.Open();
            var xml = XDocument.Load(stream);
            shared.AddRange(xml.Descendants().Where(x => x.Name.LocalName == "si")
                .Select(si => string.Concat(si.Descendants().Where(x => x.Name.LocalName == "t").Select(t => t.Value))));
        }

        var sheets = archive.Entries
            .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase);
        var output = new StringBuilder();
        foreach (var sheet in sheets)
        {
            using var stream = sheet.Open();
            var xml = XDocument.Load(stream);
            foreach (var row in xml.Descendants().Where(x => x.Name.LocalName == "row"))
            {
                var cells = new List<string>();
                foreach (var cell in row.Elements().Where(x => x.Name.LocalName == "c"))
                {
                    var type = (string?)cell.Attribute("t");
                    var value = cell.Elements().FirstOrDefault(x => x.Name.LocalName == "v")?.Value ?? "";
                    if (type == "s" && int.TryParse(value, out var index) && index >= 0 && index < shared.Count)
                        value = shared[index];
                    else if (type == "inlineStr")
                        value = string.Concat(cell.Descendants().Where(x => x.Name.LocalName == "t").Select(x => x.Value));
                    cells.Add(value.Replace("\r", " ").Replace("\n", " "));
                }
                output.AppendLine(string.Join("\t", cells));
            }
        }
        return output.ToString();
    }
}
