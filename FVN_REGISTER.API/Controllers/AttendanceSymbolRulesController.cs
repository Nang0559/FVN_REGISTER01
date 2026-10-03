using FVN_REGISTER.Application.Interfaces.FeatureOperators;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Infrastructure.Services.Calendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

[ApiController, Route("api/attendance-symbol-rules"), Authorize]
public sealed class AttendanceSymbolRulesController : ControllerBase
{
    private readonly FVNWEBAPPContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IFeatureOperatorAssignmentService _operators;
    public AttendanceSymbolRulesController(FVNWEBAPPContext db, ICurrentUserService currentUser, IAuthorizationService authorization, IFeatureOperatorAssignmentService operators){_db=db;_currentUser=currentUser;_authorization=authorization;_operators=operators;}

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttendanceSymbolRuleDto>>> GetAll(CancellationToken ct){if(!await CanManage(ct))return Forbid();var r=await new AttendanceSymbolRuleService(_db).GetAllAsync(ct);return r.IsSuccess?Ok(r.Data):BadRequest(r.Message);}
    [HttpPost]
    public async Task<ActionResult<AttendanceSymbolRuleDto>> Create([FromBody] AttendanceSymbolRuleUpsertRequest request,CancellationToken ct){if(!await CanManage(ct))return Forbid();var u=_currentUser.GetCurrentUser();if(u is null)return Unauthorized();var r=await new AttendanceSymbolRuleService(_db).SaveAsync(null,request,u.UserId,ct);return r.IsSuccess?Ok(r.Data):BadRequest(r.Message);}
    [HttpPut("{id:int}")]
    public async Task<ActionResult<AttendanceSymbolRuleDto>> Update(int id,[FromBody] AttendanceSymbolRuleUpsertRequest request,CancellationToken ct){if(!await CanManage(ct))return Forbid();var u=_currentUser.GetCurrentUser();if(u is null)return Unauthorized();var r=await new AttendanceSymbolRuleService(_db).SaveAsync(id,request,u.UserId,ct);return r.IsSuccess?Ok(r.Data):BadRequest(r.Message);}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id,CancellationToken ct){if(!await CanManage(ct))return Forbid();var u=_currentUser.GetCurrentUser();if(u is null)return Unauthorized();var r=await new AttendanceSymbolRuleService(_db).DeactivateAsync(id,u.UserId,ct);return r.IsSuccess?Ok(r.Data):BadRequest(r.Message);}
    [HttpPost("test")]
    public async Task<ActionResult<AttendanceSymbolRuleTestResultDto>> Test([FromBody] AttendanceSymbolRuleTestRequest request,CancellationToken ct){if(!await CanManage(ct))return Forbid();return Ok(await new AttendanceSymbolRuleService(_db).TestAsync(request,ct));}

    private async Task<bool> CanManage(CancellationToken ct){var u=_currentUser.GetCurrentUser();if(u is null||!await _authorization.HasAsync(u,SecurityFunctionCodes.WorkCalendarSymbolRuleManage,ct))return false;return await _operators.CanOperateAsync(u.UserId,u.EmployeeCode,SecurityFunctionCodes.WorkCalendarSymbolRuleManage,FeatureOperatorCatalog.AttendanceSymbolRule,null,ct);}
}
