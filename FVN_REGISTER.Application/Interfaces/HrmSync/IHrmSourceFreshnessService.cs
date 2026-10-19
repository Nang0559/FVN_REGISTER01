namespace FVN_REGISTER.Application.Interfaces.HrmSync;

/// <summary>Pending HRM source fingerprint captured before one work date is calculated.</summary>
public sealed record HrmSourceCapture(Guid CaptureId, DateOnly WorkDate, string? EmployeeCode, bool IsActive);

/// <summary>One employee whose stored fingerprint differs from HRM for a contiguous date window.</summary>
public sealed record HrmStaleEmployeeRange(string EmployeeCode, DateOnly FromDate, DateOnly ToDate);

/// <summary>
/// Freshness tracking for calculated attendance. A successful calculation run only proves a date was
/// calculated; this service compares the HRM source fingerprint used by that calculation with the
/// current HRM fingerprint so changed dates are recalculated and unchanged dates never are.
/// All methods are failure tolerant: when the SQL objects are missing or HRM is unreachable the
/// behaviour degrades to "coverage only" and never blocks a calculation.
/// </summary>
public interface IHrmSourceFreshnessService
{
    /// <summary>Capture the current HRM fingerprint BEFORE calculating <paramref name="workDate"/>.</summary>
    Task<HrmSourceCapture> CaptureAsync(string? employeeCode, DateOnly workDate, CancellationToken ct = default);

    /// <summary>Promote the captured fingerprint after the calculation of that date succeeded.</summary>
    Task CommitAsync(HrmSourceCapture capture, Guid? calculationBatchId, CancellationToken ct = default);

    /// <summary>Dates in the range whose stored fingerprint is missing (inside the lookback) or different.</summary>
    Task<IReadOnlyList<DateOnly>> GetStaleDatesAsync(string employeeCode, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Employees that already have stored fingerprints and are now stale in the window (for the sweep).</summary>
    Task<IReadOnlyList<HrmStaleEmployeeRange>> GetStaleEmployeeRangesAsync(DateOnly from, DateOnly to, int maxEmployees, CancellationToken ct = default);
}
