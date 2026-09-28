using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FVN_REGISTER.Core.Schema;

/// <summary>
/// Generic Excel column inference used by Equipment and intended for checklist,
/// attendance, payroll and other configurable-table imports.
/// It only proposes a schema; the caller remains responsible for user confirmation.
/// </summary>
public static class ExcelSchemaInference
{
    private static readonly CultureInfo ViCulture = new("vi-VN");
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    public sealed record InputColumn(string Header, IReadOnlyList<string?> Samples);
    public sealed record Result(string FieldKey, string FieldLabel, string DataType, bool IsRequired, int? MaxLength, int DisplayOrder, string? SampleValue);

    public static IReadOnlyList<Result> Infer(IReadOnlyList<InputColumn> columns)
    {
        if (columns.Count == 0) throw new ArgumentException("Excel phải có ít nhất một cột.", nameof(columns));
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<Result>(columns.Count);
        for (var i = 0; i < columns.Count; i++)
        {
            var header = columns[i].Header?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(header)) throw new InvalidOperationException($"Cột thứ {i + 1} chưa có tên.");
            var key = CreateUniqueKey(header, usedKeys);
            var samples = columns[i].Samples.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Take(200).ToList();
            var type = InferType(header, samples);
            var required = columns[i].Samples.Count > 0 && columns[i].Samples.All(x => !string.IsNullOrWhiteSpace(x));
            var maxLength = type is "Text" or "Email" or "Choice" ? samples.Select(x => x.Length).DefaultIfEmpty(0).Max() : (int?)null;
            results.Add(new Result(key, header, type, required, maxLength > 0 ? Math.Min(maxLength.Value, 4000) : null, i + 1, samples.FirstOrDefault()));
        }
        return results;
    }

    private static string InferType(string header, IReadOnlyList<string> samples)
    {
        if (samples.Count == 0) return "Text";
        var normalizedHeader = Normalize(header);
        if (normalizedHeader.Contains("email") || normalizedHeader.Contains("mail")) return samples.All(IsEmail) ? "Email" : "Text";
        if (samples.All(IsBoolean)) return "Boolean";
        var dateScore = samples.Count(x => IsDate(x)) / (double)samples.Count;
        if (dateScore >= 0.90) return samples.Any(x => x.Contains(':')) || normalizedHeader.Contains("gio") || normalizedHeader.Contains("time") ? "DateTime" : "Date";
        var currencyHint = normalizedHeader.Contains("tien") || normalizedHeader.Contains("gia") || normalizedHeader.Contains("price") || normalizedHeader.Contains("salary") || normalizedHeader.Contains("luong");
        var numeric = samples.Where(x => !LooksLikeIdentifier(x)).ToList();
        var numberScore = numeric.Count == 0 ? 0 : numeric.Count(x => IsDecimal(x)) / (double)numeric.Count;
        if (numberScore >= 0.95 && numeric.Count == samples.Count) return currencyHint ? "Currency" : (samples.All(IsInteger) ? "Integer" : "Decimal");
        return "Text";
    }

    private static bool LooksLikeIdentifier(string value) { var text = value.Trim(); return text.Length > 1 && text[0] == '0' && text.All(char.IsDigit); }
    private static bool IsInteger(string value) => int.TryParse(value, NumberStyles.Integer, Invariant, out _) || long.TryParse(value, NumberStyles.Integer, Invariant, out _) || int.TryParse(value, NumberStyles.Integer, ViCulture, out _);
    private static bool IsDecimal(string value) => decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, ViCulture, out _) || decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, Invariant, out _);
    private static bool IsBoolean(string value) { var n = Normalize(value); return n is "true" or "false" or "co" or "khong" or "yes" or "no" or "1" or "0"; }
    private static bool IsEmail(string value) => Regex.IsMatch(value.Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant);
    private static bool IsDate(string value)
    {
        var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy", "M/d/yyyy", "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm", "yyyy-MM-dd HH:mm:ss" };
        return DateTime.TryParseExact(value.Trim(), formats, ViCulture, DateTimeStyles.AllowWhiteSpaces, out _) || DateTime.TryParse(value, ViCulture, DateTimeStyles.AllowWhiteSpaces, out _) || DateTime.TryParse(value, Invariant, DateTimeStyles.AllowWhiteSpaces, out _);
    }
    private static string CreateUniqueKey(string header, HashSet<string> used)
    {
        var baseKey = Normalize(header); if (string.IsNullOrWhiteSpace(baseKey)) baseKey = "Cot"; if (baseKey.Length > 60) baseKey = baseKey[..60];
        var key = baseKey; var suffix = 2;
        while (!used.Add(key)) { var suffixText = suffix.ToString(Invariant); var prefixLength = Math.Max(1, 60 - suffixText.Length - 1); key = $"{baseKey[..Math.Min(baseKey.Length, prefixLength)]}_{suffixText}"; suffix++; }
        return key;
    }
    private static string Normalize(string value)
    {
        var decomposed = value.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"[^a-z0-9]+", string.Empty);
    }
}
