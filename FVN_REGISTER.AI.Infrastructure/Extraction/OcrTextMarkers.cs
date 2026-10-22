using System.Text.RegularExpressions;

namespace FVN_REGISTER.AI.Infrastructure.Extraction;

/// <summary>
/// The OCR text protocol extends the existing page/row markers with per-line confidence:
/// <c>[LINE conf=0.93] text</c> and <c>[ROW 3 conf=0.91 cc=0.93,0.88] cell1&lt;TAB&gt;cell2</c>.
/// Consumers that only need plain text (knowledge chunks, LLM prompts) strip the attributes with this helper,
/// leaving exactly the text and the original <c>[ROW n]</c> marker.
/// </summary>
public static class OcrTextMarkers
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex LineConfidence = new(@"^\[LINE\s+conf=[0-9.]+\]\s?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, MatchTimeout);
    private static readonly Regex RowAttributes = new(@"^(\[ROW\s+\d+)(?:\s+conf=[0-9.]+)?(?:\s+cc=[0-9.,]*)?(\])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, MatchTimeout);

    /// <summary>Removes the confidence attributes from one line (no-op for lines without them).</summary>
    public static string StripLine(string line)
    {
        if (string.IsNullOrEmpty(line) || line[0] != '[') return line;
        line = LineConfidence.Replace(line, "", 1);
        return line.Length > 0 && line[0] == '[' ? RowAttributes.Replace(line, "$1$2", 1) : line;
    }

    /// <summary>Removes the confidence attributes from every line of a document text.</summary>
    public static string StripText(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("conf=", StringComparison.Ordinal)) return text;
        return string.Join('\n', text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(StripLine));
    }
}
