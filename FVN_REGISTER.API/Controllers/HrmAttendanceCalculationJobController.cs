using FVN_REGISTER.Application.Interfaces.Security;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Contract.Dtos.HrmSync;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Infrastructure;
using FVN_REGISTER.Infrastructure.Services.HrmSync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/hrm-attendance-calculation/jobs")]
public sealed class HrmAttendanceCalculationJobController : BaseApiController
{
    private readonly HrmAttendanceCalculationJobService _jobs;
    private readonly IAuthorizationService _authorization;
    private readonly FVNWEBAPPContext _db;

    public HrmAttendanceCalculationJobController(HrmAttendanceCalculationJobService jobs,IAuthorizationService authorization,FVN_REGISTER.Application.Interfaces.Users.ICurrentUserService currentUser,FVN_REGISTER.Application.Interfaces.Users.IUserLogService userLog,ILogger<HrmAttendanceCalculationJobController> logger,Microsoft.Extensions.Options.IOptionsMonitor<FVN_REGISTER.Application.Configuration.AuthDebugOptions> options,FVNWEBAPPContext db)
        :base(currentUser,userLog,logger,options){_jobs=jobs;_authorization=authorization;_db=db;}

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if(!await CanCalculateAsync(ct))return Forbid();
        var result=await _jobs.ListAsync(ct);
        return result.IsSuccess?Ok(ApiResponse<List<HrmAttendanceCalculationJobDto>>.Ok(result.Data??new())):BadRequest(ApiResponse<object>.Fail(result.Message??"Không tải được job."));
    }

    [HttpGet("{jobId:guid}")]
    public async Task<IActionResult> Get(Guid jobId,CancellationToken ct)
    {
        if(!await CanCalculateAsync(ct))return Forbid();
        var result=await _jobs.GetAsync(jobId,ct);
        return result.IsSuccess?Ok(ApiResponse<HrmAttendanceCalculationJobDto>.Ok(result.Data!)):NotFound(ApiResponse<object>.Fail(result.Message??"Không tìm thấy job."));
    }

    [HttpPost]
    public async Task<IActionResult> Start([FromBody] HrmAttendanceCalculationJobCommandDto request,CancellationToken ct)
    {
        if(!await CanCalculateAsync(ct))return Forbid();
        var triggeredBy=string.IsNullOrWhiteSpace(UserInfo?.EmployeeCode)?"HRM-UI":UserInfo.EmployeeCode!;
        var result=await _jobs.EnqueueAsync(request,triggeredBy,ct);
        return result.IsSuccess?Accepted($"api/hrm-attendance-calculation/jobs/{result.Data!.JobId}",ApiResponse<HrmAttendanceCalculationJobDto>.Ok(result.Data!,"Đã đưa phép tính HRM vào hàng đợi. UI không cần giữ request SQL dài.")):BadRequest(ApiResponse<object>.Fail(result.Message??"Không thể tạo job."));
    }

    [HttpPost("{jobId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid jobId,CancellationToken ct)
    {
        if(!await CanCalculateAsync(ct))return Forbid();
        var result=await _jobs.RequestCancelAsync(jobId,UserInfo?.EmployeeCode??"HRM-UI",ct);
        return result.IsSuccess?Ok(ApiResponse<object>.Ok(new{jobId,status="CancelRequested"})):BadRequest(ApiResponse<object>.Fail(result.Message??"Không thể dừng job."));
    }

    [HttpPost("{jobId:guid}/resume")]
    public async Task<IActionResult> Resume(Guid jobId,CancellationToken ct)
    {
        if(!await CanCalculateAsync(ct))return Forbid();
        var result=await _jobs.ResumeAsync(jobId,UserInfo?.EmployeeCode??"HRM-UI",ct);
        return result.IsSuccess?Ok(ApiResponse<object>.Ok(new{jobId,status="Queued"})):BadRequest(ApiResponse<object>.Fail(result.Message??"Không thể Resume job."));
    }

    [HttpPost("{jobId:guid}/force-terminate")]
    public async Task<IActionResult> ForceTerminate(Guid jobId,CancellationToken ct)
    {
        if(!await CanCalculateAsync(ct))return Forbid();
        var target=await _db.Database.SqlQuery<TerminateTarget>($"""
            SELECT TOP(1) r.CalculationBatchId,r.Status AS RunStatus,r.SqlSessionId,r.SqlSessionLoginTime,r.SqlConnectionId
            FROM dbo.F03HrmAttendanceCalculationRun r
            WHERE r.TriggeredBy LIKE CONCAT(N'HRM-JOB:',CONVERT(nvarchar(36),{jobId}),N'%')
            ORDER BY CASE WHEN r.Status=N'Running' THEN 0 ELSE 1 END,r.StartedAt DESC
            """).SingleOrDefaultAsync(ct);
        if(target is null)return NotFound(ApiResponse<object>.Fail("Job chưa có calculation run để dừng."));
        if(target.SqlSessionId is null)return BadRequest(ApiResponse<object>.Fail("Job không có SQL session đang được theo dõi."));
        var live=await _db.Database.SqlQuery<LiveSession>($"""
            SELECT TOP(1) r.session_id AS SessionId,CONVERT(datetime2(3),s.login_time) AS SessionLoginTime,c.connection_id AS ConnectionId,r.status AS SqlStatus,r.wait_type AS WaitType,r.blocking_session_id AS BlockingSessionId,r.wait_time AS WaitTimeMs
            FROM sys.dm_exec_requests r INNER JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id INNER JOIN sys.dm_exec_connections c ON c.session_id=r.session_id
            WHERE r.session_id={target.SqlSessionId.Value} AND r.database_id=DB_ID(N'FVN_REGISTER')
            """).SingleOrDefaultAsync(ct);
        if(live is null){await _jobs.RequestCancelAsync(jobId,UserInfo?.EmployeeCode??"HRM-UI",CancellationToken.None);return Conflict(ApiResponse<object>.Fail("SQL session đã không còn tồn tại. Job đã được chuyển sang yêu cầu dừng; hãy Resume để tính phần còn thiếu."));}
        if(live.SessionLoginTime!=target.SqlSessionLoginTime||live.ConnectionId!=target.SqlConnectionId)return Conflict(ApiResponse<object>.Fail("SPID đã được tái sử dụng cho connection khác. Không thực hiện KILL để tránh dừng nhầm session."));
        var ownSpid=await _db.Database.SqlQuery<int>($"SELECT @@SPID AS [Value]").SingleAsync(ct);
        if(live.SessionId==ownSpid)return BadRequest(ApiResponse<object>.Fail("Không thể kết thúc SQL session đang phục vụ request này."));
        await _db.Database.ExecuteSqlRawAsync($"KILL {live.SessionId}",CancellationToken.None);
        await _jobs.RequestCancelAsync(jobId,UserInfo?.EmployeeCode??"HRM-UI",CancellationToken.None);
        return Ok(ApiResponse<object>.Ok(new{jobId,sessionId=live.SessionId,status="Terminating"}));
    }

    private async Task<bool> CanCalculateAsync(CancellationToken ct)=>UserInfo is not null&&await _authorization.HasAsync(UserInfo,SecurityFunctionCodes.AttendanceCalculate,ct);

    private sealed class TerminateTarget{public Guid CalculationBatchId{get;set;}public string RunStatus{get;set;}=string.Empty;public int? SqlSessionId{get;set;}public DateTime? SqlSessionLoginTime{get;set;}public Guid? SqlConnectionId{get;set;}}
    private sealed class LiveSession{public int SessionId{get;set;}public DateTime? SessionLoginTime{get;set;}public Guid? ConnectionId{get;set;}public string? SqlStatus{get;set;}public string? WaitType{get;set;}public int? BlockingSessionId{get;set;}public int? WaitTimeMs{get;set;}}
}
