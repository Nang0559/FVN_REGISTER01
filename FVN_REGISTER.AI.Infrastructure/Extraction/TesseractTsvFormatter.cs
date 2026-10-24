using System.Globalization;
using System.Text;

namespace FVN_REGISTER.AI.Infrastructure.Extraction;

/// <summary>One recognised word with its geometry (pixels) and confidence (0..1).</summary>
public sealed record OcrWord(int Block, int Paragraph, int Line, int Left, int Top, int Width, int Height,
    double Confidence, string Text);

/// <summary>
/// Turns Tesseract TSV output (<c>tesseract image stdout tsv</c>) into the OCR text protocol:
/// real per-line/per-cell confidence and simple table detection from horizontal word gaps.
/// TSV columns: level, page_num, block_num, par_num, line_num, word_num, left, top, width, height, conf, text.
/// </summary>
public static class TesseractTsvFormatter
{
    public const int MaximumTsvCharacters = 10_000_000;
    /// <summary>A gap wider than this many text heights between two words starts a new column.</summary>
    private const double ColumnGapFactor = 1.8;

    public static IReadOnlyList<OcrWord> ParseWords(string tsv)
    {
        var words = new List<OcrWord>();
        if (string.IsNullOrEmpty(tsv)) return words;
        if (tsv.Length > MaximumTsvCharacters) throw new InvalidDataException("OCR_OUTPUT_EXCEEDS_LIMIT");
        foreach (var raw in tsv.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            var parts = line.Split('\t');
            if (parts.Length < 11 || parts[0] != "5") continue; // level 5 = word; also skips the header row
            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var block) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var paragraph) ||
                !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var lineNumber) ||
                !int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var left) ||
                !int.TryParse(parts[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var top) ||
                !int.TryParse(parts[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
                !int.TryParse(parts[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) ||
                !double.TryParse(parts[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var confidence) ||
                confidence < 0) continue;
            var text = parts.Length > 11 ? string.Join(' ', parts.Skip(11)).Trim() : "";
            if (text.Length == 0) continue;
            words.Add(new OcrWord(block, paragraph, lineNumber, left, top, Math.Max(width, 0), Math.Max(height, 1),
                Math.Clamp(confidence, 0, 100) / 100d, text));
        }
        return words;
    }

    /// <summary>Formats the words of ONE page (without the [PAGE n] header).</summary>
    public static string FormatPage(IReadOnlyList<OcrWord> words)
    {
        var lines = words.GroupBy(w => (w.Block, w.Paragraph, w.Line))
            .Select(g => BuildLine(g.OrderBy(w => w.Left).ToList()))
            .ToList();
        var output = new StringBuilder();
        var rowNumber = 0;
        for (var index = 0; index < lines.Count;)
        {
            var end = index + 1;
            if (lines[index].Cells.Count >= 2)
                while (end < lines.Count && lines[end].Cells.Count >= 2 &&
                       Math.Abs(lines[end].Cells.Count - lines[end - 1].Cells.Count) <= 1) end++;
            if (end - index >= 2)
            {
                // Two or more consecutive multi-column lines are treated as table rows.
                for (var row = index; row < end; row++)
                {
                    var line = lines[row];
                    output.Append("[ROW ").Append(++rowNumber)
                        .Append(" conf=").Append(Format(line.Confidence))
                        .Append(" cc=").Append(string.Join(',', line.Cells.Select(c => Format(c.Confidence))))
                        .Append("] ").AppendLine(string.Join('\t', line.Cells.Select(c => c.Text)));
                }
            }
            else
            {
                var line = lines[index];
                output.Append("[LINE conf=").Append(Format(line.Confidence)).Append("] ")
                    .AppendLine(string.Join(' ', line.Cells.Select(c => c.Text)));
                end = index + 1;
            }
            index = end;
        }
        return output.ToString();
    }

    private static OcrLine BuildLine(IReadOnlyList<OcrWord> words)
    {
        var heights = words.Select(w => w.Height).OrderBy(h => h).ToArray();
        var threshold = Math.Max(1, heights[heights.Length / 2]) * ColumnGapFactor;
        var cells = new List<OcrCell>();
        var current = new List<OcrWord> { words[0] };
        for (var i = 1; i < words.Count; i++)
        {
            var gap = words[i].Left - (words[i - 1].Left + words[i - 1].Width);
            if (gap > threshold) { cells.Add(ToCell(current)); current = []; }
            current.Add(words[i]);
        }
        cells.Add(ToCell(current));
        return new OcrLine(cells, words.Average(w => w.Confidence));
    }

    private static OcrCell ToCell(List<OcrWord> words) =>
        new(string.Join(' ', words.Select(w => w.Text)), words.Average(w => w.Confidence));

    private static string Format(double value) =>
        Math.Round(Math.Clamp(value, 0, 1), 2).ToString("0.##", CultureInfo.InvariantCulture);

    private sealed record OcrCell(string Text, double Confidence);
    private sealed record OcrLine(List<OcrCell> Cells, double Confidence);
}
