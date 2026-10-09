using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Services.Common;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Requests.Security;
using FVN_REGISTER.Core.Entities.Security;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.Infrastructure.Services.Security;

public sealed class AuthorizationService : BaseService<AuthorizationService>, IAuthorizationService
{
    private readonly IUnitOfWork _uow;
    private readonly FVN_REGISTER.Application.Interfaces.Auths.ISessionService _sessionService;
    private readonly FVN_REGISTER.Application.Interfaces.Auths.IAuditService _auditService;

    public AuthorizationService(
        IUnitOfWork uow,
        ILogger<AuthorizationService> logger,
        IOptionsMonitor<AuthDebugOptions> options,
        FVN_REGISTER.Application.Interfaces.Auths.ISessionService sessionService,
        FVN_REGISTER.Application.Interfaces.Auths.IAuditService auditService)
        : base(logger, options)
    {
        _uow = uow;
        _sessionService = sessionService;
        _auditService = auditService;
    }

    public async Task<bool> HasAsync(
        UserIdentityDto user,
        int functionCode,
        CancellationToken ct = default)
    {
        if (user.UserId <= 0)
            return false;

        var snapshot = await GetSnapshotAsync(user.UserId, ct);
        return snapshot.Has(functionCode);
    }

    public async Task<bool> HasScopeAsync(UserIdentityDto user, int functionCode, string scope, CancellationToken ct = default)
    {
        if (user.UserId <= 0 || string.IsNullOrWhiteSpace(scope))
            return false;

        var normalizedScope = scope.Trim();
        return await (
            from ur in _uow.Repository<F03UserRole>().Query().AsNoTracking()
            join rf in _uow.Repository<F03RoleFunction>().Query().AsNoTracking()
                on ur.IdRole equals rf.IdRole
            join r in _uow.Repository<F03Role>().Query().AsNoTracking()
                on ur.IdRole equals r.Id
            join f in _uow.Repository<F03Function>().Query().AsNoTracking()
                on rf.IdFunction equals f.Id
            where ur.IdUser == user.UserId
                && ur.IsActive == true
                && rf.IsActive == true
                && r.IsActive == true
                && (f.IsActive ?? true)
                && f.FunctionCode == functionCode
                && (rf.ScopeCode ?? f.ScopeCode) == normalizedScope
            select f.Id
        ).AnyAsync(ct);
    }

    public async Task<bool> HasPersonalAsync(UserIdentityDto user, int functionCode, CancellationToken ct = default)
    {
        if (user.UserId <= 0)
            return false;

        // Personal capability is meaningful only when the account resolves to
        // an active HRM employee. A manually-created account may still have a
        // Personal RoleFunction grant, but without F03Employees identity there
        // is no safe "của tôi" subject for Leave/OT/Trip/Equipment/Calendar.
        var hasActiveEmployee = await _uow.Repository<F03Employee>().Query()
            .AsNoTracking()
            .AnyAsync(x =>
                x.IsActive == true &&
                x.EmployeeCode == user.EmployeeCode,
                ct);

        if (!hasActiveEmployee)
            return false;

        return await (
            from ur in _uow.Repository<F03UserRole>().Query().AsNoTracking()
            join rf in _uow.Repository<F03RoleFunction>().Query().AsNoTracking()
                on ur.IdRole equals rf.IdRole
            join r in _uow.Repository<F03Role>().Query().AsNoTracking()
                on ur.IdRole equals r.Id
            join f in _uow.Repository<F03Function>().Query().AsNoTracking()
                on rf.IdFunction equals f.Id
            where ur.IdUser == user.UserId
                && ur.IsActive == true
                && rf.IsActive == true
                && r.IsActive == true
                && (f.IsActive ?? true)
                && f.FunctionCode == functionCode
                && (rf.AccessMode == "Personal"
                    || (rf.AccessMode == null
                        && (rf.ScopeCode ?? f.ScopeCode) == AuthorizationScopeCodes.Own))
            select f.Id
        ).AnyAsync(ct);
    }

    public async Task<bool> HasManagementAsync(UserIdentityDto user, int functionCode, CancellationToken ct = default)
    {
        if (user.UserId <= 0) return false;
        return await (
            from ur in _uow.Repository<F03UserRole>().Query().AsNoTracking()
            join rf in _uow.Repository<F03RoleFunction>().Query().AsNoTracking() on ur.IdRole equals rf.IdRole
            join r in _uow.Repository<F03Role>().Query().AsNoTracking() on ur.IdRole equals r.Id
            join f in _uow.Repository<F03Function>().Query().AsNoTracking() on rf.IdFunction equals f.Id
            where ur.IdUser == user.UserId && ur.IsActive == true && rf.IsActive == true && r.IsActive == true && (f.IsActive ?? true)
                && f.FunctionCode == functionCode
                && (rf.AccessMode == "Management"
                    || (rf.AccessMode == null
                        && ((rf.ScopeCode ?? f.ScopeCode) == AuthorizationScopeCodes.Department
                            || (rf.ScopeCode ?? f.ScopeCode) == AuthorizationScopeCodes.All)))
            select f.Id
        ).AnyAsync(ct);
    }

