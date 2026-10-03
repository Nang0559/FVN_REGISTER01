using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.Security;
using FVN_REGISTER.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.API.Controllers;

[ApiController]
[Route("api/security/endpoints/lanscope")]
[Authorize]
public sealed class LanscopeDeploymentController : ControllerBase
{
    private readonly FVNWEBAPPContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService _authorization;
    private readonly IConfiguration _configuration;

    public LanscopeDeploymentController(FVNWEBAPPContext db, ICurrentUserService currentUser, FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService authorization, IConfiguration configuration)
    {
        _db=db; _currentUser=currentUser; _authorization=authorization; _configuration=configuration;
    }

    [HttpPost("deployments")]
    public async Task<ActionResult<ApiResponse<LanscopeDeploymentViewDto>>> CreateDeployment(LanscopeDeploymentCreateRequest request, CancellationToken ct)
    {
        var user=await RequireAsync(SecurityFunctionCodes.EndpointLanscopeDeploymentManage,ct);
        if(user==null)return Forbid();
        if(string.IsNullOrWhiteSpace(request.DeploymentCode))return BadRequest(ApiResponse<LanscopeDeploymentViewDto>.Fail("DeploymentCode là bắt buộc."));
        var code=request.DeploymentCode.Trim();
        if(await _db.EndpointDeployments.AnyAsync(x=>x.DeploymentCode==code,ct))return Conflict(ApiResponse<LanscopeDeploymentViewDto>.Fail("DeploymentCode đã tồn tại.",409));
        var entity=new F03EndpointDeployment{DeploymentCode=code,PackageVersion=Trim(request.PackageVersion,50),Status="Ready",CreatedBy=user.Id,CreatedAtUtc=DateTime.UtcNow,ExpiresAtUtc=request.ExpiresAtUtc?.UtcDateTime};
        _db.EndpointDeployments.Add(entity);await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<LanscopeDeploymentViewDto>.Ok(await ToViewAsync(entity.Id,ct)));
    }

    [HttpPost("deployments/{deploymentId:int}/targets")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LanscopeDeploymentTargetResult>>>> CreateTargets(int deploymentId,[FromBody] IReadOnlyList<LanscopeDeploymentTargetRequest> requests,CancellationToken ct)
    {
        if(await RequireAsync(SecurityFunctionCodes.EndpointLanscopeDeploymentManage,ct)==null)return Forbid();
        if(requests.Count<1||requests.Count>5000)return BadRequest(ApiResponse<IReadOnlyList<LanscopeDeploymentTargetResult>>.Fail("Số target phải từ 1 đến 5000."));
        var deployment=await _db.EndpointDeployments.FirstOrDefaultAsync(x=>x.Id==deploymentId,ct);
        if(deployment==null)return NotFound(ApiResponse<IReadOnlyList<LanscopeDeploymentTargetResult>>.Fail("Không tìm thấy deployment.",404));
        if(deployment.ExpiresAtUtc<=DateTime.UtcNow&&deployment.ExpiresAtUtc.HasValue)return BadRequest(ApiResponse<IReadOnlyList<LanscopeDeploymentTargetResult>>.Fail("Deployment đã hết hạn."));
        await using var tx=await _db.Database.BeginTransactionAsync(ct);
        var normalized = requests.Select(x => x.TargetId?.Trim() ?? string.Empty).ToArray();
        if (normalized.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100))
            return BadRequest(ApiResponse<IReadOnlyList<LanscopeDeploymentTargetResult>>.Fail("Mỗi TargetId phải có từ 1 đến 100 ký tự."));
        var duplicateKeys = normalized.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (duplicateKeys.Length > 0)
            return Conflict(ApiResponse<IReadOnlyList<LanscopeDeploymentTargetResult>>.Fail($"TargetId trùng trong batch: {string.Join(", ", duplicateKeys)}",409));
        var existingKeys = await _db.EndpointDeploymentTargets.AsNoTracking()
            .Where(x => x.DeploymentId == deploymentId && normalized.Contains(x.TargetKey))
            .Select(x => x.TargetKey).ToListAsync(ct);
        if (existingKeys.Count > 0)
            return Conflict(ApiResponse<IReadOnlyList<LanscopeDeploymentTargetResult>>.Fail($"Target đã tồn tại: {string.Join(", ", existingKeys)}",409));

        var targets = new List<(string Key, LanscopeClientInventoryDto Client, F03EndpointDeploymentTarget Entity)>();
        for (var i = 0; i < requests.Count; i++)
        {
            var client=requests[i].Client;
            var target=new F03EndpointDeploymentTarget{
                DeploymentId=deploymentId,TargetKey=normalized[i],ClientId=Trim(client.ClientId,100),ComputerName=Trim(client.ComputerName,255),
                IpAddress=Trim(client.Ip,100),MacAddress=Trim(client.Mac,100),SerialNumber=Trim(client.SerialNumber,255),WindowsUser=Trim(client.WindowsUser,255),
                DomainName=Trim(client.Domain,255),OrganizationalUnit=Trim(client.Ou,500),LanscopeGroup=Trim(client.Group,255),OsName=Trim(client.Os,255),
                Manufacturer=Trim(client.Manufacturer,255),Model=Trim(client.Model,255),Status="Pending",CreatedAtUtc=DateTime.UtcNow};
            targets.Add((normalized[i],client,target));
            _db.EndpointDeploymentTargets.Add(target);
        }
        await _db.SaveChangesAsync(ct);

        var results=new List<LanscopeDeploymentTargetResult>();
        var apiBase=GetPublicBaseUrl();
        var tokenMinutes=GetTokenMinutes();
        foreach(var item in targets)
        {
            var token=GenerateSecret(32);
            _db.EndpointEnrollmentTokens.Add(new F03EndpointEnrollmentToken{DeploymentId=deploymentId,TargetId=item.Entity.Id,TokenHash=Hash(token),ExpiresAtUtc=DateTime.UtcNow.AddMinutes(tokenMinutes),CreatedAtUtc=DateTime.UtcNow});
            var bootstrap=JsonSerializer.Serialize(new{deploymentId,targetId=item.Entity.Id,enrollmentToken=token,apiBaseUrl=apiBase,clientId=item.Client.ClientId});
            results.Add(new LanscopeDeploymentTargetResult(item.Entity.Id,item.Key,token,$"fvn-bootstrap-{SafeFile(item.Key)}.json",bootstrap));
        }
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<LanscopeDeploymentTargetResult>>.Ok(results));
    }

    [HttpGet("deployments/{deploymentId:int}")]
    public async Task<ActionResult<ApiResponse<LanscopeDeploymentViewDto>>> GetDeployment(int deploymentId,CancellationToken ct)
    {
        if(await RequireAsync(SecurityFunctionCodes.EndpointLanscopeDeploymentView,ct)==null)return Forbid();
        var result=await ToViewAsync(deploymentId,ct);return result==null?NotFound(ApiResponse<LanscopeDeploymentViewDto>.Fail("Không tìm thấy deployment.",404)):Ok(ApiResponse<LanscopeDeploymentViewDto>.Ok(result));
    }

    [HttpGet("deployments/{deploymentId:int}/targets")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LanscopeDeploymentTargetViewDto>>>> GetTargets(int deploymentId,CancellationToken ct)
    {
        if(await RequireAsync(SecurityFunctionCodes.EndpointLanscopeDeploymentView,ct)==null)return Forbid();
        var rows=await _db.EndpointDeploymentTargets.AsNoTracking().Where(x=>x.DeploymentId==deploymentId).OrderBy(x=>x.Id)
            .Select(x=>new LanscopeDeploymentTargetViewDto(x.Id,x.TargetKey,x.ClientId,x.ComputerName,x.SerialNumber,x.Status,_db.EndpointEnrollmentTokens.Where(t=>t.TargetId==x.Id&&t.UsedAtUtc==null).OrderByDescending(t=>t.CreatedAtUtc).Select(t=>(DateTimeOffset?)new DateTimeOffset(t.ExpiresAtUtc,TimeSpan.Zero)).FirstOrDefault(),x.EnrolledAtUtc,x.EndpointDeviceId)).ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<LanscopeDeploymentTargetViewDto>>.Ok(rows));
    }

    [AllowAnonymous]
    [HttpPost("enroll")]
    public async Task<ActionResult<ApiResponse<LanscopeEnrollmentResponseDto>>> Enroll(LanscopeEnrollmentRequestDto request,CancellationToken ct)
    {
        if(request.DeploymentId<=0||request.TargetId<=0||string.IsNullOrWhiteSpace(request.EnrollmentToken))return BadRequest(ApiResponse<LanscopeEnrollmentResponseDto>.Fail("Bootstrap enrollment request không hợp lệ."));
        await using var tx=await _db.Database.BeginTransactionAsync(ct);
        var tokenHash=Hash(request.EnrollmentToken.Trim());
        var token=await _db.EndpointEnrollmentTokens.FirstOrDefaultAsync(x=>x.DeploymentId==request.DeploymentId&&x.TargetId==request.TargetId&&x.TokenHash==tokenHash&&x.UsedAtUtc==null&&x.ExpiresAtUtc>DateTime.UtcNow,ct);
        if(token==null)return Unauthorized(ApiResponse<LanscopeEnrollmentResponseDto>.Fail("Bootstrap token không hợp lệ, đã dùng hoặc đã hết hạn.",401));
        var claimed=await _db.Database.ExecuteSqlRawAsync("UPDATE dbo.F03EndpointEnrollmentTokens SET UsedAtUtc=SYSUTCDATETIME() WHERE Id={0} AND UsedAtUtc IS NULL AND ExpiresAtUtc>SYSUTCDATETIME();",new object[]{token.Id},ct);
        if(claimed!=1){await tx.RollbackAsync(ct);return Unauthorized(ApiResponse<LanscopeEnrollmentResponseDto>.Fail("Bootstrap token đã được sử dụng.",401));}
        var target=await _db.EndpointDeploymentTargets.FirstOrDefaultAsync(x=>x.Id==request.TargetId&&x.DeploymentId==request.DeploymentId,ct);
        var deployment=await _db.EndpointDeployments.FirstOrDefaultAsync(x=>x.Id==request.DeploymentId,ct);
        if(target==null||deployment==null){await tx.RollbackAsync(ct);return NotFound(ApiResponse<LanscopeEnrollmentResponseDto>.Fail("Deployment target không tồn tại.",404));}
        if(deployment.ExpiresAtUtc<=DateTime.UtcNow&&deployment.ExpiresAtUtc.HasValue){await tx.RollbackAsync(ct);return Unauthorized(ApiResponse<LanscopeEnrollmentResponseDto>.Fail("Deployment đã hết hạn.",401));}
        var serialMatches=!string.IsNullOrWhiteSpace(target.SerialNumber)&&!string.IsNullOrWhiteSpace(request.SerialNumber)&&string.Equals(target.SerialNumber.Trim(),request.SerialNumber.Trim(),StringComparison.OrdinalIgnoreCase);
        var identityStatus=serialMatches?"Verified":"PendingReview";
        var device=target.EndpointDeviceId.HasValue?await _db.EndpointDevices.FirstOrDefaultAsync(x=>x.Id==target.EndpointDeviceId.Value,ct):null;
        if(device==null){device=new F03EndpointDevice{DeviceKey="LAN-"+Guid.NewGuid().ToString("N"),CreatedAt=DateTime.UtcNow,Source="LANSCOPE"};_db.EndpointDevices.Add(device);}
        device.LanscopeClientId=Trim(request.ClientId,100) ?? target.ClientId;device.ComputerName=Trim(request.ComputerName,255) ?? target.ComputerName;device.IpAddress=Trim(request.Ip,100) ?? target.IpAddress;device.MacAddress=Trim(request.Mac,100) ?? target.MacAddress;
        device.SerialNumber=Trim(request.SerialNumber,255) ?? target.SerialNumber;device.HardwareUuid=Trim(request.HardwareUuid,255);device.AgentInstallationId=Trim(request.AgentInstallationId,100);device.WindowsUser=Trim(request.WindowsUser,255) ?? target.WindowsUser;
        device.DomainName=Trim(request.Domain,255) ?? target.DomainName;device.OrganizationalUnit=Trim(request.Ou,500) ?? target.OrganizationalUnit;device.LanscopeGroup=Trim(request.Group,255) ?? target.LanscopeGroup;device.OsName=Trim(request.Os,255) ?? target.OsName;
        device.Manufacturer=Trim(request.Manufacturer,255) ?? target.Manufacturer;device.Model=Trim(request.Model,255) ?? target.Model;device.AgentVersion=Trim(request.AgentVersion,50);device.LastSeenUtc=DateTime.UtcNow;
        device.Status="Online";device.IdentityStatus=identityStatus;device.Source="LANSCOPE";
        await _db.SaveChangesAsync(ct);
        target.EndpointDeviceId=device.Id;target.Status=identityStatus=="Verified"?"Enrolled":"PendingReview";target.EnrolledAtUtc=DateTime.UtcNow;
        var secret=GenerateSecret(48);var hash=SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        await _db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.F03EndpointCredentials(EndpointDeviceId,SecretHash,CreatedAtUtc,ExpiresAtUtc,CreatedBy,RevokedBy,GraceExpiresAtUtc) VALUES ({0},{1},SYSUTCDATETIME(),DATEADD(YEAR,1,SYSUTCDATETIME()),NULL,NULL,NULL);",new object[]{device.Id,hash},ct);
        await _db.Database.ExecuteSqlRawAsync("INSERT INTO dbo.F03EndpointCredentialAudit(EndpointCredentialId,EndpointDeviceId,ActionCode,ActorUserId,OccurredAtUtc,Detail) SELECT TOP (1) Id,{0},N'Provision-LANSCOPE',NULL,SYSUTCDATETIME(),CONCAT(N'DeploymentId=',{1},N';TargetId=',{2}) FROM dbo.F03EndpointCredentials WHERE EndpointDeviceId={0} ORDER BY Id DESC;",new object[]{device.Id,request.DeploymentId,request.TargetId},ct);
        await _db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return Ok(ApiResponse<LanscopeEnrollmentResponseDto>.Ok(new LanscopeEnrollmentResponseDto(device.DeviceKey,secret,DateTimeOffset.UtcNow.AddYears(1),identityStatus)));
    }

    private async Task<F03User?> RequireAsync(int code,CancellationToken ct){var user=_currentUser.GetCurrentUser();return user!=null&&await _authorization.HasAsync(user,code,ct)?user:null;}
    private async Task<LanscopeDeploymentViewDto?> ToViewAsync(int id,CancellationToken ct){var d=await _db.EndpointDeployments.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id,ct);if(d==null)return null;var targets=await _db.EndpointDeploymentTargets.AsNoTracking().Where(x=>x.DeploymentId==id).ToListAsync(ct);return new LanscopeDeploymentViewDto(d.Id,d.DeploymentCode,d.PackageVersion,d.Status,new DateTimeOffset(d.CreatedAtUtc,TimeSpan.Zero),d.ExpiresAtUtc.HasValue?new DateTimeOffset(d.ExpiresAtUtc.Value,TimeSpan.Zero):null,targets.Count,targets.Count(x=>x.EndpointDeviceId.HasValue));}
    [HttpPost("deployments/{deploymentId:int}/targets/{targetId:int}/reissue")]
    public async Task<ActionResult<ApiResponse<LanscopeDeploymentTargetResult>>> ReissueTargetToken(int deploymentId, int targetId, CancellationToken ct)
    {
        if(await RequireAsync(SecurityFunctionCodes.EndpointLanscopeDeploymentManage,ct)==null)return Forbid();
        var deployment=await _db.EndpointDeployments.FirstOrDefaultAsync(x=>x.Id==deploymentId,ct);
        var target=await _db.EndpointDeploymentTargets.FirstOrDefaultAsync(x=>x.Id==targetId&&x.DeploymentId==deploymentId,ct);
        if(deployment==null||target==null)return NotFound(ApiResponse<LanscopeDeploymentTargetResult>.Fail("Deployment target không tồn tại.",404));
        if(deployment.ExpiresAtUtc.HasValue&&deployment.ExpiresAtUtc.Value<=DateTime.UtcNow)return BadRequest(ApiResponse<LanscopeDeploymentTargetResult>.Fail("Deployment đã hết hạn."));
        if(target.Status=="Enrolled"||target.EndpointDeviceId.HasValue)return Conflict(ApiResponse<LanscopeDeploymentTargetResult>.Fail("Target đã enroll; không cấp bootstrap token mới."));
        await _db.EndpointEnrollmentTokens.Where(x=>x.TargetId==targetId&&x.UsedAtUtc==null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.UsedAtUtc,DateTime.UtcNow),ct);
        var token=GenerateSecret(32); var expires=DateTime.UtcNow.AddMinutes(GetTokenMinutes());
        _db.EndpointEnrollmentTokens.Add(new F03EndpointEnrollmentToken{DeploymentId=deploymentId,TargetId=targetId,TokenHash=Hash(token),ExpiresAtUtc=expires,CreatedAtUtc=DateTime.UtcNow});
        await _db.SaveChangesAsync(ct);
        var bootstrap=JsonSerializer.Serialize(new{deploymentId,targetId,enrollmentToken=token,apiBaseUrl=GetPublicBaseUrl(),clientId=target.ClientId});
        return Ok(ApiResponse<LanscopeDeploymentTargetResult>.Ok(new LanscopeDeploymentTargetResult(target.Id,target.TargetKey,token,$"fvn-bootstrap-{SafeFile(target.TargetKey)}.json",bootstrap)));
    }

    private string GetPublicBaseUrl()
    {
        var configured=_configuration["LanscopeDeployment:PublicBaseUrl"]?.Trim();
        if(string.IsNullOrWhiteSpace(configured)||!Uri.TryCreate(configured,UriKind.Absolute,out var uri)||uri.Scheme!=Uri.UriSchemeHttps)
            throw new InvalidOperationException("LanscopeDeployment:PublicBaseUrl phải được cấu hình bằng HTTPS.");
        return configured.TrimEnd('/');
    }
    private int GetTokenMinutes()
        => Math.Clamp(_configuration.GetValue<int?>("LanscopeDeployment:EnrollmentTokenMinutes") ?? 60, 5, 1440);

    private static string GenerateSecret(int bytes){Span<byte> b=stackalloc byte[bytes];RandomNumberGenerator.Fill(b);return Convert.ToBase64String(b).Replace("+","-",StringComparison.Ordinal).Replace("/","_",StringComparison.Ordinal).TrimEnd('=');}
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string? Trim(string? value,int max)=>string.IsNullOrWhiteSpace(value)?null:value.Trim()[..Math.Min(value.Trim().Length,max)];
    private static string SafeFile(string value)=>new(value.Select(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_').ToArray());
}
