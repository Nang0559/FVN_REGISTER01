using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using FVN_REGISTER.AI.Application.Abstractions;
using UglyToad.PdfPig;

namespace FVN_REGISTER.AI.Infrastructure.Extraction;

/// <summary>
/// Local deterministic extractor for UTF-8 TXT/CSV, XLSX OpenXML and text-based PDF files.
/// Image-only/scanned PDF requires a separately configured OCR service.
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
                using var reader = new StreamReader(content, StrictUtf8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                return await reader.ReadToEndAsync(cancellationToken);
            }

            if (mediaType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase))
                return ExtractXlsx(content);

            if (mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                return ExtractPdf(content);

            throw new NotSupportedException($"No local text extractor is configured for media type '{mediaType}'.");
        }
        finally { content.Position = start; }
    }

    private static string ExtractPdf(Stream content)
    {
        using var document = PdfDocument.Open(content);
        if (document.NumberOfPages > 500)
            throw new InvalidDataException("PDF exceeds the 500-page extraction limit.");

        var output = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            output.AppendLine($"[PAGE {page.Number}]");
            output.AppendLine(page.Text);
            if (output.Length > 2_000_000)
                throw new InvalidDataException("PDF extracted text exceeds the 2 MB limit.");
        }

        var text = output.ToString();
        if (string.IsNullOrWhiteSpace(text))
            throw new NotSupportedException("PDF has no embedded text. Scanned/image-only PDF requires the approved OCR service.");
        return text;
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
