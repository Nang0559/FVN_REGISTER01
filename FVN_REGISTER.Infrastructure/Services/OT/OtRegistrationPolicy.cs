using FVN_REGISTER.Application.Interfaces.OT;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Registration;
using FVN_REGISTER.Core.Entities.OT;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.OT;

/// <summary>Single source of truth for department-level OT/Trip registrar policy.</summary>
public sealed class OtRegistrationPolicy : IOtRegistrationPolicy
{
    private readonly FVNWEBAPPContext _db;

    public OtRegistrationPolicy(FVNWEBAPPContext db) => _db = db;

    public async Task<bool> CanRegisterForOthersAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default)
    {
        var rule = await ResolveAsync(deptCode, NormalizeType(registerType), ct);
        if (rule == null) return false;
        return rule.Mode switch
        {
            RegistrationRuleMode.Everyone => true,
            RegistrationRuleMode.AssignedUsers => rule.Registrars.Any(x => x.UserId == user.UserId),
            RegistrationRuleMode.AssignedPositions => !string.IsNullOrWhiteSpace(user.PositionCode)
                && rule.RegistrarPositions.Any(x => string.Equals(x.PositionCode, user.PositionCode, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    public async Task<bool> CanRegisterSelfOnlyAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default)
        => !await CanRegisterForOthersAsync(user, deptCode, registerType, ct);

    public async Task<RegistrationPolicyDecisionDto> GetDecisionAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default)
    {
        var type = NormalizeType(registerType);
        var rule = await ResolveAsync(deptCode, type, ct);
        var others = rule != null && await CanRegisterForOthersAsync(user, deptCode, type, ct);
        return new RegistrationPolicyDecisionDto
        {
            DepartmentCode = deptCode,
            RegisterType = type,
            Mode = rule?.Mode ?? RegistrationRuleMode.AssignedPositions,
            CanRegisterForOthers = others,
            CanRegisterSelfOnly = !others,
            IsFallback = rule == null || rule.DepartmentCode == 0
        };
    }

    private async Task<F03OTRegistrationRule?> ResolveAsync(int deptCode, string registerType, CancellationToken ct)
        => await _db.Set<F03OTRegistrationRule>()
            .AsNoTracking()
            .Include(x => x.Registrars)
            .Include(x => x.RegistrarPositions)
            .Where(x => x.IsActive && x.RegisterType == registerType && (x.DepartmentCode == deptCode || x.DepartmentCode == 0))
            .OrderByDescending(x => x.DepartmentCode == deptCode)
            .FirstOrDefaultAsync(ct);

    private static string NormalizeType(string value)
        => string.Equals(value, "TRIP", StringComparison.OrdinalIgnoreCase) ? "TRIP" : "OT";
}
