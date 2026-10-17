using AppAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;
using FVN_REGISTER.Application.Interfaces.OT;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Positions;
using FVN_REGISTER.Contract.Dtos.Registration;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Entities.OT;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/registration-policy")]
public sealed class RegistrationPolicyController : BaseApiController
{
    private readonly FVNWEBAPPContext _db;
    private readonly AppAuthorizationService _authorization;
    private readonly IOtRegistrationPolicy _policy;

    public RegistrationPolicyController(
        FVNWEBAPPContext db,
        ICurrentUserService currentUser,
        IUserLogService userLog,
        ILogger<RegistrationPolicyController> logger,
        Microsoft.Extensions.Options.IOptionsMonitor<Application.Configuration.AuthDebugOptions> options,
        AppAuthorizationService authorization,
        IOtRegistrationPolicy policy)
        : base(currentUser, userLog, logger, options)
    {
        _db = db;
        _authorization = authorization;
        _policy = policy;
    }

    /// <summary>
    /// Quyền đăng ký của user hiện tại. Dùng CHUNG IOtRegistrationPolicy với OTController/TripController/Calendar
    /// (chức vụ approver, rule phòng ban và Feature Operator OT.RegistrarManage) để UI và API không lệch nhau.
    /// OT: không phải registrar thì không đăng ký được (kể cả cho chính mình) và phải báo cấp trên / người được chỉ định.
    /// TRIP: đi một mình ai có quyền TripCreate cũng tạo được; thêm người đi cùng phải là registrar.
    /// </summary>
    [HttpGet("decision")]
    public async Task<IActionResult> Decision([FromQuery] string registerType = "OT", [FromQuery] int? deptCode = null, CancellationToken ct = default)
    {
        if (UserInfo == null) return Unauthorized();
        var type = NormalizeType(registerType);
        var dept = deptCode ?? UserInfo.DeptCode ?? 0;
        var decision = await _policy.GetDecisionAsync(UserInfo, dept, type, ct);
        var canCreate = await _authorization.HasPersonalAsync(UserInfo, type == "OT" ? SecurityFunctionCodes.OTCreate : SecurityFunctionCodes.TripCreate, ct);
        var isRegistrar = decision.CanRegisterForOthers;
        var canSelfRegisterByPosition = false;
        if (type == "OT" && !string.IsNullOrWhiteSpace(UserInfo.PositionCode))
        {
            canSelfRegisterByPosition = await _db.Database.SqlQuery<int>($"""
                SELECT COUNT(1) AS [Value]
                FROM dbo.F03OTSelfRegistrationPositions p
                WHERE p.IsActive=1 AND p.RegisterType=N'OT'
                  AND p.PositionCode={UserInfo.PositionCode.Trim()}
                  AND p.DepartmentCode IN (0,{dept})
                """).SingleAsync(ct) > 0;
        }

        decision.CanRegisterForOthers = canCreate && isRegistrar;
        decision.CanRegisterSelfOnly = canCreate && !isRegistrar
            && (type == "TRIP" || canSelfRegisterByPosition);
        decision.CanRegister = canCreate && (type == "TRIP" || isRegistrar || canSelfRegisterByPosition);
        return Ok(ApiResponse<RegistrationPolicyDecisionDto>.Ok(decision));
    }