    public async Task<string> GetScopeAsync(int userId, int functionCode, CancellationToken ct = default)
    {
        if (userId <= 0)
            return AuthorizationScopeCodes.None;

        var scopes = await (
            from ur in _uow.Repository<F03UserRole>().Query().AsNoTracking()
            join rf in _uow.Repository<F03RoleFunction>().Query().AsNoTracking()
                on ur.IdRole equals rf.IdRole
            join r in _uow.Repository<F03Role>().Query().AsNoTracking()
                on ur.IdRole equals r.Id
            join f in _uow.Repository<F03Function>().Query().AsNoTracking()
                on rf.IdFunction equals f.Id
            where ur.IdUser == userId
                && ur.IsActive == true
                && rf.IsActive == true
                && r.IsActive == true
                && (f.IsActive ?? true)
                && f.FunctionCode == functionCode
            select rf.ScopeCode ?? f.ScopeCode
        ).ToListAsync(ct);

        // Capability-granting operator assignments (e.g. Execution.Review) contribute their own scope.
        if (FeatureOperatorCatalog.AssignmentGrantsCapability(functionCode))
        {
            var assignmentScopes = await GetAssignmentScopesAsync(userId, functionCode, ct);
            return AuthorizationScopePolicy.ResolveEffectiveScope(scopes.Concat(assignmentScopes));
        }

        return AuthorizationScopePolicy.ResolveEffectiveScope(scopes);
    }

    /// <summary>
    /// Scopes contributed by active, module-wide operator assignments of a capability-granting function.
    /// Requires an active user AND an active HRM employee; otherwise the assignment grants nothing.
    /// </summary>
    private async Task<List<string?>> GetAssignmentScopesAsync(int userId, int functionCode, CancellationToken ct)
    {
        var employeeCode = await _uow.Repository<F03User>().Query()
            .AsNoTracking()
            .Where(x => x.Id == userId && (x.IsActive ?? true))
            .Select(x => x.EmployeeCode)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(employeeCode))
            return new List<string?>();

        var employeeActive = await _uow.Repository<F03Employee>().Query()
            .AsNoTracking()
            .AnyAsync(x => x.IsActive == true && x.EmployeeCode == employeeCode, ct);
        if (!employeeActive)
            return new List<string?>();

