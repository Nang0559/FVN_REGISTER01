using Blazored.LocalStorage;

namespace FVN_REGISTER.Shared.Services.Language;

public sealed class LanguageService : ILanguageService
{
    private const string StorageKey = "fvn.ui.language";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);
    private static long _lastRefreshTicksUtc;

    private readonly ILocalStorageService _storage;
    private readonly ILanguageCatalogClientService? _catalog;
    private LanguageCode _current = LanguageCode.Vi;

    public LanguageService(ILocalStorageService storage, ILanguageCatalogClientService? catalog = null)
    {
        _storage = storage;
        _catalog = catalog;
    }

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

        // Texts saved in the Language Center (no event here: the caller is still initializing).
        await RefreshCoreAsync(force: false, raiseEvent: false, cancellationToken);
    }

    public Task RefreshOverridesAsync(bool force = false, CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(force, raiseEvent: true, cancellationToken);

    private async Task RefreshCoreAsync(bool force, bool raiseEvent, CancellationToken cancellationToken)
    {
        if (_catalog == null)
            return;
        if (!force && !IsRefreshDue())
            return;

        await RefreshGate.WaitAsync(cancellationToken);
        try
        {
            if (!force && !IsRefreshDue())
                return;

            // Stamp the attempt first: when the API is down the next page load must not wait for it again.
            Interlocked.Exchange(ref _lastRefreshTicksUtc, DateTime.UtcNow.Ticks);

            // The login page waits for this call, so never let it hang for the default HttpClient timeout.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);

            var since = LocalizationStore.OverridesVersion;
            var result = await _catalog.GetRuntimeSnapshotAsync(since < 0 ? null : since, timeout.Token);
            if (!result.IsSuccess || result.Data == null || result.Data.Unchanged)
                return;

            LocalizationStore.ApplyOverrides(result.Data.Version, result.Data.Vi, result.Data.Ja);
            if (raiseEvent)
                LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // The embedded catalog keeps the UI fully usable when the runtime texts cannot be loaded.
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private static bool IsRefreshDue() =>
        DateTime.UtcNow - new DateTime(Interlocked.Read(ref _lastRefreshTicksUtc), DateTimeKind.Utc) >= RefreshInterval;

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
        var value = LocalizationStore.Get(_current, key);

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
