using FVN_REGISTER.Application.Interfaces.Dashboards;
using FVN_REGISTER.Application.Interfaces.Trips;
using FVN_REGISTER.Contract.Dtos;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Dashboard;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Infrastructure.Services.Dashboards;

public sealed class TripDashboardProvider : IModuleDashboardProvider
{
    private readonly ITripService _service;
    private readonly IAuthorizationService _authorization;
    public RequestModule Module => RequestModule.Trip;
    public int RequiredFunctionCode => SecurityFunctionCodes.TripView;
    public TripDashboardProvider(ITripService service, IAuthorizationService authorization)
    {
        _service = service;
        _authorization = authorization;
    }

    public async Task<ModuleDashboardContribution> GetContributionAsync(UserIdentityDto user, CancellationToken ct = default)
    {
        if (!await _authorization.HasPersonalAsync(user, SecurityFunctionCodes.TripView, ct))
            return new ModuleDashboardContribution { Module = Module, Widgets = new List<WidgetCounterDto>(), Detail = new List<FVN_REGISTER.Contract.Dtos.Trips.TripRequestDto>() };

        var result = await _service.GetMineAsync(ct);
        var rows = result.Data ?? new List<FVN_REGISTER.Contract.Dtos.Trips.TripRequestDto>();
        var pending = rows.Count(x => x.RequestStatus == ApprovalStatus.Pending || x.RequestStatus == ApprovalStatus.InProgress);
        var approved = rows.Count(x => x.RequestStatus == ApprovalStatus.Approved);
        var widgets = new List<WidgetCounterDto>
        {
            new() { Title = "Công tác đang chờ", Value = pending.ToString(), Icon = "FlightTakeoff", Color = "Info", Link = "/trip/history", IsPersonal = true },
            new() { Title = "Công tác đã duyệt", Value = approved.ToString(), Icon = "CheckCircle", Color = "Success", Link = "/trip/history", IsPersonal = true }
        };
        return new ModuleDashboardContribution { Module = Module, Widgets = widgets, Detail = rows.Take(5).ToList() };
    }
}
