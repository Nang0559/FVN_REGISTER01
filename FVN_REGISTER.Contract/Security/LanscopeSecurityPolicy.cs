using System.Text.RegularExpressions;

namespace FVN_REGISTER.Contract.Security;

public static class LanscopeSecurityPolicy
{
    private static readonly HashSet<string> SerialPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "TO BE FILLED BY O.E.M.",
        "TO BE FILLED BY OEM",
        "DEFAULT STRING",
        "DEFAULT",
        "UNKNOWN",
        "N/A",
        "NA",
        "NONE",
        "NOT SPECIFIED",
        "SERIALNUMBER",
        "SYSTEM SERIAL NUMBER"
    };

    public static bool IsMeaningfulSerial(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
        if (normalized.Length < 3) return false;
        if (SerialPlaceholders.Contains(normalized)) return false;
        var compact = Regex.Replace(normalized.ToUpperInvariant(), @"[\s._-]+", string.Empty);
        if (compact.Length < 3 || compact.All(c => c == '0')) return false;
        return true;
    }

    public static bool ShouldRetryEnrollmentStatus(int statusCode) => statusCode >= 500 && statusCode <= 599;
}
