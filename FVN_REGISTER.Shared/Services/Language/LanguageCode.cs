namespace FVN_REGISTER.Shared.Services.Language;

public enum LanguageCode
{
    Vi,
    Ja
}

public static class LanguageCodeExtensions
{
    public static string ToStorageValue(this LanguageCode code) => code switch
    {
        LanguageCode.Ja => "ja-JP",
        _ => "vi-VN"
    };

    /// <summary>
    /// Parses the browser preference. The persisted contract is vi-VN/ja-JP,
    /// but short BCP-47 values are accepted as a compatibility convenience.
    /// Unknown or empty values always fall back to Vietnamese.
    /// </summary>
    public static LanguageCode Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "ja" or "ja-jp" => LanguageCode.Ja,
        "vi" or "vi-vn" => LanguageCode.Vi,
        _ => LanguageCode.Vi
    };
}
