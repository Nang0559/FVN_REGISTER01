using System.Globalization;
using FVN_REGISTER.AI.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>
/// Settings for <see cref="FvnKnowledgeAccessProvider"/>. Bound from
/// <c>AIPlatform:KnowledgeAuthorization</c>.
/// </summary>
public sealed class FvnKnowledgeAuthorizationOptions
{
    public const string SectionName = "AIPlatform:KnowledgeAuthorization";

    /// <summary>
    /// Connection string name of the database that holds the FVN security tables (dbo.F03*).
    /// When it is not configured the AI connection string is used, which is correct only when the
    /// AI schema lives in the same database as FVN_REGISTER.
    /// </summary>
    public string ConnectionStringName { get; set; } = "FvnRegister";

    /// <summary>SecurityFunctionCodes.AIAssistantChat. Required to search the knowledge base.</summary>
    public int ChatFunctionCode { get; set; } = 3510;

    /// <summary>
    /// SecurityFunctionCodes.AIDocumentDataEntryTemplateManage. Required, with data scope "All",
    /// to publish or unpublish knowledge for the whole company.
    /// </summary>
    public int PublishFunctionCode { get; set; } = 3502;

    /// <summary>ACL scope carried by chunks that every authorized FVN user may read.</summary>
    public string StaffScope { get; set; } = "fvn:staff";
}

/// <summary>
/// Maps the effective FVN_REGISTER RBAC (User -> Role -> Function) to knowledge ACL scopes.
/// It reads the same tables and applies the same activity rules as FVN's AuthorizationService.
/// The actor always comes from the HMAC-validated gateway header, never from request JSON.
///
/// Scope vocabulary stored in chunk ACLs and matched at search time:
///   fvn:staff          any active FVN user who holds AIAssistant.Chat
///   fvn:role:{code}    members of the FVN role with that RoleCode
///   fvn:dept:{code}    users whose F03Users.DeptCode equals the code
///
/// Fail closed: any SQL problem surfaces as KNOWLEDGE_AUTHORIZATION_PROVIDER_NOT_CONFIGURED,
/// which the endpoints already translate to HTTP 503.
/// </summary>
public sealed class FvnKnowledgeAccessProvider : IAuthorizedKnowledgeAccessProvider
{
    private const string NotConfiguredMessage = "KNOWLEDGE_AUTHORIZATION_PROVIDER_NOT_CONFIGURED";
    private const string AllScope = "All";
    private const string OwnScope = "Own";

    private readonly string _connectionString;
    private readonly FvnKnowledgeAuthorizationOptions _options;
    private readonly ILogger<FvnKnowledgeAccessProvider> _logger;

    public FvnKnowledgeAccessProvider(
        string connectionString,
        FvnKnowledgeAuthorizationOptions options,
        ILogger<FvnKnowledgeAccessProvider> logger)
    {
        _connectionString = connectionString;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<string>> GetSearchScopesAsync(
        string tenantCode, string actorId, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(actorId, out var userId)) return Array.Empty<string>();
        var state = await LoadAsync(userId, cancellationToken);
        if (!state.IsActive || !HasEffectiveChat(state)) return Array.Empty<string>();

        var scopes = new List<string> { _options.StaffScope };
        scopes.AddRange(state.RoleCodes.Select(code => $"fvn:role:{code.ToString(CultureInfo.InvariantCulture)}"));
        if (state.DeptCode is int dept) scopes.Add($"fvn:dept:{dept.ToString(CultureInfo.InvariantCulture)}");
        return scopes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyCollection<string>> GetPublicationScopesAsync(
        string tenantCode, string actorId, Guid documentId, Guid reviewId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(actorId, out var userId)) return Array.Empty<string>();
        var state = await LoadAsync(userId, cancellationToken);
        if (!state.IsActive || !HasCompanyWidePublish(state)) return Array.Empty<string>();

        // Publishing is an explicit, human-approved act by an administrator with data scope All,
        // so the published audience is every FVN user who may use the assistant.
        return new[] { _options.StaffScope };
    }

