using Blazored.LocalStorage;

namespace FVN_REGISTER.Shared.Services.Language;

public sealed class LanguageService : ILanguageService
{
    private const string StorageKey = "fvn.ui.language";
    private readonly ILocalStorageService _storage;
    private LanguageCode _current = LanguageCode.Vi;

    public LanguageService(ILocalStorageService storage) => _storage = storage;

    public LanguageCode Current => _current;
    public event EventHandler? LanguageChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = await _storage.GetItemAsync<string>(StorageKey, cancellationToken);
            _current = LanguageCodeExtensions.Parse(stored);
        }
        catch
        {
            _current = LanguageCode.Vi;
        }
    }

    public async Task SetLanguageAsync(LanguageCode language, CancellationToken cancellationToken = default)
    {
        if (_current == language)
            return;

        _current = language;
        try
        {
            await _storage.SetItemAsync(StorageKey, language.ToStorageValue(), cancellationToken);
        }
        catch
        {
            // UI localization continues even when browser storage is unavailable.
        }

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string T(string key, params object?[] args)
    {
        var value = LanguageCatalog.Get(_current, key);
        if (string.Equals(value, key, StringComparison.Ordinal))
            value = LanguageCatalogAdditional.TryGet(_current, key, out var additional) ? additional : key;
        if (string.Equals(value, key, StringComparison.Ordinal))
            value = LanguageCatalogModules.TryGet(_current, key, out var moduleValue) ? moduleValue : key;
        if (string.Equals(value, key, StringComparison.Ordinal))
            value = LanguageCatalogRemaining.TryGet(_current, key, out var remainingValue) ? remainingValue : key;
        if (string.Equals(value, key, StringComparison.Ordinal))
            value = LanguageCatalogUiCommon.TryGet(_current, key, out var commonValue) ? commonValue : key;
        if (string.Equals(value, key, StringComparison.Ordinal))
            value = LanguageCatalogLayout.TryGet(_current, key, out var layoutValue) ? layoutValue : key;
        if (string.Equals(value, key, StringComparison.Ordinal))
            value = LanguageCatalogSharedUi.TryGet(_current, key, out var sharedValue) ? sharedValue : key;

        if (args.Length == 0)
            return value;

        try
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, value, args);
        }
        catch (FormatException)
        {
            return value;
        }
    }
}
