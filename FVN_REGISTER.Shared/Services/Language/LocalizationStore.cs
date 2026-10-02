using System.Reflection;
using System.Text.Json;

namespace FVN_REGISTER.Shared.Services.Language;

/// <summary>One embedded JSON file: <c>Localization/{lang}.{module}.json</c>.</summary>
public sealed record LocalizationFile(LanguageCode Language, string Module, IReadOnlyDictionary<string, string> Entries);

/// <summary>
/// Loads the UI text from the embedded <c>Localization/*.json</c> files (one file per language and module,
/// e.g. <c>vi.common.json</c> / <c>ja.common.json</c>) and serves lookups.
///
/// Rules (enforced at load time and by <c>LocalizationCatalogTests</c>):
///  - every key is written once, in the file of its module (the key prefix before the first '.');
///  - a duplicated key inside one file throws instead of silently overwriting the earlier value;
///  - lookups are case-insensitive; a key missing in Japanese falls back to Vietnamese, then to the key itself.
/// </summary>
public static class LocalizationStore
{
    private const string ResourcePrefix = "Localization.";
    private const string ResourceSuffix = ".json";

    private static readonly Lazy<IReadOnlyDictionary<LanguageCode, IReadOnlyDictionary<string, string>>> Cache =
        new(() => Build(ReadFiles(typeof(LocalizationStore).Assembly)), LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyDictionary<string, string> GetAll(LanguageCode language) => Cache.Value[language];

    public static bool TryGet(LanguageCode language, string key, out string value)
    {
        var all = Cache.Value;
        if (all[language].TryGetValue(key, out value!))
            return true;
        if (language != LanguageCode.Vi && all[LanguageCode.Vi].TryGetValue(key, out value!))
            return true;
        value = key;
        return false;
    }

    /// <summary>Returns the text for <paramref name="key"/>, or the key itself when it does not exist.</summary>
    public static string Get(LanguageCode language, string key) => TryGet(language, key, out var value) ? value : key;

    /// <summary>Splits a pipe-separated value (e.g. <c>help.X.steps</c>) into its items.</summary>
    public static string[] Steps(LanguageCode language, string featureCode) =>
        Get(language, $"help.{featureCode}.steps").Split('|', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Reads every embedded localization file. Throws <see cref="InvalidDataException"/> on malformed content.</summary>
    public static IReadOnlyList<LocalizationFile> ReadFiles(Assembly assembly)
    {
        var files = new List<LocalizationFile>();
        foreach (var resource in assembly.GetManifestResourceNames().OrderBy(n => n, StringComparer.Ordinal))
        {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal) ||
                !resource.EndsWith(ResourceSuffix, StringComparison.Ordinal))
                continue;

            var name = resource[ResourcePrefix.Length..^ResourceSuffix.Length];   // "vi.common"
            var dot = name.IndexOf('.');
            if (dot <= 0 || dot == name.Length - 1)
                throw new InvalidDataException($"Localization file '{resource}' must be named '{{lang}}.{{module}}.json'.");

            var language = ParseLanguage(name[..dot], resource);
            var module = name[(dot + 1)..];

            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidDataException($"Cannot open embedded resource '{resource}'.");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            files.Add(new LocalizationFile(language, module, ParseFlatObject(buffer.ToArray(), resource)));
        }
        return files;
    }

    private static LanguageCode ParseLanguage(string code, string resource) => code.ToLowerInvariant() switch
    {
        "vi" => LanguageCode.Vi,
        "ja" => LanguageCode.Ja,
        _ => throw new InvalidDataException($"Localization file '{resource}' uses unknown language '{code}'.")
    };

    /// <summary>Parses <c>{ "key": "text", ... }</c>; unlike Dictionary deserialization it rejects duplicate keys.</summary>
    private static IReadOnlyDictionary<string, string> ParseFlatObject(byte[] utf8, string resource)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            throw new InvalidDataException($"'{resource}' must contain a JSON object.");

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = reader.GetString()!;
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                throw new InvalidDataException($"'{resource}': value of '{key}' must be a string.");
            if (!result.TryAdd(key, reader.GetString()!))
                throw new InvalidDataException($"'{resource}': duplicate key '{key}'.");
        }
        return result;
    }

    private static IReadOnlyDictionary<LanguageCode, IReadOnlyDictionary<string, string>> Build(IReadOnlyList<LocalizationFile> files)
    {
        var map = new Dictionary<LanguageCode, Dictionary<string, string>>
        {
            [LanguageCode.Vi] = new(StringComparer.OrdinalIgnoreCase),
            [LanguageCode.Ja] = new(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var file in files)
            foreach (var (key, value) in file.Entries)
                if (!map[file.Language].TryAdd(key, value))
                    throw new InvalidDataException($"Key '{key}' is defined more than once in language '{file.Language}' (file module '{file.Module}').");

        return map.ToDictionary(p => p.Key, p => (IReadOnlyDictionary<string, string>)p.Value);
    }
}
