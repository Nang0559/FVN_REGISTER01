using System.Text;

namespace FVN_REGISTER.AI.Application.Contracts;

/// <summary>Canonical, module-neutral representation shared by classification, extraction and review.</summary>
public sealed record DocumentRepresentation(
    string Version,
    string MediaType,
    bool IsOcr,
    IReadOnlyList<DocumentPage> Pages,
    IReadOnlyList<DocumentTable> Tables,
    IReadOnlyList<DocumentRegion> Regions)
{
    public string ToModelText()
    {
        var builder = new StringBuilder();
        foreach (var page in Pages)
        {
            builder.Append("[PAGE ").Append(page.PageNumber).AppendLine("]");
            foreach (var paragraph in page.Paragraphs)
                builder.AppendLine(paragraph.Text);
            foreach (var row in page.Rows)
            {
                builder.Append("[ROW ").Append(row.RowNumber).Append("] ");
                builder.AppendLine(string.Join("\t", row.Cells.Select(x => x.Text)));
            }
        }
        return builder.ToString();
    }
}

public sealed record DocumentPage(
    int PageNumber,
    IReadOnlyList<DocumentParagraph> Paragraphs,
    IReadOnlyList<DocumentRow> Rows);

public sealed record DocumentParagraph(
    string Locator,
    string Text,
    double Confidence);

public sealed record DocumentTable(
    string Locator,
    IReadOnlyList<DocumentRow> Rows,
    double Confidence);

public sealed record DocumentRow(
    int RowNumber,
    string Locator,
    IReadOnlyList<DocumentCell> Cells,
    double Confidence);

public sealed record DocumentCell(
    int ColumnNumber,
    string Locator,
    string Text,
    double Confidence);

public sealed record DocumentRegion(
    string Locator,
    int PageNumber,
    string Text,
    double Confidence,
    string RegionType = "Text");
