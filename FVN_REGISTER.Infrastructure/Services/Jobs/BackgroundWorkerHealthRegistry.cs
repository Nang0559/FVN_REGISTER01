using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.Jobs;

public sealed record BackgroundWorkerHealthSnapshot(
    string Name,
    DateTimeOffset StartedAt,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    string? LastError);

public sealed class BackgroundWorkerHealthRegistry
{
    private sealed class State
    {
        public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? LastSuccessAt { get; set; }
        public DateTimeOffset? LastFailureAt { get; set; }
        public string? LastError { get; set; }
    }

    private readonly ConcurrentDictionary<string, State> _states = new(StringComparer.OrdinalIgnoreCase);

    public BackgroundWorkerHealthRegistry(
        IServiceProvider services,
        IHostApplicationLifetime lifetime,
        ILogger<HrmAttendanceCalculationJobWorker> workerLogger)
    {
        // HrmAttendanceCalculationWorker already causes this singleton to be created during host startup.
        // Start the persistent manual-calculation worker from here without requiring another Program.cs registration.
        _ = new HrmAttendanceCalculationJobWorker(services, workerLogger, this).StartAsync(lifetime.ApplicationStopping);
    }

    public void Started(string name)
        => _states.TryAdd(name, new State());

    public void Success(string name)
    {
        var state = _states.GetOrAdd(name, _ => new State());
        state.LastSuccessAt = DateTimeOffset.UtcNow;
        state.LastError = null;
    }

    public void Failure(string name, Exception ex)
    {
        var state = _states.GetOrAdd(name, _ => new State());
        state.LastFailureAt = DateTimeOffset.UtcNow;
        state.LastError = ex.Message;
    }

    public IReadOnlyList<BackgroundWorkerHealthSnapshot> Snapshot()
        => _states.Select(x => new BackgroundWorkerHealthSnapshot(
                x.Key,
                x.Value.StartedAt,
                x.Value.LastSuccessAt,
                x.Value.LastFailureAt,
                x.Value.LastError))
            .OrderBy(x => x.Name)
            .ToList();
}
