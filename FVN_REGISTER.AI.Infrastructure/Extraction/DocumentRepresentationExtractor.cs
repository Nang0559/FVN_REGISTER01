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
    private static readonly Regex RowMarker = new(@"^\[ROW\s+(?<row>\d+)\]\s*(?<text>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
        var current = new PageBuilder(1);
        pages.Add(current);
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

            var row = RowMarker.Match(line);
            if (row.Success)
            {
                rowNumber = int.Parse(row.Groups["row"].Value);
                AddRow(current, rowNumber, row.Groups["text"].Value, confidence);
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

        var normalizedPages = pages.OrderBy(x => x.PageNumber).Select(x => x.Build()).ToArray();
        var tables = normalizedPages
            .Where(x => x.Rows.Count > 0)
            .Select(x => new DocumentTable($"page:{x.PageNumber};table:1", x.Rows, confidence))
            .ToArray();
        var regions = normalizedPages.SelectMany(page =>
            page.Paragraphs.Select(p => new DocumentRegion(p.Locator, page.PageNumber, p.Text, p.Confidence, "Paragraph"))
            .Concat(page.Rows.SelectMany(r => r.Cells.Select(c =>
                new DocumentRegion(c.Locator, page.PageNumber, c.Text, c.Confidence, "TableCell"))))).ToArray();

        return Task.FromResult(new DocumentRepresentation("1.0", mediaType, isOcr, normalizedPages, tables, regions));
    }

    private static void AddRow(PageBuilder page, int rowNumber, string value, double confidence)
    {
        var parts = value.Split('\t');
        var cells = parts.Select((cell, index) => new DocumentCell(
            index + 1,
            $"page:{page.PageNumber};row:{rowNumber};cell:{index + 1}",
            cell.Trim(),
            confidence)).ToArray();
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
