using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>
/// Deterministic helpers used when building the generic extraction candidate from OCR/plain text.
/// </summary>
public static class DocumentCandidateText
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    // 2023-04-06, 2023/4/6
    private static readonly Regex YearFirst = new(
        @"(?<!\d)(?<y>\d{4})[-/](?<m>\d{1,2})[-/](?<d>\d{1,2})(?!\d)", Options, MatchTimeout);

    // 06/04/2023, 6-4-2023 (day first, as used in Vietnam)
    private static readonly Regex DayFirst = new(
        @"(?<!\d)(?<d>\d{1,2})[-/](?<m>\d{1,2})[-/](?<y>\d{4})(?!\d)", Options, MatchTimeout);

    // "ngày 06 tháng 04 năm 2023" (also without diacritics, as OCR often drops them)
    private static readonly Regex Vietnamese = new(
        @"(?<![\p{L}\d])ng[àa]y\s+(?<d>\d{1,2})\s+th[áa]ng\s+(?<m>\d{1,2})\s+n[ăa]m\s+(?<y>\d{4})(?!\d)",
        Options, MatchTimeout);

    /// <summary>
    /// Returns distinct, valid calendar dates found in the text, normalised to yyyy-MM-dd,
    /// in order of appearance.
    /// </summary>
    public static string[] ExtractDateCandidates(string text, int maximum = 100)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        text = text.Normalize(NormalizationForm.FormC);
        var found = new List<(int Index, string Value)>();
        foreach (var regex in new[] { YearFirst, DayFirst, Vietnamese })
            foreach (Match match in regex.Matches(text))
                if (TryNormalise(match, out var iso))
                    found.Add((match.Index, iso));
        return found.OrderBy(x => x.Index).Select(x => x.Value).Distinct().Take(maximum).ToArray();
    }

    // "Số: FCC-06042023" -> FCC-06042023 (also "No: ...")
    private static readonly Regex DocumentNumber = new(
        @"(?<![\p{L}\d])(?:s[ốo]|no\.?)\s*[:：]\s*(?<n>[A-Za-z0-9][A-Za-z0-9\-/._]{2,39})", Options, MatchTimeout);

    // Internal form codes such as HC-NS-05 (upper-case only: avoids matching ordinary hyphenated words).
    private static readonly Regex FormCode = new(
        @"(?<![\p{L}\d-])(?<c>[A-Z]{2,5}-[A-Z]{2,5}-\d{2,4})(?![\p{L}\d-])", RegexOptions.CultureInvariant, MatchTimeout);

    // "Điều 15- Chương 3", "Điều 18"
    private static readonly Regex Article = new(
        @"(?<![\p{L}\d])(?:đ|d)i(?:ề|e)u\s+(?<a>\d{1,3})(?!\d)(?:\s*[-–—]\s*ch(?:ư|u)(?:ơ|o)ng\s+(?<c>\d{1,2})(?!\d))?",
        Options, MatchTimeout);

    // Page markers and outline numbering ("2.1. Trường hợp", "1. Nội dung") are structure, not data.
    private static readonly Regex PageMarker = new(@"\[PAGE\s+\d+\]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, MatchTimeout);
    private static readonly Regex OutlineNumber = new(
        @"^[ \t]*(?<n>\d+(?:\.\d+)+(?=\.[ \t])|\d+(?=\.[ \t]+\p{L}))",
        RegexOptions.CultureInvariant | RegexOptions.Multiline, MatchTimeout);
    private static readonly Regex Number = new(@"(?<!\w)-?\d+(?:[.,]\d+)?", RegexOptions.CultureInvariant, MatchTimeout);

    /// <summary>Document numbers announced by "Số:" (e.g. FCC-06042023), in order of appearance.</summary>
    public static string[] ExtractDocumentNumberCandidates(string text, int maximum = 20)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        text = text.Normalize(NormalizationForm.FormC);
        return DocumentNumber.Matches(text).Select(m => m.Groups["n"].Value.TrimEnd('.', '-', '/', '_'))
            .Where(x => x.Length >= 3 && x.Any(char.IsDigit)).Distinct().Take(maximum).ToArray();
    }

    /// <summary>Internal form codes such as HC-NS-05.</summary>
    public static string[] ExtractFormCodeCandidates(string text, int maximum = 20)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        text = text.Normalize(NormalizationForm.FormC);
        return FormCode.Matches(text).Select(m => m.Groups["c"].Value).Distinct().Take(maximum).ToArray();
    }

    /// <summary>Article references normalised to "Điều N" or "Điều N - Chương M".</summary>
    public static string[] ExtractArticleCandidates(string text, int maximum = 50)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        text = text.Normalize(NormalizationForm.FormC);
        return Article.Matches(text).Select(m => m.Groups["c"].Success
                ? $"Điều {m.Groups["a"].Value} - Chương {m.Groups["c"].Value}"
                : $"Điều {m.Groups["a"].Value}")
            .Distinct().Take(maximum).ToArray();
    }

    /// <summary>
    /// Numbers that can be data (hours, days, amounts). Numbers that are part of a date, document number, form code,
    /// article reference, page marker or outline numbering are removed first so they do not pollute the list.
    /// </summary>
    public static string[] ExtractNumericCandidates(string text, int maximum = 200)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var chars = text.Normalize(NormalizationForm.FormC).ToCharArray();
        var source = new string(chars);

        void Mask(Match match, string? group = null)
        {
            var g = group is null ? match.Groups[0] : match.Groups[group];
            for (var i = g.Index; i < g.Index + g.Length && i < chars.Length; i++) chars[i] = ' ';
        }

        foreach (var regex in new[] { YearFirst, DayFirst, Vietnamese, FormCode, Article, PageMarker })
            foreach (Match match in regex.Matches(source)) Mask(match);
        foreach (Match match in DocumentNumber.Matches(source)) Mask(match, "n");
        foreach (Match match in OutlineNumber.Matches(source)) Mask(match, "n");

        return Number.Matches(new string(chars)).Select(m => m.Value).Distinct().Take(maximum).ToArray();
    }

    /// <summary>
    /// True for OCR junk such as stamp/signature fragments ("aS", "Sy", "a 1"): lines with fewer than
    /// three letters or digits carry no evidence value.
    /// </summary>
    public static bool IsNoiseLine(string line) =>
        string.IsNullOrWhiteSpace(line) || line.Count(char.IsLetterOrDigit) < 3;

    private static bool TryNormalise(Match match, out string iso)
    {
        iso = string.Empty;
        var text = $"{match.Groups["y"].Value}-{match.Groups["m"].Value}-{match.Groups["d"].Value}";
        if (!DateOnly.TryParseExact(text, "yyyy-M-d", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return false;
        iso = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return true;
    }
}