        return await (
            from a in _uow.Repository<F03FeatureOperatorAssignment>().Query().AsNoTracking()
            join f in _uow.Repository<F03Function>().Query().AsNoTracking()
                on a.FunctionCode equals f.FunctionCode
            where a.IsActive == true
                && a.EmployeeCode == employeeCode
                && a.ResourceId == null
                && a.FunctionCode == functionCode
                && (f.IsActive ?? true)
            select (string?)(a.ScopeCode ?? f.ScopeCode)
        ).ToListAsync(ct);
    }

    /// <summary>
    /// Functions granted purely by an active module-wide operator assignment (no role grant needed).
    /// </summary>
    private async Task<List<SecurityFunctionDto>> GetAssignmentGrantedFunctionsAsync(
        string employeeCode,
        IReadOnlyCollection<int> functionCodes,
        CancellationToken ct)
    {
        var rows = await (
            from a in _uow.Repository<F03FeatureOperatorAssignment>().Query().AsNoTracking()
            join f in _uow.Repository<F03Function>().Query().AsNoTracking()
                on a.FunctionCode equals f.FunctionCode
            where a.IsActive == true
                && a.EmployeeCode == employeeCode
                && a.ResourceId == null
                && functionCodes.Contains(a.FunctionCode)
                && (f.IsActive ?? true)
            select new { Function = f, Scope = a.ScopeCode ?? f.ScopeCode }
        ).ToListAsync(ct);

        return rows
            .GroupBy(x => x.Function.FunctionCode)
            .Select(g =>
            {
                var function = g.First().Function;
                var scope = AuthorizationScopePolicy.ResolveEffectiveScope(g.Select(x => (string?)x.Scope));
                return new SecurityFunctionDto
                {
                    IdFunction = function.Id,
                    FunctionCode = function.FunctionCode,
                    FunctionName = function.FunctionName,
                    Detail = function.Detail,
                    ModuleCode = function.ModuleCode,
                    ActionCode = function.ActionCode,
                    ScopeCode = scope,
                    AccessMode = string.Equals(scope, AuthorizationScopeCodes.Own, StringComparison.OrdinalIgnoreCase)
                        ? "Personal"
                        : "Management",
                    DisplayOrder = function.DisplayOrder,
                    IsSystemCritical = function.IsSystemCritical
                };
            })
            .ToList();
    }

    public async Task<bool> CanAccessAsync(
        UserIdentityDto user,
        int functionCode,
        string? employeeCode,
        int? deptCode,
        CancellationToken ct = default)
    {
        if (user.UserId <= 0)
            return false;

        var scope = await GetScopeAsync(user.UserId, functionCode, ct);
        if (AuthorizationScopePolicy.CanAccess(
            scope,
            user.EmployeeCode,
            user.DeptCode,
            employeeCode,
            deptCode))
            return true;

        // ManagedScope is an additional data-scope grant. It never grants the
        // capability itself and therefore cannot bypass HasAsync/RoleFunction.
        if (string.Equals(scope, AuthorizationScopeCodes.None, StringComparison.OrdinalIgnoreCase))
            return false;

        var managed = await GetManagedScopesAsync(user.UserId, ct);
        if (managed.Count == 0)
            return false;

        return await IsWithinManagedScopeAsync(managed, employeeCode, deptCode, ct);
    }

    public async Task<List<ManagedScopeDto>> GetManagedScopesAsync(
        int userId,
        CancellationToken ct = default)
    {
        if (userId <= 0)
            return new();

        var employeeCode = await _uow.Repository<F03User>().Query()
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.EmployeeCode)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(employeeCode))
            return new();

        return await _uow.Repository<F03ManagedScope>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true && x.EmployeeCode == employeeCode)
            .OrderBy(x => x.NodeType)
            .ThenBy(x => x.NodeCode)
            .Select(x => new ManagedScopeDto
            {
                Id = x.Id,
                EmployeeCode = x.EmployeeCode,
                NodeType = x.NodeType,
                NodeCode = x.NodeCode,
                FactoryCode = x.FactoryCode,
                DeptCode = x.DeptCode,
                SubDepartmentCode = x.SubDepartmentCode,
                IncludeChildren = x.IncludeChildren,
                Remark = x.Remark
            })
            .ToListAsync(ct);
    }

    public async Task<List<ManagedEmployeeDto>> GetManagedEmployeesAsync(
        int userId,
        CancellationToken ct = default)
    {
        var scopes = await GetManagedScopesAsync(userId, ct);
        if (scopes.Count == 0)
            return new();

        var employees = await _uow.Repository<F03Employee>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true)
            .Select(x => new { x.EmployeeCode, x.EmployeeName, x.DeptCode })
            .ToListAsync(ct);

        var departments = await _uow.Repository<F03Department>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true)
            .Select(x => new { x.DeptCode, x.ParentDeptCode, x.BlockCode })
            .ToListAsync(ct);

        var departmentMap = departments
            .GroupBy(x => x.DeptCode)
            .ToDictionary(
                g => g.Key,
                g => (
                    g.First().ParentDeptCode,
                    g.First().BlockCode));

        return employees
            .Where(x => ManagedScopeMatches(scopes, x.DeptCode, departmentMap))
            .OrderBy(x => x.DeptCode)
            .ThenBy(x => x.EmployeeCode)
            .Select(x => new ManagedEmployeeDto
            {
                EmployeeCode = x.EmployeeCode,
                EmployeeName = x.EmployeeName ?? x.EmployeeCode,
                DeptCode = x.DeptCode
            })
            .ToList();
    }

    public async Task<PermissionSnapshotDto> ReplaceManagedScopesAsync(
        int userId,
        IReadOnlyCollection<ManagedScopeRequest> scopes,
        int actorUserId,
        CancellationToken ct = default)
    {
        if (userId <= 0 || actorUserId <= 0)
            throw new InvalidOperationException("UserId/ActorUserId không hợp lệ.");

        var employeeCode = await _uow.Repository<F03User>().Query()
            .Where(x => x.Id == userId)
            .Select(x => x.EmployeeCode)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(employeeCode))
            throw new InvalidOperationException("Tài khoản chưa liên kết nhân viên.");

        foreach (var scope in scopes)
        {
            var nodeType = (scope.NodeType ?? string.Empty).Trim();
            if (nodeType is not ("Company" or "Factory" or "Department" or "SubDepartment"))
                throw new InvalidOperationException($"NodeType ManagedScope không hợp lệ: {nodeType}.");

            if (nodeType != "Company" &&
                string.IsNullOrWhiteSpace(scope.NodeCode) &&
                scope.DeptCode == null &&
                scope.SubDepartmentCode == null &&
                string.IsNullOrWhiteSpace(scope.FactoryCode))
                throw new InvalidOperationException($"ManagedScope {nodeType} phải có NodeCode hoặc mã node tổ chức.");
        }

        var repo = _uow.Repository<F03ManagedScope>();
        var existing = await repo.Query()
            .Where(x => x.EmployeeCode == employeeCode && x.IsActive == true)
            .ToListAsync(ct);

        foreach (var row in existing)
        {
            row.IsActive = false;
            row.ModifiedBy = actorUserId;
            row.ModifiedAt = DateTime.Now;
            row.LastModifiedSource = "SECURITY_MANAGED_SCOPE_REPLACE";
        }

        foreach (var scope in scopes)
        {
            await repo.AddAsync(new F03ManagedScope
            {
                EmployeeCode = employeeCode,
                NodeType = scope.NodeType.Trim(),
                NodeCode = scope.NodeCode?.Trim(),
                FactoryCode = scope.FactoryCode?.Trim(),
                DeptCode = scope.DeptCode,
                SubDepartmentCode = scope.SubDepartmentCode,
                IncludeChildren = scope.IncludeChildren,
                Remark = scope.Remark?.Trim(),
                CreatedBy = actorUserId,
                LastModifiedSource = "SECURITY_MANAGED_SCOPE"
            }, ct);
        }

        await _uow.SaveChangesAsync(ct);
        await _auditService.LogAction(
            "SECURITY_MANAGED_SCOPE_CHANGED",
            actorUserId,
            $"UserId={userId}; EmployeeCode={employeeCode}; Count={scopes.Count}",
            ct: ct);

        return await GetSnapshotAsync(userId, ct);
    }

    public async Task<EffectivePermissionPreviewDto> GetEffectivePermissionPreviewAsync(
        int userId,
        CancellationToken ct = default)
    {
        var snapshot = await GetSnapshotAsync(userId, ct);
        var user = await _uow.Repository<F03User>().Query()
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.EmployeeCode, x.FullName, x.DeptCode, x.Cvcode })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        var roles = await GetRolesAsync(ct);
        var roleNames = roles
            .Where(x => snapshot.RoleCodes.Contains(x.RoleCode))
            .Select(x => x.RoleName)
            .ToList();

        var managedScopes = await GetManagedScopesAsync(userId, ct);
        var policies = await _uow.Repository<F03ApprovalPolicy>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true
                && x.ApprovalPositionCode == user.Cvcode)
            .OrderBy(x => x.RequestType)
            .ThenBy(x => x.Level)
            .ThenBy(x => x.Sequence)
            .Select(x => new EffectiveApprovalPolicyDto
            {
                RequestType = (int)x.RequestType,
                RequestTypeName = x.RequestType.ToString(),
                DeptCode = x.DeptCode,
                PositionCode = x.PositionCode,
                ApprovalPositionCode = x.ApprovalPositionCode,
                Level = x.Level,
                Sequence = x.Sequence,
                LevelName = x.LevelName,
                RoleName = x.RoleName,
                Required = x.Required
            })
            .ToListAsync(ct);

        var actions = snapshot.Functions
            .Where(x => !string.IsNullOrWhiteSpace(x.ModuleCode))
            .Select(x => new EffectivePermissionActionDto
            {
                FunctionCode = x.FunctionCode,
                ModuleCode = x.ModuleCode!,
                ActionCode = x.ActionCode ?? string.Empty,
                ScopeCode = NormalizeScopeCode(x.ScopeCode),
                AccessMode = x.AccessMode
            })
            .ToList();

        var visibleMenus = actions
            .Select(x => x.ModuleCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var constraints = new List<string>
        {
            "Business State luôn được kiểm tra server-side; capability không tự bỏ qua state machine."
        };
        if (actions.Any(x => x.ModuleCode.Equals("Leave", StringComparison.OrdinalIgnoreCase)))
            constraints.Add("Leave: Draft/Pending/Approved/Rejected/Cancelled giới hạn Create/Edit/Cancel/Approve theo state.");
        if (actions.Any(x => x.ModuleCode.Equals("OT", StringComparison.OrdinalIgnoreCase)))
            constraints.Add("OT: Draft/Pending/Approved/Rejected/Cancelled giới hạn Create/Edit/Cancel/Approve/Reconcile theo state.");
        if (actions.Any(x => x.ModuleCode.Equals("Trip", StringComparison.OrdinalIgnoreCase)))
            constraints.Add("Trip: Draft/Pending/Approved/Rejected/Cancelled giới hạn Create/Edit/Cancel/Approve theo state.");
        if (actions.Any(x => x.ModuleCode.Equals("Equipment", StringComparison.OrdinalIgnoreCase)))
            constraints.Add("Equipment: Assign/Transfer/Return/Liquidate/Repair/Approve còn bị giới hạn bởi asset/request state và field rule.");
        if (actions.Any(x => x.ModuleCode.Equals("Payroll", StringComparison.OrdinalIgnoreCase)))
            constraints.Add("Payroll: không Lock/Export khi reconciliation/correction còn unresolved hoặc snapshot stale.");

        return new EffectivePermissionPreviewDto
        {
            UserId = userId,
            EmployeeCode = user.EmployeeCode,
            EmployeeName = user.FullName,
            DeptCode = user.DeptCode,
            PositionCode = user.Cvcode,
            RoleCodes = snapshot.RoleCodes,
            RoleNames = roleNames,
            VisibleMenus = visibleMenus,
            Actions = actions,
            ManagedScopes = managedScopes,
            ApprovalPolicies = policies,
            BusinessStateConstraints = constraints
        };
    }

    private static string? NormalizeScopeCode(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return scope;

        var value = scope.Trim();
        return string.Equals(value, "Global", StringComparison.OrdinalIgnoreCase)
            ? AuthorizationScopeCodes.All
            : value;
    }

    private static bool ManagedScopeMatches(
        IReadOnlyCollection<ManagedScopeDto> scopes,
        int? targetDept,
        IReadOnlyDictionary<int, (int? ParentDeptCode, string? BlockCode)> departments)
    {
        if (!targetDept.HasValue)
            return scopes.Any(x => string.Equals(x.NodeType, "Company", StringComparison.OrdinalIgnoreCase));

        if (!departments.TryGetValue(targetDept.Value, out var target))
            target = (null, null);

        foreach (var scope in scopes)
        {
            var nodeType = (scope.NodeType ?? string.Empty).Trim();

            if (nodeType.Equals("Company", StringComparison.OrdinalIgnoreCase))
                return true;

            string? node = nodeType switch
            {
                "Factory" => scope.FactoryCode ?? scope.NodeCode,
                "Department" => scope.DeptCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? scope.NodeCode,
                "SubDepartment" => scope.SubDepartmentCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? scope.NodeCode,
                _ => scope.NodeCode
            };

            node = node?.Trim();
            if (string.IsNullOrWhiteSpace(node))
                continue;

            // Factory has no FactoryCode on F03Department. Support both models:
            // 1) factory represented by a department ancestor, and
            // 2) factory represented by the HR BlockCode field.
            if (nodeType.Equals("Factory", StringComparison.OrdinalIgnoreCase)
                && string.Equals(target.BlockCode, node, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(targetDept.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), node, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!scope.IncludeChildren)
                continue;

            var cursor = targetDept.Value;
            var visited = new HashSet<int>();

            while (departments.TryGetValue(cursor, out var current)
                   && current.ParentDeptCode != null
                   && visited.Add(cursor))
            {
                var parent = current.ParentDeptCode.Value;

                if (string.Equals(parent.ToString(System.Globalization.CultureInfo.InvariantCulture), node, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (nodeType.Equals("Factory", StringComparison.OrdinalIgnoreCase)
                    && departments.TryGetValue(parent, out var parentNode)
                    && string.Equals(parentNode.BlockCode, node, StringComparison.OrdinalIgnoreCase))
                    return true;

                cursor = parent;
            }
        }

        return false;
    }

    private async Task<bool> IsWithinManagedScopeAsync(
        IReadOnlyCollection<ManagedScopeDto> scopes,
        string? employeeCode,
        int? deptCode,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(employeeCode) && deptCode == null)
            return false;

        var target = await _uow.Repository<F03Employee>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true &&
                        ((employeeCode != null && x.EmployeeCode == employeeCode) ||
                         (employeeCode == null && deptCode != null && x.DeptCode == deptCode)))
            .Select(x => new { x.EmployeeCode, x.DeptCode })
            .FirstOrDefaultAsync(ct);

        var targetDept = target?.DeptCode ?? deptCode;
        if (!targetDept.HasValue)
            return false;

        var departments = await _uow.Repository<F03Department>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true)
            .Select(x => new { x.DeptCode, x.ParentDeptCode, x.BlockCode })
            .ToListAsync(ct);

        var departmentMap = departments
            .GroupBy(x => x.DeptCode)
            .ToDictionary(
                g => g.Key,
                g => (
                    g.First().ParentDeptCode,
                    g.First().BlockCode));

        return ManagedScopeMatches(scopes, targetDept, departmentMap);
    }

    private async Task EnsureSuperAdminCriticalGrantsAsync(CancellationToken ct)
    {
        var role = await _uow.Repository<F03Role>().Query()
            .FirstOrDefaultAsync(x => x.RoleCode == 1 && x.IsActive == true, ct);

        if (role == null)
            return;

        // Materialize the IReadOnlySet as an array before entering the EF query.
        // EF Core translates Enumerable.Contains over an array/list to SQL IN,
        // while IReadOnlySet<T>.Contains is not translatable by the SQL Server provider.
        var criticalFunctionCodes = SecurityFunctionCodes.SystemCriticalCodes.ToArray();

        var criticalFunctions = await _uow.Repository<F03Function>().Query()
            .Where(x => criticalFunctionCodes.Contains(x.FunctionCode)
                && (x.IsActive ?? true))
            .ToListAsync(ct);

        if (criticalFunctions.Count == 0)
            return;

        var repo = _uow.Repository<F03RoleFunction>();
        var existing = await repo.Query()
            .Where(x => x.IdRole == role.Id)
            .ToListAsync(ct);
        var existingByFunction = existing.ToDictionary(x => x.IdFunction);
        var changed = false;

        foreach (var function in criticalFunctions)
        {
            if (existingByFunction.TryGetValue(function.Id, out var grant))
            {
                if (grant.IsActive != true || grant.ScopeCode != null || grant.AccessMode != null)
                {
                    grant.IsActive = true;
                    grant.ScopeCode = null;
                    grant.AccessMode = null;
                    grant.ModifiedBy = 0;
                    grant.ModifiedAt = DateTime.Now;
                    grant.LastModifiedSource = "SUPERADMIN_CRITICAL_SELF_HEAL";
                    changed = true;
                }

                continue;
            }

            await repo.AddAsync(new F03RoleFunction
            {
                IdRole = role.Id,
                IdFunction = function.Id,
                IsActive = true,
                ScopeCode = null,
                AccessMode = null,
                CreatedBy = 0,
                CreatedAt = DateTime.Now,
                LastModifiedSource = "SUPERADMIN_CRITICAL_SELF_HEAL"
            }, ct);
            changed = true;
        }

        if (changed)
            await _uow.SaveChangesAsync(ct);
    }

    public async Task<PermissionSnapshotDto> GetSnapshotAsync(
        int userId,
        CancellationToken ct = default)
    {
        var active = await _uow.Repository<F03User>().Query()
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => (bool?)x.IsActive)
            .FirstOrDefaultAsync(ct);

        if (active != true)
        {
            return new PermissionSnapshotDto
            {
                UserId = userId,
                PermissionCode = await _uow.Repository<F03User>().Query()
                    .Where(x => x.Id == userId)
                    .Select(x => (int?)x.PermissionCode)
                    .FirstOrDefaultAsync(ct),
                RoleCodes = new List<int>(),
                Functions = new List<SecurityFunctionDto>(),
                FunctionCodes = new HashSet<int>()
            };
        }

        var roleCodes = await (
            from ur in _uow.Repository<F03UserRole>().Query().AsNoTracking()
            join r in _uow.Repository<F03Role>().Query().AsNoTracking()
                on ur.IdRole equals r.Id
            where ur.IdUser == userId && ur.IsActive == true && r.IsActive == true
            select r.RoleCode
        ).Distinct().ToListAsync(ct);

        // Existing databases may predate the immutable critical-function invariant.
        // Heal only the active SuperAdmin baseline before calculating effective permissions,
        // so the Security Center matrix is never hidden merely because one legacy grant is missing.
        if (roleCodes.Contains(1))
            await EnsureSuperAdminCriticalGrantsAsync(ct);

        // Canonical RBAC: User -> Role -> Function/Action.
        // PermissionCode and legacy F03UserFunction are not effective grants.
        // This is important when an administrator removes a module from a role:
        // stale legacy rows must not make that module reappear in the UI/API.
        var rawFunctions = await (
            from ur in _uow.Repository<F03UserRole>().Query().AsNoTracking()
            join rf in _uow.Repository<F03RoleFunction>().Query().AsNoTracking()
                on ur.IdRole equals rf.IdRole
            join f in _uow.Repository<F03Function>().Query().AsNoTracking()
                on rf.IdFunction equals f.Id
            join r in _uow.Repository<F03Role>().Query().AsNoTracking()
                on ur.IdRole equals r.Id
            where ur.IdUser == userId && ur.IsActive == true && rf.IsActive == true && r.IsActive == true && (f.IsActive ?? true)
            select new { Function = f, EffectiveScope = NormalizeScopeCode(rf.ScopeCode ?? f.ScopeCode), EffectiveAccessMode = rf.AccessMode ?? ((NormalizeScopeCode(rf.ScopeCode ?? f.ScopeCode)) == AuthorizationScopeCodes.Own || NormalizeScopeCode(rf.ScopeCode ?? f.ScopeCode) == AuthorizationScopeCodes.Employee ? "Personal" : "Management") }
        ).ToListAsync(ct);

        // Personal permissions are effective only when the account has an
        // active HRM employee subject. Keep management/system permissions intact
        // for manually-created or break-glass accounts without an employee row.
        var snapshotEmployeeCode = await _uow.Repository<F03User>().Query()
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.EmployeeCode)
            .FirstOrDefaultAsync(ct);

        var hasActiveEmployee = !string.IsNullOrWhiteSpace(snapshotEmployeeCode)
            && await _uow.Repository<F03Employee>().Query()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.IsActive == true &&
                    x.EmployeeCode == snapshotEmployeeCode,
                    ct);

        if (!hasActiveEmployee)
        {
            rawFunctions = rawFunctions
                .Where(x => !string.Equals(
                    x.EffectiveAccessMode,
                    "Personal",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var functions = rawFunctions
            .GroupBy(x => new { x.Function.FunctionCode, Scope = x.EffectiveScope ?? AuthorizationScopeCodes.None, Mode = x.EffectiveAccessMode })
            .Select(g => g.OrderBy(x => x.Function.DisplayOrder).First())
            .OrderBy(x => x.Function.DisplayOrder)
            .ThenBy(x => x.Function.FunctionCode)
            .Select(x => new SecurityFunctionDto
            {
                IdFunction = x.Function.Id,
                FunctionCode = x.Function.FunctionCode,
                FunctionName = x.Function.FunctionName,
                Detail = x.Function.Detail,
                ModuleCode = x.Function.ModuleCode,
                ActionCode = x.Function.ActionCode,
                ScopeCode = x.EffectiveScope,
                AccessMode = x.EffectiveAccessMode,
                DisplayOrder = x.Function.DisplayOrder,
                IsSystemCritical = x.Function.IsSystemCritical
            })
            .ToList();

        // Effective capability = role grant OR active operator assignment (for capability-granting
        // functions such as Execution.Review). Merged HERE so every HasAsync/permission-snapshot
        // consumer (API guards, runtime services, UI menu) follows the same formula.
        var grantingCodes = FeatureOperatorCatalog.CapabilityGrantingFunctionCodes;
        if (hasActiveEmployee && grantingCodes.Count > 0 && !string.IsNullOrWhiteSpace(snapshotEmployeeCode))
        {
            var assignmentGranted = await GetAssignmentGrantedFunctionsAsync(snapshotEmployeeCode!, grantingCodes, ct);
            foreach (var granted in assignmentGranted)
            {
                if (functions.All(x => x.FunctionCode != granted.FunctionCode))
                    functions.Add(granted);
            }
        }

        var permissionCode = await _uow.Repository<F03User>().Query()
            .Where(u => u.Id == userId)
            .Select(u => (int?)u.PermissionCode)
            .FirstOrDefaultAsync(ct);

        return new PermissionSnapshotDto
        {
            UserId = userId,
            PermissionCode = permissionCode,
            RoleCodes = roleCodes,
            Functions = functions,
            FunctionCodes = functions.Select(x => x.FunctionCode).ToHashSet()
        };
    }

    public async Task<List<SecurityRoleDto>> GetRolesAsync(CancellationToken ct = default)
    {
        var roles = await _uow.Repository<F03Role>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true)
            .OrderBy(x => x.RoleCode)
            .ToListAsync(ct);

        var roleIds = roles.Select(x => x.Id).ToList();
        var map = await (
            from rf in _uow.Repository<F03RoleFunction>().Query().AsNoTracking()
            join f in _uow.Repository<F03Function>().Query().AsNoTracking()
                on rf.IdFunction equals f.Id
            where roleIds.Contains(rf.IdRole)
                && rf.IsActive == true
                && (f.IsActive ?? true)
            select new
            {
                rf.IdRole,
                f.FunctionCode,
                EffectiveScope = NormalizeScopeCode(rf.ScopeCode ?? f.ScopeCode ?? AuthorizationScopeCodes.None),
                EffectiveAccessMode = rf.AccessMode
                    ?? (((rf.ScopeCode ?? f.ScopeCode) == AuthorizationScopeCodes.Own
                        || (rf.ScopeCode ?? f.ScopeCode) == AuthorizationScopeCodes.Employee)
                        ? "Personal"
                        : "Management")
            }
        ).ToListAsync(ct);

        return roles.Select(r => new SecurityRoleDto(
            r.Id,
            r.RoleCode,
            r.RoleName,
            r.Detail,
            r.IsSystem,
            r.IsActive == true)
        {
            FunctionCodes = map.Where(x => x.IdRole == r.Id)
                .Select(x => x.FunctionCode).Distinct().OrderBy(x => x).ToList(),
            FunctionScopes = map.Where(x => x.IdRole == r.Id)
                .GroupBy(x => x.FunctionCode)
                .ToDictionary(x => x.Key, x => x.First().EffectiveScope),
            FunctionAccessModes = map.Where(x => x.IdRole == r.Id)
                .GroupBy(x => x.FunctionCode)
                .ToDictionary(x => x.Key, x => x.First().EffectiveAccessMode)
        }).ToList();
    }

    public async Task<List<SecurityFunctionDto>> GetFunctionsAsync(CancellationToken ct = default)
    {
        return await _uow.Repository<F03Function>().Query()
            .AsNoTracking()
            .Where(x => x.IsActive == true)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.FunctionCode)
            .Select(x => new SecurityFunctionDto
            {
                IdFunction = x.Id,
                FunctionCode = x.FunctionCode,
                FunctionName = x.FunctionName,
                Detail = x.Detail,
                ModuleCode = x.ModuleCode,
                ActionCode = x.ActionCode,
                ScopeCode = x.ScopeCode,
                DisplayOrder = x.DisplayOrder,
                IsSystemCritical = x.IsSystemCritical
            })
            .ToListAsync(ct);
    }

    public async Task<List<TwoFactorAdminUserDto>> GetTwoFactorUsersAsync(CancellationToken ct = default)
    {
        var users = await _uow.Repository<F03User>().Query()
            .AsNoTracking()
            .OrderBy(x => x.EmployeeCode)
            .ToListAsync(ct);

        var deptCodes = users.Select(x => x.DeptCode)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
        var positionCodes = users.Select(x => x.Cvcode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var deptNames = await _uow.Repository<F03Department>().Query()
            .AsNoTracking()
            .Where(x => deptCodes.Contains(x.DeptCode))
            .ToDictionaryAsync(x => x.DeptCode, x => x.DeptName, ct);

        var positionNames = await _uow.Repository<F03Position>().Query()
            .AsNoTracking()
            .Where(x => positionCodes.Contains(x.PositionCode))
            .ToDictionaryAsync(x => x.PositionCode, x => x.PositionName, ct);

        return users.Select(x => new TwoFactorAdminUserDto
        {
            UserId = x.Id,
            EmployeeCode = x.EmployeeCode,
            FullName = x.FullName,
            DeptCode = x.DeptCode,
            DeptName = x.DeptCode.HasValue ? deptNames.GetValueOrDefault(x.DeptCode.Value) : null,
            PositionCode = x.Cvcode,
            PositionName = x.Cvcode != null ? positionNames.GetValueOrDefault(x.Cvcode) : null,
            IsActive = x.IsActive == true,
            Required = x.TwoFactorRequired,
            Enabled = x.TwoFactorEnabled,
            RequiredAt = x.TwoFactorRequiredAt,
            EnabledAt = x.TwoFactorEnabledAt
        }).ToList();
    }

    public async Task<PermissionSnapshotDto> SetUserRolesAsync(
        int userId,
        IReadOnlyCollection<int> roleCodes,
        int actorUserId,
        CancellationToken ct = default)
    {
        if (userId == actorUserId)
            throw new InvalidOperationException("Không thể tự thay đổi role của chính mình.");
        if (roleCodes == null || roleCodes.Count == 0)
            throw new InvalidOperationException("Tài khoản phải có ít nhất một role.");

        var roles = await _uow.Repository<F03Role>().Query()
            .Where(x => x.IsActive == true && roleCodes.Contains(x.RoleCode))
            .ToListAsync(ct);

        if (roles.Count != roleCodes.Distinct().Count())
            throw new InvalidOperationException("Một hoặc nhiều role không tồn tại.");

        var repo = _uow.Repository<F03UserRole>();
        var existing = await repo.Query().Where(x => x.IdUser == userId).ToListAsync(ct);
        foreach (var row in existing)
            repo.Remove(row);

        foreach (var role in roles)
        {
            await repo.AddAsync(new F03UserRole
            {
                IdUser = userId,
                IdRole = role.Id,
                IsPrimary = role.RoleCode == roleCodes.FirstOrDefault(),
                CreatedBy = actorUserId,
                CreatedAt = DateTime.Now,
                ModifiedBy = actorUserId,
                ModifiedAt = DateTime.Now
            }, ct);
        }

        // Keep legacy primary PermissionCode synchronized for existing login/UI code.
        var primaryRoleCode = roleCodes.FirstOrDefault();
        var user = await _uow.Repository<F03User>().Query()
            .FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user != null && primaryRoleCode > 0)
        {
            user.PermissionCode = primaryRoleCode;
            user.ModifiedBy = actorUserId;
            user.ModifiedAt = DateTime.Now;
        }

        await _uow.SaveChangesAsync(ct);
        await _auditService.LogAction(
            "SECURITY_USER_ROLES_CHANGED",
            actorUserId,
            $"UserId={userId}; Roles={string.Join(',', roleCodes.Distinct().OrderBy(x => x))}",
            ct: ct);
        await _sessionService.RevokeAllAsync(userId, ct);
        return await GetSnapshotAsync(userId, ct);
    }
    public async Task<SecurityRoleDto> SetRoleFunctionsAsync(
        int roleCode,
        IReadOnlyCollection<int> functionCodes,
        IReadOnlyDictionary<int, string?> scopeOverrides,
        IReadOnlyDictionary<int, string?> accessModeOverrides,
        int actorUserId,
        CancellationToken ct = default)
    {
        if (functionCodes == null)
            throw new InvalidOperationException("FunctionCodes không hợp lệ.");

        var role = await _uow.Repository<F03Role>().Query()
            .FirstOrDefaultAsync(x => x.RoleCode == roleCode && x.IsActive == true, ct);

        if (role == null)
            throw new InvalidOperationException("Role không tồn tại hoặc đã ngừng hoạt động.");

        var codes = functionCodes.Distinct().ToList();

        // SuperAdmin is the final security break-glass role. Its system-critical
        // Security capabilities are not optional and cannot be removed by the matrix.
        if (roleCode == 1)
            codes = codes.Union(SecurityFunctionCodes.SystemCriticalCodes).Distinct().ToList();

        var normalizedScopeOverrides = (scopeOverrides ?? new Dictionary<int, string?>())
            .ToDictionary(x => x.Key, x => NormalizeScopeCode(x.Value));
        var normalizedAccessModeOverrides = accessModeOverrides ?? new Dictionary<int, string?>();
        var allowedScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AuthorizationScopeCodes.Own,
            AuthorizationScopeCodes.Employee,
            AuthorizationScopeCodes.Department,
            AuthorizationScopeCodes.All
        };

        foreach (var overridePair in normalizedScopeOverrides)
        {
            if (!codes.Contains(overridePair.Key))
                throw new InvalidOperationException($"Scope override không thuộc FunctionCodes: {overridePair.Key}.");

            if (!string.IsNullOrWhiteSpace(overridePair.Value)
                && !allowedScopes.Contains(overridePair.Value.Trim()))
                throw new InvalidOperationException($"ScopeCode không hợp lệ: {overridePair.Value}.");
        }

        var allowedAccessModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Personal",
            "Management"
        };
        foreach (var modePair in normalizedAccessModeOverrides)
        {
            if (!codes.Contains(modePair.Key))
                throw new InvalidOperationException($"AccessMode override không thuộc FunctionCodes: {modePair.Key}.");
            if (!string.IsNullOrWhiteSpace(modePair.Value) && !allowedAccessModes.Contains(modePair.Value.Trim()))
                throw new InvalidOperationException($"AccessMode không hợp lệ: {modePair.Value}.");
        }

        var functions = await _uow.Repository<F03Function>().Query()
            .Where(x => codes.Contains(x.FunctionCode) && (x.IsActive ?? true))
            .ToListAsync(ct);

        if (functions.Count != codes.Count)
        {
            var validCodes = functions
                .Select(x => x.FunctionCode)
                .ToHashSet();

            var invalidCodes = codes
                .Where(x => !validCodes.Contains(x))
                .OrderBy(x => x)
                .ToList();

            throw new InvalidOperationException(
                $"Function không tồn tại hoặc đã ngừng hoạt động: {string.Join(", ", invalidCodes)}.");
        }

        var repo = _uow.Repository<F03RoleFunction>();
        var existing = await repo.Query().Where(x => x.IdRole == role.Id).ToListAsync(ct);
        var existingByFunction = existing.ToDictionary(x => x.IdFunction);

        foreach (var row in existing)
        {
            var function = functions.FirstOrDefault(x => x.Id == row.IdFunction);
            var isCritical = function?.IsSystemCritical == true;

            // Never delete SuperAdmin's critical grants. Other role grants remain
            // fully replaceable by the matrix.
            if (roleCode == 1 && isCritical)
                continue;

            repo.Remove(row);
        }

        foreach (var function in functions)
        {
            var isCriticalSuperAdmin = roleCode == 1 && function.IsSystemCritical;

            if (isCriticalSuperAdmin && existingByFunction.TryGetValue(function.Id, out var existingCritical))
            {
                existingCritical.IsActive = true;
                existingCritical.ScopeCode = null;
                existingCritical.AccessMode = null;
                existingCritical.ModifiedBy = actorUserId;
                existingCritical.ModifiedAt = DateTime.Now;
                continue;
            }

            await repo.AddAsync(new F03RoleFunction
            {
                IdRole = role.Id,
                IdFunction = function.Id,
                ScopeCode = normalizedScopeOverrides.TryGetValue(function.FunctionCode, out var scope) && !string.IsNullOrWhiteSpace(scope)
                    ? scope.Trim()
                    : null,
                AccessMode = normalizedAccessModeOverrides.TryGetValue(function.FunctionCode, out var mode) && !string.IsNullOrWhiteSpace(mode)
                    ? mode.Trim()
                    : null,
                CreatedBy = actorUserId,
                CreatedAt = DateTime.Now
            }, ct);
        }

        role.ModifiedBy = actorUserId;
        role.ModifiedAt = DateTime.Now;

        await _uow.SaveChangesAsync(ct);
        await _auditService.LogAction(
            "SECURITY_ROLE_FUNCTIONS_CHANGED",
            actorUserId,
            $"RoleCode={roleCode}; Functions={string.Join(',', codes.OrderBy(x => x))}",
            ct: ct);
        return (await GetRolesAsync(ct)).Single(x => x.RoleCode == roleCode);
    }

}
