namespace FVN_REGISTER.Application.Interfaces.HrmSync;

public enum AttendanceBackfillStatus
{
    /// <summary>The requested range is covered (nothing to do, or the backfill finished within the wait budget).</summary>
    Ready,

    /// <summary>The backfill is still running in the background; callers should show the data that exists now.</summary>
    Pending,

    /// <summary>The backfill failed inside the wait budget. Existing data is still readable.</summary>
    Failed
}

public sealed record AttendanceBackfillResult(AttendanceBackfillStatus Status, string? Message = null);

/// <summary>
/// Lazy attendance backfill for calendar requests that never blocks the caller for long:
/// waits at most <c>maxWait</c> for the backfill and otherwise lets it finish in the background.
/// </summary>
public interface IAttendanceBackfillCoordinator
{
    Task<AttendanceBackfillResult> EnsureAsync(
        string employeeCode,
        int? deptCode,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);
}
