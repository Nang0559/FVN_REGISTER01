using FVN_REGISTER.Application.Interfaces.OT;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Registration;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Entities.OT;
using FVN_REGISTER.Core.Entities.Security;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.OT;

/// <summary>
/// Single source of truth for who may register OT / Trip / LateEarly, with independent Feature Operator assignments per registration type.
/// Registrar of a department = (a) active Feature Operator assignment of OT.RegistrarManage covering the department
/// (department / block = nhóm phòng ban / all), OR (b) the department rule: Everyone / assigned users / approver positions.
/// Everybody else is NOT a registrar: they cannot create OT and cannot add other people to a Trip; they report to their
/// supervisor (calendar "Phản hồi cấp trên") or to HR.
/// </summary>
public sealed class OtRegistrationPolicy : IOtRegistrationPolicy
{
    private readonly FVNWEBAPPContext _db;

    public OtRegistrationPolicy(FVNWEBAPPContext db) => _db = db;

    public async Task<bool> IsRegistrarAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default)
    {
        if (user == null) return false;
        if (await IsAssignedRegistrarAsync(user, deptCode, NormalizeType(registerType), ct)) return true;

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

    public async Task<bool> CanRegisterForEmployeesAsync(UserIdentityDto user, IEnumerable<string> employeeCodes, string registerType = "OT", CancellationToken ct = default)
    {
        if (user == null) return false;
        var codes = (employeeCodes ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var depts = codes.Count == 0
            ? new List<int>()
            : (await _db.Set<F03Employee>().AsNoTracking()
                .Where(e => codes.Contains(e.EmployeeCode))
                .Select(e => (int?)e.DeptCode)
                .Distinct()
                .ToListAsync(ct)).Select(d => d ?? 0).Distinct().ToList();

        // Không xác định được phòng ban nhân viên (mã lạ): xét theo phòng ban của người đăng ký; service tạo đơn sẽ báo lỗi mã lạ.
        if (depts.Count == 0) depts.Add(user.DeptCode ?? 0);

        foreach (var dept in depts)
            if (!await IsRegistrarAsync(user, dept, registerType, ct))
                return false;
        return true;
    }

    public async Task<bool> IsAnyRegistrarAsync(UserIdentityDto user, string registerType = "OT", CancellationToken ct = default)
    {
        if (user == null) return false;
        var type = NormalizeType(registerType);

        // 1) registrar của phòng ban của chính mình (rule Everyone / chức vụ approver / user chỉ định)
        if (await IsRegistrarAsync(user, user.DeptCode ?? 0, type, ct)) return true;

        // 2) được chỉ định qua Feature Operator riêng của loại đăng ký (phòng ban / khối / toàn công ty), kể cả phòng ban KHÁC phòng mình
        var code = user.EmployeeCode?.Trim();
        if (!string.IsNullOrWhiteSpace(code)
            && await _db.Set<F03FeatureOperatorAssignment>().AsNoTracking().AnyAsync(x =>
                x.IsActive == true
                && x.FunctionCode == RegistrarFunctionCode(type)
                && x.ResourceType == RegistrarResourceType(type)
                && x.EmployeeCode == code, ct))
            return true;

        // 3) được chỉ định trực tiếp trong rule của một phòng ban khác
        return await _db.Set<F03OTRegistrar>().AsNoTracking()
            .AnyAsync(x => x.UserId == user.UserId && x.RegisterType == type && x.IsActive != false
                           && _db.Set<F03OTRegistrationRule>().Any(r => r.Id == x.RegistrationRuleId && r.IsActive==true && r.Mode == RegistrationRuleMode.AssignedUsers), ct);
    }

    public Task<bool> CanRegisterForOthersAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default)
        => IsRegistrarAsync(user, deptCode, registerType, ct);

    public async Task<bool> CanRegisterSelfOnlyAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default)
        => !await IsRegistrarAsync(user, deptCode, registerType, ct);

    public async Task<RegistrationPolicyDecisionDto> GetDecisionAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default)
    {
        var type = NormalizeType(registerType);
        var rule = await ResolveAsync(deptCode, type, ct);
        var registrar = await IsRegistrarAsync(user, deptCode, type, ct);
        return new RegistrationPolicyDecisionDto
        {
            DepartmentCode = deptCode,
            RegisterType = type,
            Mode = rule?.Mode ?? RegistrationRuleMode.AssignedPositions,
            CanRegister = registrar,
            CanRegisterForOthers = registrar,
            CanRegisterSelfOnly = !registrar,
            IsFallback = rule == null || rule.DepartmentCode == 0
        };
    }

    private async Task<bool> IsAssignedRegistrarAsync(UserIdentityDto user, int deptCode, string registerType, CancellationToken ct)
    {
        var code = user.EmployeeCode?.Trim();
        if (string.IsNullOrWhiteSpace(code)) return false;

        var rows = await _db.Set<F03FeatureOperatorAssignment>().AsNoTracking()
            .Where(x => x.IsActive == true
                        && x.FunctionCode == RegistrarFunctionCode(registerType)
                        && x.ResourceType == RegistrarResourceType(registerType)
                        && x.EmployeeCode == code)
            .Select(x => new { x.ResourceId, x.ScopeCode })
            .ToListAsync(ct);
        if (rows.Count == 0) return false;

        string? block = null;
        if (rows.Any(x => !x.ResourceId.HasValue && (x.ScopeCode ?? string.Empty).StartsWith("Block:", StringComparison.OrdinalIgnoreCase)))
            block = await _db.Set<F03Department>().AsNoTracking()
                .Where(d => d.DeptCode == deptCode)
                .Select(d => d.BlockCode)
                .FirstOrDefaultAsync(ct);

        return rows.Any(x => FeatureOperatorAuthorizationPolicy.OtRegistrarCovers(x.ResourceId, x.ScopeCode, deptCode, block));
    }

    private async Task<F03OTRegistrationRule?> ResolveAsync(int deptCode, string registerType, CancellationToken ct)
        => await _db.Set<F03OTRegistrationRule>()
            .AsNoTracking()
            .Include(x => x.Registrars)
            .Include(x => x.RegistrarPositions)
            .Where(x => x.IsActive == true && x.RegisterType == registerType && (x.DepartmentCode == deptCode || x.DepartmentCode == 0))
            .OrderByDescending(x => x.DepartmentCode == deptCode)
            .FirstOrDefaultAsync(ct);

    private static int RegistrarFunctionCode(string registerType)
        => registerType == "LATE_EARLY"
            ? SecurityFunctionCodes.LateEarlyRegistrarManage
            : SecurityFunctionCodes.OTRegistrarManage;

    private static string RegistrarResourceType(string registerType)
        => registerType == "LATE_EARLY"
            ? FeatureOperatorCatalog.LateEarlyRegistrar
            : FeatureOperatorCatalog.OtRegistrar;

    private static string NormalizeType(string value)
        => string.Equals(value, "TRIP", StringComparison.OrdinalIgnoreCase) ? "TRIP"
            : string.Equals(value, "LATE_EARLY", StringComparison.OrdinalIgnoreCase) ? "LATE_EARLY" : "OT";
}
