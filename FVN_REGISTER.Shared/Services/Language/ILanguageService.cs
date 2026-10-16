namespace FVN_REGISTER.Shared.Services.Language;

public interface ILanguageService
{
    LanguageCode Current { get; }
    event EventHandler? LanguageChanged;
    string T(string key, params object?[] args);
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SetLanguageAsync(LanguageCode language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the texts saved in the Language Center and applies them over the embedded catalog. Cheap when nothing changed
    /// (version check), throttled unless <paramref name="force"/> is set, and silent on failure (the embedded text keeps working).
    /// Raises <see cref="LanguageChanged"/> when the text changed so components re-render.
    /// </summary>
    Task RefreshOverridesAsync(bool force = false, CancellationToken cancellationToken = default);
}
