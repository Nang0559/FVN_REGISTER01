using FVN_REGISTER.Application.Interfaces.Dashboards;
using FVN_REGISTER.Application.Interfaces.Equipment;
using FVN_REGISTER.Contract.Dtos;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Dashboard;
using FVN_REGISTER.Contract.Dtos.Equipment;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Core.Enums;

namespace FVN_REGISTER.Infrastructure.Services.Dashboards;

public sealed class EquipmentDashboardProvider : IModuleDashboardProvider
{
    private readonly IEquipmentService _service;
    private readonly IAuthorizationService _authorization;

    public RequestModule Module => RequestModule.Equipment;
    public int RequiredFunctionCode => SecurityFunctionCodes.EquipmentView;

    public EquipmentDashboardProvider(
        IEquipmentService service,
        IAuthorizationService authorization)
    {
        _service = service;
        _authorization = authorization;
    }

    public async Task<ModuleDashboardContribution> GetContributionAsync(
        UserIdentityDto user,
        CancellationToken ct = default)
    {
        // Dashboard personal equipment is capability + assignment driven.
        // ManagedScope must never grant the personal capability.
        if (!await _authorization.HasPersonalAsync(
                user, SecurityFunctionCodes.EquipmentView, ct))
        {
            return Empty();
        }

        var assetsResult = await _service.GetMyAssignedAssetsAsync(ct);
        var mineResult = await _service.GetMineAsync(ct);

        var assets = assetsResult.IsSuccess && assetsResult.Data != null
            ? assetsResult.Data
            : new List<EquipmentAssetDto>();

        var requests = mineResult.IsSuccess && mineResult.Data != null
            ? mineResult.Data
            : new List<EquipmentRequestDto>();

        var pending = requests.Count(x =>
            x.RequestStatus == ApprovalStatus.Pending ||
            x.RequestStatus == ApprovalStatus.InProgress);

        var approved = requests.Count(x =>
            x.RequestStatus == ApprovalStatus.Approved);

        var widgets = new List<WidgetCounterDto>();

        if (assets.Count > 0)
        {
            widgets.Add(new WidgetCounterDto
            {
                Title = "dashboard.equipmentAssigned",
                Value = assets.Count.ToString(),
                Icon = "Devices",
                Color = "Info",
                Link = "/equipment",
                IsPersonal = true
            });
        }

        if (requests.Count > 0)
        {
            widgets.Add(new WidgetCounterDto
            {
                Title = "dashboard.equipmentRequests",
                Value = requests.Count.ToString(),
                Icon = "Assignment",
                Color = "Primary",
                Link = "/equipment",
                IsPersonal = true
            });

            widgets.Add(new WidgetCounterDto
            {
                Title = "dashboard.equipmentPending",
                Value = pending.ToString(),
                Icon = "PendingActions",
                Color = "Warning",
                Link = "/equipment",
                IsPersonal = true
            });

            widgets.Add(new WidgetCounterDto
            {
                Title = "dashboard.equipmentApproved",
                Value = approved.ToString(),
                Icon = "Verified",
                Color = "Success",
                Link = "/equipment",
                IsPersonal = true
            });
        }

        return new ModuleDashboardContribution
        {
            Module = Module,
            Widgets = widgets,
            Detail = requests.Take(5).ToList()
        };
    }

    private static ModuleDashboardContribution Empty() =>
        new()
        {
            Module = RequestModule.Equipment,
            Widgets = new List<WidgetCounterDto>(),
            Detail = new List<EquipmentRequestDto>()
        };
}
