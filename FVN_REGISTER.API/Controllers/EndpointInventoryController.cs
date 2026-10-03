using System.Data;
using System.Security.Cryptography;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Attributes;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;
namespace FVN_REGISTER.API.Controllers;
[ApiController]
[Route("api/security/endpoints")]
[Authorize]
public sealed class EndpointInventoryController : ControllerBase
{
    private readonly FVNWEBAPPContext _db;
    private readonly IEndpointComplianceService _compliance;
    private readonly IEndpointInventoryService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly AppAuthorizationService _authorization;
    public EndpointInventoryController(FVNWEBAPPContext db,IEndpointComplianceService compliance,IEndpointInventoryService service,ICurrentUserService currentUser,AppAuthorizationService authorization){_db=db;_compliance=compliance;_service=service;_currentUser=currentUser;_authorization=authorization;}
    [HttpGet]
    [SecurityFunctionDefinition("Endpoint.InventoryView", "Endpoint Inventory - xem danh sách")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EndpointInventorySummaryDto>>>> GetAll(CancellationToken ct){var user=_currentUser.GetCurrentUser();if(user==null||!await _authorization.HasAsync(user,SecurityFunctionCodes.EndpointInventoryView,ct))return Forbid();return Ok(ApiResponse<IReadOnlyList<EndpointInventorySummaryDto>>.Ok(await _service.GetAllAsync(ct)));}
    [HttpGet("{deviceKey}")]
    [SecurityFunctionDefinition("Endpoint.InventoryView", "Endpoint Inventory - xem thiết bị")]
    public async Task<ActionResult<ApiResponse<EndpointInventorySummaryDto>>> Get(string deviceKey,CancellationToken ct){var user=_currentUser.GetCurrentUser();if(user==null||!await _authorization.HasAsync(user,SecurityFunctionCodes.EndpointInventoryView,ct))return Forbid();var data=await _service.GetAsync(deviceKey,ct);return data==null?NotFound(ApiResponse<EndpointInventorySummaryDto>.Fail("Không tìm thấy máy.",404)):Ok(ApiResponse<EndpointInventorySummaryDto>.Ok(data));}
    [HttpGet("{endpointDeviceId:long}/equipment-suggestions")]
    [SecurityFunctionDefinition("Equipment.Assign", "Endpoint - gợi ý Equipment theo serial")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<object>>>> EquipmentSuggestions(long endpointDeviceId,CancellationToken ct){var user=_currentUser.GetCurrentUser();if(user==null||!await _authorization.HasAsync(user,SecurityFunctionCodes.EquipmentAssign,ct))return Forbid();var serial=await _db.EndpointDevices.AsNoTracking().Where(x=>x.Id==endpointDeviceId).Select(x=>x.SerialNumber).FirstOrDefaultAsync(ct);if(string.IsNullOrWhiteSpace(serial))return Ok(ApiResponse<IReadOnlyList<object>>.Ok(Array.Empty<object>()));var normalized=serial.Trim();var matches=await _db.EquipmentAssets.AsNoTracking().Where(x=>x.SerialNumber!=null&&x.SerialNumber==normalized).OrderBy(x=>x.EquipmentCode).Select(x=>(object)new{x.Id,x.EquipmentCode,x.EquipmentName,x.SerialNumber}).ToListAsync(ct);return Ok(ApiResponse<IReadOnlyList<object>>.Ok(matches));}
    [HttpPost("{endpointDeviceId:long}/evaluate")]
    [SecurityFunctionDefinition("Endpoint.ComplianceView", "Endpoint Compliance - đánh giá")]
    public async Task<ActionResult<ApiResponse<int>>> Evaluate(long endpointDeviceId,CancellationToken ct){var user=_currentUser.GetCurrentUser();if(user==null||!await _authorization.HasAsync(user,SecurityFunctionCodes.EndpointComplianceView,ct))return Forbid();return Ok(ApiResponse<int>.Ok(await _compliance.EvaluateAsync(endpointDeviceId,ct)));}
    [HttpPut("{endpointDeviceId:long}/equipment")]
    [SecurityFunctionDefinition("Equipment.Assign", "Endpoint - liên kết Equipment")]
    public async Task<ActionResult<ApiResponse<bool>>> LinkEquipment(long endpointDeviceId,EndpointEquipmentLinkRequest request,CancellationToken ct){var user=_currentUser.GetCurrentUser();if(user==null||!await _authorization.HasAsync(user,SecurityFunctionCodes.EquipmentAssign,ct))return Forbid();if(request.EquipmentAssetId<=0)return BadRequest(ApiResponse<bool>.Fail("EquipmentAssetId không hợp lệ."));var device=await _db.EndpointDevices.FirstOrDefaultAsync(x=>x.Id==endpointDeviceId,ct);if(device==null)return NotFound(ApiResponse<bool>.Fail("Không tìm thấy Endpoint device.",404));var equipment=await _db.EquipmentAssets.AsNoTracking().Where(x=>x.Id==request.EquipmentAssetId).Select(x=>new{x.Id,x.EquipmentCode,x.EquipmentName,x.SerialNumber}).FirstOrDefaultAsync(ct);if(equipment==null)return BadRequest(ApiResponse<bool>.Fail("Equipment Asset không tồn tại."));device.EquipmentAssetId=equipment.Id;device.UpdatedAt=DateTime.UtcNow;await _db.SaveChangesAsync(ct);return Ok(ApiResponse<bool>.Ok(true));}
    [AllowAnonymous]
    [HttpPost("inventory")]
    public async Task<ActionResult<ApiResponse<EndpointInventorySummaryDto>>> Ingest([FromBody]EndpointInventoryRequestDto request,CancellationToken ct){var apiKey=Request.Headers["X-FVN-Device-Api-Key"].FirstOrDefault();if(string.IsNullOrWhiteSpace(apiKey))return Unauthorized(ApiResponse<EndpointInventorySummaryDto>.Fail("Thiếu thông tin xác thực thiết bị.",401));var deviceKey=await ResolveDeviceKeyAsync(apiKey,ct);if(string.IsNullOrWhiteSpace(deviceKey))return Unauthorized(ApiResponse<EndpointInventorySummaryDto>.Fail("Thông tin xác thực thiết bị không hợp lệ hoặc đã hết hạn.",401));var trustedRequest=request with{DeviceKey=deviceKey,EmployeeCode=null,EquipmentAssetId=null};try{var result=await _service.UpsertInventoryAsync(trustedRequest,ct);await _db.Database.ExecuteSqlRawAsync(@"UPDATE dbo.F03EndpointDevices SET LanscopeClientId=COALESCE(NULLIF({1},N''),LanscopeClientId),IpAddress=COALESCE(NULLIF({2},N''),IpAddress),MacAddress=COALESCE(NULLIF({3},N''),MacAddress),WindowsUser=COALESCE(NULLIF({4},N''),WindowsUser),DomainName=COALESCE(NULLIF({5},N''),DomainName),OrganizationalUnit=COALESCE(NULLIF({6},N''),OrganizationalUnit),LanscopeGroup=COALESCE(NULLIF({7},N''),LanscopeGroup),Manufacturer=COALESCE(NULLIF({8},N''),Manufacturer),Model=COALESCE(NULLIF({9},N''),Model),UpdatedAt=SYSUTCDATETIME() WHERE DeviceKey={0};",new object[]{deviceKey,request.LanscopeClientId,request.IpAddress,request.MacAddress,request.WindowsUser,request.DomainName,request.OrganizationalUnit,request.LanscopeGroup,request.Manufacturer,request.Model},ct);var credentialStatus=await _credentialStatusAsync(deviceKey,ct);if(credentialStatus is DateTime expiresAtUtc)Response.Headers["X-FVN-ApiKey-Expires-Utc"]=new DateTimeOffset(expiresAtUtc,TimeSpan.Zero).ToString("O");return Ok(ApiResponse<EndpointInventorySummaryDto>.Ok(result));}catch(ArgumentException ex){return BadRequest(ApiResponse<EndpointInventorySummaryDto>.Fail(ex.Message));}}
    private async Task<string?> ResolveDeviceKeyAsync(string apiKey,CancellationToken cancellationToken){byte[] hash=SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(apiKey));var connection=_db.Database.GetDbConnection();await using var command=connection.CreateCommand();command.CommandText="SELECT TOP (1) d.DeviceKey FROM dbo.F03EndpointCredentials c INNER JOIN dbo.F03EndpointDevices d ON d.Id=c.EndpointDeviceId WHERE c.SecretHash=@hash AND c.RevokedAtUtc IS NULL AND c.ExpiresAtUtc>SYSUTCDATETIME();";var parameter=command.CreateParameter();parameter.ParameterName="@hash";parameter.DbType=DbType.Binary;parameter.Value=hash;command.Parameters.Add(parameter);if(connection.State!=ConnectionState.Open)await connection.OpenAsync(cancellationToken);var value=await command.ExecuteScalarAsync(cancellationToken);return value==null||value==DBNull.Value?null:Convert.ToString(value);}
    private async Task<DateTime?> _credentialStatusAsync(string deviceKey,CancellationToken ct){var connection=_db.Database.GetDbConnection();await using var command=connection.CreateCommand();command.CommandText="SELECT TOP (1) ExpiresAtUtc FROM dbo.F03EndpointCredentials c INNER JOIN dbo.F03EndpointDevices d ON d.Id=c.EndpointDeviceId WHERE d.DeviceKey=@deviceKey AND c.RevokedAtUtc IS NULL ORDER BY c.CreatedAtUtc DESC;";var parameter=command.CreateParameter();parameter.ParameterName="@deviceKey";parameter.Value=deviceKey;command.Parameters.Add(parameter);if(connection.State!=ConnectionState.Open)await connection.OpenAsync(ct);var value=await command.ExecuteScalarAsync(ct);return value==null||value==DBNull.Value?null:(DateTime?)Convert.ToDateTime(value);}
}
