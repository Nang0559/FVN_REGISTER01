using FVN_REGISTER.Infrastructure.Services.HrmSync;
using FVN_REGISTER.Infrastructure.Models.Data;

namespace FVN_REGISTER.Application.Interfaces.HrmSync;

// Compatibility facade for existing controller DI. The implementation lives in Infrastructure;
// this concrete type lets ASP.NET resolve the service without changing the large Program.cs registration block.
public sealed class IHrmAttendanceCalculationJobService : HrmAttendanceCalculationJobService
{
    public IHrmAttendanceCalculationJobService(FVNWEBAPPContext db) : base(db) { }
}
