namespace FVN_REGISTER.Shared.Services.Loading;

public interface ILoadingService
{
    bool IsLoading { get; }
    string? Message { get; }

    event EventHandler? StateChanged;

    IDisposable Begin(string? message = null);
    void Show(string? message = null);
    void Hide();

    Task RunAsync(Func<Task> operation, string? message = null);
    Task<T> RunAsync<T>(Func<Task<T>> operation, string? message = null);
}
