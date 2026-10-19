using FVN_REGISTER.Infrastructure.Models.Data;
using FVN_REGISTER.Infrastructure.Services.HrmSync;
using FVN_REGISTER.Infrastructure.Services.Jobs;
using Microsoft.Extensions.Hosting;

namespace FVN_REGISTER.Application.Interfaces.HrmSync;

// Compatibility facade for the existing controller DI plus a lazy process-wide worker.
// The worker is started by the first job/monitor request and survives HTTP request cancellation.
public sealed class IHrmAttendanceCalculationJobService : HrmAttendanceCalculationJobService
{
    private static int _workerStarted;

    public IHrmAttendanceCalculationJobService(
        FVNWEBAPPContext db,
        HrmAttendanceCalculationJobWorker worker,
        IHostApplicationLifetime lifetime)
        : base(db)
    {
        if (Interlocked.Exchange(ref _workerStarted, 1) == 0)
            _ = worker.StartAsync(lifetime.ApplicationStopping);
    }
}
