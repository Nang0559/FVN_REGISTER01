using System.Globalization;
using System.Text.RegularExpressions;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Extraction;

/// <summary>
/// Converts the deterministic text/OCR output into one stable representation.
/// Locators are intentionally provider-neutral so later OCR/layout providers can replace this adapter.
/// </summary>
public sealed class DocumentRepresentationExtractor : IDocumentRepresentationExtractor
{
    private static readonly Regex PageMarker = new(@"^\[PAGE\s+(?<page>\d+)\]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RowMarker = new(@"^\[ROW\s+(?<row>\d+)(?:\s+conf=(?<conf>[0-9.]+))?(?:\s+cc=(?<cc>[0-9.,]*))?\]\s*(?<text>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LineMarker = new(@"^\[LINE\s+conf=(?<conf>[0-9.]+)\]\s*(?<text>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Task<DocumentRepresentation> ExtractAsync(string text, string mediaType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Document text is required.", nameof(text));
        if (text.Length > 2_000_000) throw new InvalidDataException("DOCUMENT_REPRESENTATION_INPUT_TOO_LARGE");

        var isOcr = text.StartsWith("[OCR]", StringComparison.Ordinal);
        if (isOcr) text = text[5..].TrimStart();
        var confidence = isOcr ? 0.85d : 1.0d;
        var tabular = mediaType.Equals("text/csv", StringComparison.OrdinalIgnoreCase) ||
                      mediaType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase);

        var pages = new List<PageBuilder>();
        PageBuilder? current = null;
        var rowNumber = 0;
        var sourceLines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        foreach (var raw in sourceLines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var page = PageMarker.Match(line);
            if (page.Success && int.TryParse(page.Groups["page"].Value, out var pageNumber))
            {
                current = pages.FirstOrDefault(x => x.PageNumber == pageNumber) ?? new PageBuilder(pageNumber);
                if (!pages.Contains(current)) pages.Add(current);
                rowNumber = 0;
                continue;
            }

            current ??= CreateDefaultPage(pages);
            var row = RowMarker.Match(line);
            if (row.Success)
            {
                rowNumber = int.Parse(row.Groups["row"].Value);
                var rowConfidence = ParseConfidence(row.Groups["conf"].Value, confidence);
                var cellConfidences = row.Groups["cc"].Success
                    ? row.Groups["cc"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => ParseConfidence(x, rowConfidence)).ToArray()
                    : null;
                AddRow(current, rowNumber, row.Groups["text"].Value, rowConfidence, cellConfidences);
                continue;
            }

            var marked = LineMarker.Match(line);
            if (marked.Success)
            {
                var lineConfidence = ParseConfidence(marked.Groups["conf"].Value, confidence);
                var markedText = marked.Groups["text"].Value.Trim();
                if (markedText.Length == 0) continue;
                current.Paragraphs.Add(new DocumentParagraph(
                    $"page:{current.PageNumber};paragraph:{current.Paragraphs.Count + 1}", markedText, lineConfidence));
                continue;
            }

            if (tabular && line.Contains('\t'))
            {
                AddRow(current, ++rowNumber, line, confidence);
                continue;
            }

            var paragraphNumber = current.Paragraphs.Count + 1;
            current.Paragraphs.Add(new DocumentParagraph(
                $"page:{current.PageNumber};paragraph:{paragraphNumber}", line, confidence));
        }

        if (pages.Count == 0) pages.Add(new PageBuilder(1));
        var normalizedPages = pages.OrderBy(x => x.PageNumber).Select(x => x.Build()).ToArray();
        var tables = normalizedPages
            .Where(x => x.Rows.Count > 0)
            .Select(x => new DocumentTable($"page:{x.PageNumber};table:1", x.Rows, x.Rows.Average(r => r.Confidence)))
            .ToArray();
        var regions = normalizedPages.SelectMany(page =>
            page.Paragraphs.Select(p => new DocumentRegion(p.Locator, page.PageNumber, p.Text, p.Confidence, "Paragraph"))
            .Concat(page.Rows.SelectMany(r => r.Cells.Select(c =>
                new DocumentRegion(c.Locator, page.PageNumber, c.Text, c.Confidence, "TableCell"))))).ToArray();

        return Task.FromResult(new DocumentRepresentation("1.0", mediaType, isOcr, normalizedPages, tables, regions));
    }

    private static PageBuilder CreateDefaultPage(List<PageBuilder> pages)
    {
        var page = pages.FirstOrDefault(x => x.PageNumber == 1) ?? new PageBuilder(1);
        if (!pages.Contains(page)) pages.Add(page);
        return page;
    }

    private static double ParseConfidence(string value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 0d, 1d)
            : fallback;

    private static void AddRow(PageBuilder page, int rowNumber, string value, double confidence, double[]? cellConfidences = null)
    {
        var parts = value.Split('\t');
        var cells = parts.Select((cell, index) => new DocumentCell(
            index + 1,
            $"page:{page.PageNumber};row:{rowNumber};cell:{index + 1}",
            cell.Trim(),
            cellConfidences is not null && index < cellConfidences.Length ? cellConfidences[index] : confidence)).ToArray();
        page.Rows.Add(new DocumentRow(rowNumber, $"page:{page.PageNumber};row:{rowNumber}", cells, confidence));
    }

    private sealed class PageBuilder(int pageNumber)
    {
        public int PageNumber { get; } = pageNumber;
        public List<DocumentParagraph> Paragraphs { get; } = [];
        public List<DocumentRow> Rows { get; } = [];
        public DocumentPage Build() => new(PageNumber, Paragraphs.ToArray(), Rows.ToArray());
    }
}
