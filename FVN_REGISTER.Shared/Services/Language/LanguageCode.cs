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

    public static LanguageCode Parse(string? value) =>
        string.Equals(value, "ja-JP", StringComparison.OrdinalIgnoreCase)
            ? LanguageCode.Ja
            : LanguageCode.Vi;
}
