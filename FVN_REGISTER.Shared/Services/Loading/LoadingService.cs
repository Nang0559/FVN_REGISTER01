namespace FVN_REGISTER.Shared.Services.Loading;

public sealed class LoadingService : ILoadingService
{
    private int _activeOperations;
    private string? _message;

    public bool IsLoading => _activeOperations > 0;
    public string? Message => _message;

    public event EventHandler? StateChanged;

    public IDisposable Begin(string? message = null)
    {
        _activeOperations++;

        if (!string.IsNullOrWhiteSpace(message))
            _message = message;

        NotifyStateChanged();
        return new LoadingScope(this);
    }

    public void Show(string? message = null)
        => Begin(message);

    public void Hide()
    {
        if (_activeOperations > 0)
            _activeOperations--;

        if (_activeOperations == 0)
            _message = null;

        NotifyStateChanged();
    }

    public async Task RunAsync(Func<Task> operation, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using var scope = Begin(message);
        await operation();
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> operation, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using var scope = Begin(message);
        return await operation();
    }

    private void NotifyStateChanged()
        => StateChanged?.Invoke(this, EventArgs.Empty);

    private sealed class LoadingScope : IDisposable
    {
        private LoadingService? _owner;

        public LoadingScope(LoadingService owner)
            => _owner = owner;

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Hide();
        }
    }
}
