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
