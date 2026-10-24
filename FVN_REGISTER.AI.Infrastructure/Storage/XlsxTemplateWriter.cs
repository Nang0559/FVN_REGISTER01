using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Storage;

/// <summary>Writes structured rows into an existing XLSX template without introducing an Office SDK dependency.</summary>
public sealed class XlsxTemplateWriter
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public byte[] Write(Stream template, DataEntryTemplateDefinition definition, IReadOnlyList<DataEntryRow> rows)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(definition);
        if (!template.CanSeek) throw new InvalidOperationException("XLSX template stream must be seekable.");
        if (definition.Columns.Count == 0) throw new ArgumentException("At least one XLSX destination column is required.", nameof(definition));
        if (definition.HeaderRow < 1 || definition.DataStartRow < 1) throw new ArgumentException("XLSX row numbers must be positive.", nameof(definition));
        if (rows.Count > 5000) throw new ArgumentException("An XLSX export cannot contain more than 5000 rows.", nameof(rows));

        using var input = new MemoryStream(); template.Position = 0; template.CopyTo(input); input.Position = 0;
        using var archive = new ZipArchive(input, ZipArchiveMode.Update, leaveOpen: true);
        var workbookEntry = archive.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException("XLSX workbook.xml is missing.");
        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels") ?? throw new InvalidDataException("XLSX workbook relationships are missing.");
        var workbook = LoadXml(workbookEntry);
        var rels = LoadXml(relsEntry);
        var sheet = workbook.Root?.Element(Main + "sheets")?.Elements(Main + "sheet").FirstOrDefault(x => string.Equals((string?)x.Attribute("name"), definition.WorksheetName, StringComparison.OrdinalIgnoreCase))
            ?? workbook.Root?.Element(Main + "sheets")?.Elements(Main + "sheet").FirstOrDefault()
            ?? throw new InvalidDataException("XLSX contains no worksheet.");
        var relationshipId = (string?)sheet.Attribute(Rel + "id") ?? throw new InvalidDataException("XLSX worksheet relationship is missing.");
        var relationship = rels.Root?.Elements(PackageRel + "Relationship").FirstOrDefault(x => string.Equals((string?)x.Attribute("Id"), relationshipId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("XLSX worksheet relationship cannot be resolved.");
        var target = (string?)relationship.Attribute("Target") ?? throw new InvalidDataException("XLSX worksheet target is missing.");
        var sheetPath = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target.TrimStart('/');
        var sheetEntry = archive.GetEntry(sheetPath) ?? archive.GetEntry(NormalizeTarget(target)) ?? throw new InvalidDataException($"XLSX worksheet '{definition.WorksheetName}' is missing.");
        var sheetXml = LoadXml(sheetEntry);
        var sheetData = sheetXml.Root?.Element(Main + "sheetData") ?? throw new InvalidDataException("XLSX sheetData is missing.");
        var templateRows = sheetData.Elements(Main + "row").ToDictionary(x => ParseRowNumber(x), x => x);
        var styleCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in definition.Columns)
        {
            if (!IsValidColumnLetter(column.ColumnLetter)) throw new ArgumentException($"Invalid XLSX column '{column.ColumnLetter}'.", nameof(definition));
            styleCache[column.ColumnLetter] = templateRows.Values.SelectMany(r => r.Elements(Main + "c")).FirstOrDefault(c => string.Equals(ColumnFromReference((string?)c.Attribute("r") ?? ""), column.ColumnLetter, StringComparison.OrdinalIgnoreCase))?.Attribute("s")?.Value;
        }
        for (var index = 0; index < rows.Count; index++)
        {
            var rowNumber = definition.DataStartRow + index;
            if (!templateRows.TryGetValue(rowNumber, out var rowElement))
            {
                rowElement = new XElement(Main + "row", new XAttribute("r", rowNumber)); var next = sheetData.Elements(Main + "row").FirstOrDefault(x => ParseRowNumber(x) > rowNumber); if (next is null) sheetData.Add(rowElement); else next.AddBeforeSelf(rowElement); templateRows[rowNumber] = rowElement;
            }
            foreach (var column in definition.Columns)
            {
                rows[index].Values.TryGetValue(column.Path, out var value); var reference = column.ColumnLetter.ToUpperInvariant() + rowNumber.ToString(CultureInfo.InvariantCulture); var cell = rowElement.Elements(Main + "c").FirstOrDefault(x => string.Equals((string?)x.Attribute("r"), reference, StringComparison.OrdinalIgnoreCase)); var style = cell?.Attribute("s")?.Value ?? styleCache[column.ColumnLetter]; cell?.Remove(); rowElement.Add(CreateCell(reference, value, column.DataType, style));
            }
        }
        sheetEntry.Delete();
        var replacement = archive.CreateEntry(sheetPath, CompressionLevel.Optimal);
        using (var output = replacement.Open()) using (var writer = new StreamWriter(output, new UTF8Encoding(false))) writer.Write(sheetXml.ToString(SaveOptions.DisableFormatting));
        archive.Dispose();
        return input.ToArray();
    }

    private static XElement CreateCell(string reference, JsonElement? value, string dataType, string? style)
    {
        var cell = new XElement(Main + "c", new XAttribute("r", reference)); if (!string.IsNullOrWhiteSpace(style)) cell.SetAttributeValue("s", style); if (!value.HasValue || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return cell; var v = value.Value;
        if (dataType.Equals("integer", StringComparison.OrdinalIgnoreCase) || dataType.Equals("decimal", StringComparison.OrdinalIgnoreCase))
        {
            if (v.ValueKind == JsonValueKind.Number && decimal.TryParse(v.GetRawText(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) cell.Add(new XElement(Main + "v", number.ToString(CultureInfo.InvariantCulture))); else { cell.SetAttributeValue("t", "inlineStr"); cell.Add(new XElement(Main + "is", new XElement(Main + "t", v.ToString()))); }
            return cell;
        }
        cell.SetAttributeValue("t", "inlineStr"); cell.Add(new XElement(Main + "is", new XElement(Main + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()))); return cell;
    }

    private static int ParseRowNumber(XElement row) => int.TryParse((string?)row.Attribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
    private static string ColumnFromReference(string reference) => new(reference.TakeWhile(char.IsLetter).ToArray());
    private static bool IsValidColumnLetter(string value) => !string.IsNullOrWhiteSpace(value) && value.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    private static string NormalizeTarget(string target) => target.Replace('\\', '/').TrimStart('/').StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? target.TrimStart('/') : "xl/" + target.TrimStart('/');
    private static XDocument LoadXml(ZipArchiveEntry entry) { using var stream = entry.Open(); return XDocument.Load(stream, LoadOptions.PreserveWhitespace); }
}