    public async Task AuthorizeUnpublishAsync(
        string tenantCode, string actorId, Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(actorId, out var userId)) throw new UnauthorizedAccessException();
        var state = await LoadAsync(userId, cancellationToken);
        if (!state.IsActive || !HasCompanyWidePublish(state)) throw new UnauthorizedAccessException();
    }

    private bool HasEffectiveChat(UserState state) =>
        state.Grants.Any(g => g.FunctionCode == _options.ChatFunctionCode
            // FVN only treats personal ("Own") grants as effective for an account that
            // resolves to an active HRM employee, so the same rule applies here.
            && (!string.Equals(g.Scope, OwnScope, StringComparison.OrdinalIgnoreCase) || state.HasActiveEmployee));

    private bool HasCompanyWidePublish(UserState state) =>
        state.Grants.Any(g => g.FunctionCode == _options.PublishFunctionCode
            && string.Equals(g.Scope, AllScope, StringComparison.OrdinalIgnoreCase));

    private static bool TryGetUserId(string actorId, out int userId) =>
        int.TryParse(actorId, NumberStyles.None, CultureInfo.InvariantCulture, out userId) && userId > 0;

    private async Task<UserState> LoadAsync(int userId, CancellationToken ct)
    {
        const string sql = """
            SELECT u.IsActive, u.DeptCode,
                   CASE WHEN EXISTS (SELECT 1 FROM dbo.F03Employees e
                                     WHERE e.IsActive = 1 AND e.EmployeeCode = u.EmployeeCode)
                        THEN 1 ELSE 0 END AS HasActiveEmployee
            FROM dbo.F03Users u WHERE u.Id = @userId;

            SELECT DISTINCT r.RoleCode
            FROM dbo.F03UserRoles ur
            JOIN dbo.F03Roles r ON r.Id = ur.IdRole
            WHERE ur.IdUser = @userId AND ur.IsActive = 1 AND r.IsActive = 1;

            SELECT DISTINCT f.FunctionCode, COALESCE(rf.ScopeCode, f.ScopeCode) AS EffectiveScope
            FROM dbo.F03UserRoles ur
            JOIN dbo.F03RoleFunctions rf ON rf.IdRole = ur.IdRole
            JOIN dbo.F03Roles r ON r.Id = ur.IdRole
            JOIN dbo.F03Functions f ON f.Id = rf.IdFunction
            WHERE ur.IdUser = @userId AND ur.IsActive = 1 AND rf.IsActive = 1 AND r.IsActive = 1
              AND (f.IsActive IS NULL OR f.IsActive = 1)
              AND f.FunctionCode IN (@chatCode, @publishCode);
            """;

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@chatCode", _options.ChatFunctionCode);
            command.Parameters.AddWithValue("@publishCode", _options.PublishFunctionCode);

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return UserState.Inactive;

            var isActive = !reader.IsDBNull(0) && reader.GetBoolean(0);
            int? deptCode = reader.IsDBNull(1) ? null : reader.GetInt32(1);
            var hasActiveEmployee = reader.GetInt32(2) == 1;

            var roleCodes = new List<int>();
            await reader.NextResultAsync(ct);
            while (await reader.ReadAsync(ct)) roleCodes.Add(reader.GetInt32(0));

            var grants = new List<Grant>();
            await reader.NextResultAsync(ct);
            while (await reader.ReadAsync(ct))
                grants.Add(new Grant(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1)?.Trim()));

            return new UserState(isActive, deptCode, hasActiveEmployee, roleCodes, grants);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex,
                "FVN knowledge authorization could not read the FVN security tables. Check ConnectionStrings:{Name}.",
                _options.ConnectionStringName);
            throw new InvalidOperationException(NotConfiguredMessage, ex);
        }
    }

    private sealed record Grant(int FunctionCode, string? Scope);

    private sealed record UserState(
        bool IsActive, int? DeptCode, bool HasActiveEmployee,
        IReadOnlyList<int> RoleCodes, IReadOnlyList<Grant> Grants)
    {
        public static readonly UserState Inactive =
            new(false, null, false, Array.Empty<int>(), Array.Empty<Grant>());
    }
}
