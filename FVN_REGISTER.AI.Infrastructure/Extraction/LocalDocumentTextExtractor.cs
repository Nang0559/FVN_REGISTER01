using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FVN_REGISTER.AI.Application.Abstractions;
using UglyToad.PdfPig;

namespace FVN_REGISTER.AI.Infrastructure.Extraction;

/// <summary>Deterministic text extraction for common office formats plus OCR fallback for images/scanned PDFs.</summary>
public sealed class LocalDocumentTextExtractor(IOcrTextExtractor ocr) : IDocumentTextExtractor
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<string> ExtractTextAsync(Stream content, string mediaType, CancellationToken cancellationToken = default)
    {
        if (!content.CanSeek) throw new InvalidOperationException("Extraction requires a seekable document stream.");
        var start = content.Position;
        try
        {
            content.Position = 0;
            if (mediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
                mediaType.Equals("text/csv", StringComparison.OrdinalIgnoreCase) ||
                mediaType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(content, StrictUtf8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                return await reader.ReadToEndAsync(cancellationToken);
            }
            if (mediaType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase)) return ExtractXlsx(content);
            if (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)) return ExtractJson(content);
            if (mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase)) return ExtractXml(content);
            if (mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase)) return await ExtractHtmlAsync(content, cancellationToken);
            if (mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                try { return ExtractPdf(content); }
                catch (NotSupportedException)
                {
                    content.Position = start;
                    return "[OCR]\n" + await ocr.ExtractTextAsync(content, mediaType, cancellationToken);
                }
            }
            if (mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase) || mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
                return "[OCR]\n" + await ocr.ExtractTextAsync(content, mediaType, cancellationToken);
            if (mediaType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase)) return ExtractDocx(content);
            throw new NotSupportedException($"No local text extractor is configured for media type '{mediaType}'.");
        }
        finally { content.Position = start; }
    }

    private static string ExtractJson(Stream content)
    {
        using var document = JsonDocument.Parse(content);
        var output = new StringBuilder();
        Append(document.RootElement, "$");
        if (output.Length > 2_000_000) throw new InvalidDataException("JSON extracted text exceeds the 2 MB limit.");
        return output.ToString();
        void Append(JsonElement element, string path)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object: foreach (var property in element.EnumerateObject()) Append(property.Value, path + "." + property.Name); break;
                case JsonValueKind.Array: var index = 0; foreach (var item in element.EnumerateArray()) Append(item, $"{path}[{index++}]"); break;
                case JsonValueKind.String: output.Append(path).Append(" = ").AppendLine(element.GetString()); break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False: output.Append(path).Append(" = ").AppendLine(element.ToString()); break;
                case JsonValueKind.Null: output.Append(path).AppendLine(" = null"); break;
            }
        }
    }

    private static string ExtractXml(Stream content)
    {
        var document = XDocument.Load(content); var output = new StringBuilder();
        if (document.Root is null) return "";
        Append(document.Root, "/" + document.Root.Name.LocalName);
        if (output.Length > 2_000_000) throw new InvalidDataException("XML extracted text exceeds the 2 MB limit.");
        return output.ToString();
        void Append(XElement element, string path)
        {
            foreach (var attribute in element.Attributes()) output.Append(path).Append("/@").Append(attribute.Name.LocalName).Append(" = ").AppendLine(attribute.Value);
            var children = element.Elements().ToArray();
            if (children.Length == 0 && !string.IsNullOrWhiteSpace(element.Value)) output.Append(path).Append(" = ").AppendLine(element.Value.Trim());
            foreach (var child in children) Append(child, path + "/" + child.Name.LocalName);
        }
    }

    private static async Task<string> ExtractHtmlAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, StrictUtf8, true, 4096, leaveOpen: true);
        var html = await reader.ReadToEndAsync(cancellationToken);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"(?is)<(script|style)\b[^>]*>.*?</\1\s*>", " ", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        html = System.Text.RegularExpressions.Regex.Replace(html, @"(?s)<!--.*?-->|<[^>]+>", " ", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        var text = System.Net.WebUtility.HtmlDecode(html);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)).Trim();
        if (text.Length > 2_000_000) throw new InvalidDataException("HTML extracted text exceeds the 2 MB limit.");
        return text;
    }

    private static string ExtractDocx(Stream content)
    {
        using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("DOCX document body is missing.");
        var output = new StringBuilder();
        using var stream = entry.Open(); var xml = XDocument.Load(stream);
        foreach (var paragraph in xml.Descendants().Where(x => x.Name.LocalName == "p"))
        {
            var text = string.Concat(paragraph.Descendants().Where(x => x.Name.LocalName == "t").Select(x => x.Value));
            if (!string.IsNullOrWhiteSpace(text)) output.AppendLine(text);
        }
        if (output.Length > 2_000_000) throw new InvalidDataException("DOCX extracted text exceeds the 2 MB limit.");
        return output.ToString();
    }

    private static string ExtractPdf(Stream content)
    {
        using var document = PdfDocument.Open(content);
        if (document.NumberOfPages > 500) throw new InvalidDataException("PDF exceeds the 500-page extraction limit.");
        var output = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            output.AppendLine($"[PAGE {page.Number}]"); output.AppendLine(page.Text);
            if (output.Length > 2_000_000) throw new InvalidDataException("PDF extracted text exceeds the 2 MB limit.");
        }
        var text = output.ToString();
        if (string.IsNullOrWhiteSpace(text)) throw new NotSupportedException("PDF has no embedded text. Scanned/image-only PDF requires the approved OCR service.");
        return text;
    }

    private static string ExtractXlsx(Stream content)
    {
        using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        var shared = new List<string>(); var sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedEntry is not null)
        {
            using var stream = sharedEntry.Open(); var xml = XDocument.Load(stream);
            shared.AddRange(xml.Descendants().Where(x => x.Name.LocalName == "si").Select(si => string.Concat(si.Descendants().Where(x => x.Name.LocalName == "t").Select(x => x.Value))));
        }
        var sheets = archive.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase);
        var output = new StringBuilder();
        foreach (var sheet in sheets)
        {
            using var stream = sheet.Open(); var xml = XDocument.Load(stream);
            foreach (var row in xml.Descendants().Where(x => x.Name.LocalName == "row"))
            {
                var cells = new List<string>();
                foreach (var cell in row.Elements().Where(x => x.Name.LocalName == "c"))
                {
                    var type = (string?)cell.Attribute("t"); var value = cell.Elements().FirstOrDefault(x => x.Name.LocalName == "v")?.Value ?? "";
                    if (type == "s" && int.TryParse(value, out var index) && index >= 0 && index < shared.Count) value = shared[index];
                    else if (type == "inlineStr") value = string.Concat(cell.Descendants().Where(x => x.Name.LocalName == "t").Select(x => x.Value));
                    cells.Add(value.Replace("\r", " ").Replace("\n", " "));
                }
                output.AppendLine(string.Join("\t", cells));
            }
        }
        return output.ToString();
    }
}
