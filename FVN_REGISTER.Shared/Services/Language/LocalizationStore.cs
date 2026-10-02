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
///
/// Runtime overlay: the Language Center saves changes to the catalog files on the API server, not into this assembly.
/// Clients therefore download those texts (<c>api/localization/runtime</c>) and hand them to <see cref="ApplyOverrides"/>;
/// the overlay is consulted before the embedded text and is swapped atomically. <see cref="GetAll"/> and
/// <see cref="ReadFiles"/> always describe the embedded defaults only.
/// </summary>
public static class LocalizationStore
{
    private const string ResourcePrefix = "Localization.";
    private const string ResourceSuffix = ".json";

    private static readonly Lazy<IReadOnlyDictionary<LanguageCode, IReadOnlyDictionary<string, string>>> Cache =
        new(() => Build(ReadFiles(typeof(LocalizationStore).Assembly)), LazyThreadSafetyMode.ExecutionAndPublication);

    private sealed class Overlay
    {
        public static readonly Overlay NotLoaded = new(-1, Empty(), Empty());

        public Overlay(long version, IReadOnlyDictionary<string, string> vi, IReadOnlyDictionary<string, string> ja)
        {
            Version = version;
            Vi = vi;
            Ja = ja;
        }

        public long Version { get; }
        public IReadOnlyDictionary<string, string> Vi { get; }
        public IReadOnlyDictionary<string, string> Ja { get; }
        public IReadOnlyDictionary<string, string> For(LanguageCode language) => language == LanguageCode.Ja ? Ja : Vi;

        private static Dictionary<string, string> Empty() => new(StringComparer.OrdinalIgnoreCase);
    }

    private static volatile Overlay _overlay = Overlay.NotLoaded;

    /// <summary>The embedded defaults only (the runtime overlay is not included).</summary>
    public static IReadOnlyDictionary<string, string> GetAll(LanguageCode language) => Cache.Value[language];

    /// <summary>Version of the applied overlay; -1 until one was applied.</summary>
    public static long OverridesVersion => _overlay.Version;

    /// <summary>Replaces the whole overlay at once, so readers always see a complete and consistent set.</summary>
    public static void ApplyOverrides(long version, IReadOnlyDictionary<string, string>? vi, IReadOnlyDictionary<string, string>? ja) =>
        _overlay = new Overlay(version, Copy(vi), Copy(ja));

    /// <summary>Drops the overlay, so only the embedded text is used again.</summary>
    public static void ClearOverrides() => _overlay = Overlay.NotLoaded;

    public static bool IsOverridden(LanguageCode language, string key) => _overlay.For(language).ContainsKey(key);

    public static bool TryGet(LanguageCode language, string key, out string value)
    {
        var overlay = _overlay;
        var all = Cache.Value;
        if (overlay.For(language).TryGetValue(key, out value!))
            return true;
        if (all[language].TryGetValue(key, out value!))
            return true;
        if (language != LanguageCode.Vi)
        {
            if (overlay.Vi.TryGetValue(key, out value!))
                return true;
            if (all[LanguageCode.Vi].TryGetValue(key, out value!))
                return true;
        }
        value = key;
        return false;
    }

    private static IReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string>? source)
    {
        var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (source != null)
            foreach (var (key, value) in source)
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrEmpty(value))
                    copy[key] = value;
        return copy;
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
