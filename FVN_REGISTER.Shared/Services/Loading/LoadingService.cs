namespace FVN_REGISTER.Shared.Services.Loading;

public sealed class LoadingService : ILoadingService
{
    private int _activeOperations;
    private string? _message;
    private readonly object _sync = new();

    public bool IsLoading => Volatile.Read(ref _activeOperations) > 0;

    public string? Message
    {
        get
        {
            lock (_sync)
                return _message;
        }
    }

    public event EventHandler? StateChanged;

    public IDisposable Begin(string? message = null)
    {
        lock (_sync)
        {
            _activeOperations++;

            if (!string.IsNullOrWhiteSpace(message))
                _message = message;
        }

        NotifyStateChanged();
        return new LoadingScope(this);
    }

    public void Show(string? message = null)
        => Begin(message);

    public void Hide()
    {
        lock (_sync)
        {
            if (_activeOperations > 0)
                _activeOperations--;

            if (_activeOperations == 0)
                _message = null;
        }

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