    /// <summary>User có phải registrar của ít nhất một phòng ban không (UI dùng để hiện menu / dashboard / hộp yêu cầu).</summary>
    [HttpGet("is-registrar")]
    public async Task<IActionResult> IsRegistrar([FromQuery] string registerType = "OT", CancellationToken ct = default)
    {
        if (UserInfo == null) return Unauthorized();
        return Ok(ApiResponse<bool>.Ok(await _policy.IsAnyRegistrarAsync(UserInfo, NormalizeType(registerType), ct)));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string registerType = "OT", CancellationToken ct = default)
    {
        if (UserInfo == null) return Unauthorized();
        if (!await _authorization.HasAsync(UserInfo, SecurityFunctionCodes.OTRegistrarManage, ct)) return Forbid();

        var type = NormalizeType(registerType);
        var rules = await _db.Set<F03OTRegistrationRule>()
            .AsNoTracking()
            .Include(x => x.Registrars)
            .Include(x => x.RegistrarPositions)
            .Where(x => x.RegisterType == type)
            .OrderBy(x => x.DepartmentCode)
            .ToListAsync(ct);

        var result = rules.Select(ToDto).ToList();
        var selfPositions = await _db.Database.SqlQueryRaw<SelfPositionRow>(
            "SELECT DepartmentCode, RegisterType, PositionCode FROM dbo.F03OTSelfRegistrationPositions WHERE IsActive=1 AND RegisterType=@type",
            new Microsoft.Data.SqlClient.SqlParameter("@type", type)).ToListAsync(ct);
        foreach (var item in result)
            item.SelfRegistrationPositionCodes = selfPositions
                .Where(x => x.DepartmentCode == item.DepartmentCode && string.Equals(x.RegisterType, item.RegisterType, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.PositionCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return Ok(ApiResponse<List<RegistrationPolicyDto>>.Ok(result));
    }

    /// <summary>
    /// Danh sách chức vụ đang hoạt động để chọn "chức vụ được phép đăng ký OT/Trip" (multi-select ở Security Center).
    /// </summary>
    [HttpGet("positions")]
    public async Task<IActionResult> Positions(CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        if (!await _authorization.HasAsync(UserInfo, SecurityFunctionCodes.OTRegistrarManage, ct)) return Forbid();

        var positions = await _db.Set<F03Position>()
            .AsNoTracking()
            .Where(x => x.IsActive == true)
            .OrderBy(x => x.PositionCode)
            .Select(x => new PositionDto
            {
                Id = x.Id,
                PositionCode = x.PositionCode,
                PositionName = x.PositionName,
                IsApprove = x.IsApprove,
                IsAllowApprove = x.IsAllowApprove,
                IsActive = true,
                EmployeeCount = _db.Set<F03Employee>().Count(e => e.PositionCode == x.PositionCode && e.IsActive == true)
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<List<PositionDto>>.Ok(positions));
    }

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] RegistrationPolicyUpsertDto dto, CancellationToken ct)
    {
        if (UserInfo == null) return Unauthorized();
        if (!await _authorization.HasAsync(UserInfo, SecurityFunctionCodes.OTRegistrarManage, ct)) return Forbid();

        var type = NormalizeType(dto.RegisterType);
        if (dto.DepartmentCode < 0) return BadRequest(ApiResponse<object>.Fail("DepartmentCode không hợp lệ."));
        if (dto.Mode == RegistrationRuleMode.AssignedUsers && dto.UserIds.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("Chế độ chỉ định user phải có ít nhất một user."));
        if (dto.Mode == RegistrationRuleMode.AssignedPositions && dto.PositionCodes.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("Chế độ chỉ định chức vụ phải có ít nhất một chức vụ."));

        var rule = await _db.Set<F03OTRegistrationRule>()
            .Include(x => x.Registrars)
            .Include(x => x.RegistrarPositions)
            .FirstOrDefaultAsync(x => x.DepartmentCode == dto.DepartmentCode && x.RegisterType == type, ct);

        if (rule == null)
        {
            rule = new F03OTRegistrationRule
            {
                DepartmentCode = dto.DepartmentCode,
                RegisterType = type,
                CreatedBy = UserInfo.UserId,
                CreatedAt = DateTime.Now
            };
            _db.Add(rule);
        }

        rule.Mode = dto.Mode;
        rule.IsActive = dto.IsActive;
        rule.ModifiedBy = UserInfo.UserId;
        rule.ModifiedAt = DateTime.Now;

        _db.RemoveRange(rule.Registrars);
        _db.RemoveRange(rule.RegistrarPositions);
        rule.Registrars = dto.UserIds.Distinct().Select(id => new F03OTRegistrar
        {
            DepartmentCode = dto.DepartmentCode,
            RegisterType = type,
            UserId = id,
            CreatedBy = UserInfo.UserId,
            CreatedAt = DateTime.Now
        }).ToList();
        rule.RegistrarPositions = dto.PositionCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(position => new F03OTRegistrarPosition
            {
                DepartmentCode = dto.DepartmentCode,
                RegisterType = type,
                PositionCode = position,
                CreatedBy = UserInfo.UserId,
                CreatedAt = DateTime.Now
            }).ToList();

        await _db.SaveChangesAsync(ct);

        if (type == "OT")
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM dbo.F03OTSelfRegistrationPositions WHERE DepartmentCode={dto.DepartmentCode} AND RegisterType={type}", ct);
            foreach (var position in dto.SelfRegistrationPositionCodes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT dbo.F03OTSelfRegistrationPositions(DepartmentCode,RegisterType,PositionCode,IsActive,CreatedAt,CreatedBy) VALUES ({dto.DepartmentCode},{type},{position},1,SYSUTCDATETIME(),{UserInfo.UserId})", ct);
            }
        }

        var response = ToDto(rule);
        if (type == "OT")
            response.SelfRegistrationPositionCodes = dto.SelfRegistrationPositionCodes
                .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return Ok(ApiResponse<RegistrationPolicyDto>.Ok(response));
    }


    public sealed class SelfPositionRow
    {
        public int DepartmentCode { get; set; }
        public string RegisterType { get; set; } = string.Empty;
        public string PositionCode { get; set; } = string.Empty;
    }

    private async Task<F03OTRegistrationRule?> ResolveRuleAsync(int deptCode, string registerType, CancellationToken ct)
        => await _db.Set<F03OTRegistrationRule>()
            .AsNoTracking()
            .Include(x => x.Registrars)
            .Include(x => x.RegistrarPositions)
            .Where(x => x.IsActive && x.RegisterType == registerType && (x.DepartmentCode == deptCode || x.DepartmentCode == 0))
            .OrderByDescending(x => x.DepartmentCode == deptCode)
            .FirstOrDefaultAsync(ct);

    private static bool MatchesOthers(F03OTRegistrationRule? rule, UserIdentityDto user)
    {
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

    private static RegistrationPolicyDto ToDto(F03OTRegistrationRule rule) => new()
    {
        Id = rule.Id,
        DepartmentCode = rule.DepartmentCode,
        RegisterType = rule.RegisterType,
        Mode = rule.Mode,
        IsActive = rule.IsActive,
        UserIds = rule.Registrars.Select(x => x.UserId).Distinct().ToList(),
        PositionCodes = rule.RegistrarPositions.Select(x => x.PositionCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
    };

    private static string NormalizeType(string value)
        => string.Equals(value, "TRIP", StringComparison.OrdinalIgnoreCase) ? "TRIP" : "OT";
}
