namespace FVN_REGISTER.Application.Interfaces.Jobs;

/// <summary>
/// Runtime API used by the hosted workers. The schedule (enabled/interval/run-now) lives in
/// dbo.F03BackgroundJobSchedules and is re-read every few seconds, so UI changes apply without
/// restarting the API. When the table is missing/unreachable the code defaults from
/// <c>BackgroundJobCatalog</c> are used and the worker keeps running.
/// </summary>
public interface IBackgroundJobScheduler
{
    /// <summary>Returns as soon as the job is enabled (immediately when it already is).</summary>
    Task WaitUntilEnabledAsync(string jobKey, CancellationToken ct);

    /// <summary>
    /// Waits for the configured interval (+ small jitter) counted from this call. Wakes early on
    /// "run now", honours interval changes made while waiting, and keeps waiting while disabled.
    /// </summary>
    Task WaitForNextAsync(string jobKey, CancellationToken ct);

    void MarkStarted(string jobKey);
    void MarkSucceeded(string jobKey, string? message = null);
    void MarkFailed(string jobKey, string message);
}
