namespace FVN_REGISTER.Shared.Services.Language;

public interface ILanguageService
{
    LanguageCode Current { get; }
    event EventHandler? LanguageChanged;
    string T(string key, params object?[] args);
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SetLanguageAsync(LanguageCode language, CancellationToken cancellationToken = default);
}
