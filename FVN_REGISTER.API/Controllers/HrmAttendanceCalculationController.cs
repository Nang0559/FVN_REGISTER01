using FVN_REGISTER.Application.Interfaces.HrmSync;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Contract.Dtos.HrmSync;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Data;
using Microsoft.Data.SqlClient;
using IAuthorizationService = FVN_REGISTER.Application.Interfaces.Security.IAuthorizationService;

namespace FVN_REGISTER.API.Controllers;

[Authorize]
[ApiController]
[Route("api/hrm-attendance-calculation")]
public sealed class HrmAttendanceCalculationController:BaseApiController
{
 private readonly IHrmAttendanceCalculationService _service;
 private readonly IHrmAttendanceCalculationJobService _jobs;
 private readonly IAuthorizationService _authorization;
 private readonly IHrmAttendanceExcelExportService _excel;
 private readonly FVNWEBAPPContext _db;
 private static readonly ConcurrentDictionary<string,DateTime> SelfRecalculationCooldown=new(StringComparer.OrdinalIgnoreCase);
 private static readonly ConcurrentDictionary<string,SemaphoreSlim> SelfRecalculationGates=new(StringComparer.OrdinalIgnoreCase);
 private static readonly TimeSpan SelfRecalculationCooldownPeriod=TimeSpan.FromMinutes(5);

 public HrmAttendanceCalculationController(IHrmAttendanceCalculationService service,IHrmAttendanceCalculationJobService jobs,IHrmAttendanceExcelExportService excel,FVN_REGISTER.Application.Interfaces.Users.ICurrentUserService currentUser,FVN_REGISTER.Application.Interfaces.Users.IUserLogService userLog,IAuthorizationService authorization,FVNWEBAPPContext db,ILogger<HrmAttendanceCalculationController> logger,Microsoft.Extensions.Options.IOptionsMonitor<FVN_REGISTER.Application.Configuration.AuthDebugOptions> options):base(currentUser,userLog,logger,options){_service=service;_jobs=jobs;_excel=excel;_authorization=authorization;_db=db;}

 [HttpGet("monitor")]
 public async Task<IActionResult> Monitor(CancellationToken ct){if(UserInfo==null||!await _authorization.HasAsync(UserInfo,SecurityFunctionCodes.AttendanceCalculate,ct))return Forbid();return Ok(ApiResponse<List<HrmAttendanceCalculationMonitorDto>>.Ok(await QueryMonitorAsync(null,ct)));}

 [HttpGet("monitor/{batchId:guid}")]
 public async Task<IActionResult> MonitorBatch(Guid batchId,CancellationToken ct){if(UserInfo==null||!await _authorization.HasAsync(UserInfo,SecurityFunctionCodes.AttendanceCalculate,ct))return Forbid();var row=(await QueryMonitorAsync(batchId,ct)).FirstOrDefault();return row is null?NotFound(ApiResponse<object>.Fail("Không tìm thấy batch tính công HRM.")):Ok(ApiResponse<HrmAttendanceCalculationMonitorDto>.Ok(row));}

 [HttpPost("monitor/{batchId:guid}/force-terminate")]
 public async Task<IActionResult> ForceTerminate(Guid batchId,CancellationToken ct)
 {
  if(UserInfo==null||!await _authorization.HasAsync(UserInfo,SecurityFunctionCodes.AttendanceCalculate,ct))return Forbid();
  var target=await _db.Database.SqlQuery<HrmCalculationTerminateTarget>($"""
   SELECT TOP (1) r.CalculationBatchId,r.Status AS RunStatus,r.SqlSessionId,r.SqlSessionLoginTime,r.SqlConnectionId
   FROM dbo.F03HrmAttendanceCalculationRun r WHERE r.CalculationBatchId={batchId}
   """).SingleOrDefaultAsync(ct);
  if(target is null)return NotFound(ApiResponse<object>.Fail("Không tìm thấy batch tính công HRM."));
  if(target.SqlSessionId is null)return BadRequest(ApiResponse<object>.Fail("Batch không có SQL session để kết thúc. Hãy tải lại monitor để xác định trạng thái."));
  if(!string.Equals(target.RunStatus,"Running",StringComparison.OrdinalIgnoreCase)&&!string.Equals(target.RunStatus,"Terminating",StringComparison.OrdinalIgnoreCase))return BadRequest(ApiResponse<object>.Fail($"Batch đang ở trạng thái '{target.RunStatus}', không còn là phiên đang chạy."));
  var live=await _db.Database.SqlQuery<HrmCalculationLiveSession>($"""
   SELECT TOP (1) r.session_id AS SessionId,CONVERT(datetime2(3),s.login_time) AS SessionLoginTime,c.connection_id AS ConnectionId,r.status AS SqlStatus,r.wait_type AS WaitType,r.blocking_session_id AS BlockingSessionId,r.wait_time AS WaitTimeMs
   FROM sys.dm_exec_requests r INNER JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id INNER JOIN sys.dm_exec_connections c ON c.session_id=r.session_id
   WHERE r.session_id={target.SqlSessionId.Value} AND r.database_id=DB_ID(N'FVN_REGISTER')
   """).SingleOrDefaultAsync(ct);
  if(live is null){await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.F03HrmAttendanceCalculationRun SET Status=N'Stalled',FinishedAt=COALESCE(FinishedAt,GETDATE()) WHERE CalculationBatchId={batchId} AND Status=N'Running'",CancellationToken.None);return Conflict(ApiResponse<object>.Fail("SQL session của batch không còn tồn tại. Batch đã được đánh dấu Stalled; các ngày đã commit vẫn được giữ lại."));}
  if(live.SessionLoginTime!=target.SqlSessionLoginTime||live.ConnectionId!=target.SqlConnectionId)return Conflict(ApiResponse<object>.Fail("SQL session id đã được tái sử dụng cho connection khác. Không thực hiện KILL để tránh kết thúc nhầm session."));
  if(live.SessionId<=0||live.SessionId>32767)return BadRequest(ApiResponse<object>.Fail("SQL session không hợp lệ."));
  if(live.SessionId==await GetOwnSqlSessionIdAsync(ct))return BadRequest(ApiResponse<object>.Fail("Không thể force terminate chính SQL session đang phục vụ request monitor."));
  await _db.Database.ExecuteSqlRawAsync($"KILL {live.SessionId}",CancellationToken.None);
  await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.F03HrmAttendanceCalculationRun SET Status=N'Terminating' WHERE CalculationBatchId={batchId} AND Status=N'Running'",CancellationToken.None);
  _logger.LogWarning("Force terminated HRM attendance calculation batch {BatchId}; SQL session {SessionId}, blocker {BlockingSessionId}, wait {WaitType}.",batchId,live.SessionId,live.BlockingSessionId,live.WaitType);
  return Ok(ApiResponse<object>.Ok(new{batchId,sessionId=live.SessionId,status="Terminating"}));
 }

 private Task<int> GetOwnSqlSessionIdAsync(CancellationToken ct)=>_db.Database.SqlQuery<int>($"SELECT @@SPID AS [Value]").SingleAsync(ct);

 private async Task<List<HrmAttendanceCalculationMonitorDto>> QueryMonitorAsync(Guid? batchId,CancellationToken ct)
 {
  var prefix=batchId.HasValue?$"""
   WITH Runs AS(SELECT r.CalculationBatchId,r.DeptCode,r.EmployeeCode,r.FromDate,r.ToDate,r.TriggeredBy,r.CalculationVersion,r.Status,r.StartedAt,r.FinishedAt,r.SqlSessionId,r.SqlSessionLoginTime,r.SqlConnectionId FROM dbo.F03HrmAttendanceCalculationRun r WHERE r.CalculationBatchId={batchId.Value})
  """:"""
   WITH Runs AS(SELECT TOP(50) r.CalculationBatchId,r.DeptCode,r.EmployeeCode,r.FromDate,r.ToDate,r.TriggeredBy,r.CalculationVersion,r.Status,r.StartedAt,r.FinishedAt,r.SqlSessionId,r.SqlSessionLoginTime,r.SqlConnectionId FROM dbo.F03HrmAttendanceCalculationRun r ORDER BY r.StartedAt DESC)
  """;
  var sql=prefix+"""
   SELECT r.CalculationBatchId,r.DeptCode,r.EmployeeCode,r.FromDate,r.ToDate,r.Status AS RunStatus,r.StartedAt,r.FinishedAt,r.TriggeredBy,r.CalculationVersion,
     DATEDIFF(DAY,r.FromDate,r.ToDate)+1 AS TotalDateCount,COUNT(DISTINCT a.WorkDate) AS CompletedDateCount,MAX(a.WorkDate) AS CurrentWorkDate,MAX(a.CalculatedAt) AS LastProgressAt,
     r.SqlSessionId,r.SqlSessionLoginTime,r.SqlConnectionId,live.SqlStatus,live.WaitType,live.BlockingSessionId,live.WaitTimeMs,live.CpuTimeMs,live.LogicalReads,live.Reads,live.Writes,live.CurrentStatement,
     DATEDIFF(SECOND,r.StartedAt,COALESCE(r.FinishedAt,GETDATE())) AS ElapsedSeconds,
     CONVERT(bit,CASE WHEN live.BlockingSessionId IS NOT NULL AND live.BlockingSessionId<>0 THEN 1 ELSE 0 END) AS IsBlocked,
     CONVERT(bit,CASE WHEN r.Status IN(N'Running',N'Terminating') AND live.SessionId IS NULL THEN 1 ELSE 0 END) AS IsStalled,
     CASE WHEN r.Status=N'Succeeded' THEN N'Completed' WHEN r.Status=N'Failed' THEN N'Failed' WHEN r.Status=N'Terminating' AND live.SessionId IS NULL THEN N'Terminated' WHEN r.Status=N'Running' AND live.BlockingSessionId IS NOT NULL AND live.BlockingSessionId<>0 THEN N'Blocked' WHEN r.Status=N'Running' AND live.SessionId IS NULL THEN N'Stalled' WHEN r.Status=N'Running' THEN N'Running' ELSE r.Status END AS DisplayStatus
   FROM Runs r LEFT JOIN dbo.F03HrmAttendanceCalculated a ON a.CalculationBatchId=r.CalculationBatchId
   OUTER APPLY(SELECT TOP(1) er.session_id AS SessionId,er.status AS SqlStatus,er.wait_type AS WaitType,er.blocking_session_id AS BlockingSessionId,er.wait_time AS WaitTimeMs,er.cpu_time AS CpuTimeMs,CONVERT(bigint,er.logical_reads) AS LogicalReads,CONVERT(bigint,er.reads) AS Reads,CONVERT(bigint,er.writes) AS Writes,
      SUBSTRING(st.text,(er.statement_start_offset/2)+1,((CASE er.statement_end_offset WHEN -1 THEN DATALENGTH(st.text) ELSE er.statement_end_offset END-er.statement_start_offset)/2)+1) AS CurrentStatement
     FROM sys.dm_exec_requests er OUTER APPLY sys.dm_exec_sql_text(er.sql_handle) st WHERE er.session_id=r.SqlSessionId AND er.database_id=DB_ID(N'FVN_REGISTER') ORDER BY er.start_time DESC) live
   GROUP BY r.CalculationBatchId,r.DeptCode,r.EmployeeCode,r.FromDate,r.ToDate,r.Status,r.StartedAt,r.FinishedAt,r.TriggeredBy,r.CalculationVersion,r.SqlSessionId,r.SqlSessionLoginTime,r.SqlConnectionId,live.SessionId,live.SqlStatus,live.WaitType,live.BlockingSessionId,live.WaitTimeMs,live.CpuTimeMs,live.LogicalReads,live.Reads,live.Writes,live.CurrentStatement
   ORDER BY r.StartedAt DESC
  """;
  return await _db.Database.SqlQueryRaw<HrmAttendanceCalculationMonitorDto>(sql).ToListAsync(ct);
 }

 private sealed class HrmCalculationTerminateTarget{public Guid CalculationBatchId{get;set;}public string RunStatus{get;set;}=string.Empty;public int? SqlSessionId{get;set;}public DateTime? SqlSessionLoginTime{get;set;}public Guid? SqlConnectionId{get;set;}}
 private sealed class HrmCalculationLiveSession{public int SessionId{get;set;}public DateTime? SessionLoginTime{get;set;}public Guid? ConnectionId{get;set;}public string? SqlStatus{get;set;}public string? WaitType{get;set;}public int? BlockingSessionId{get;set;}public int? WaitTimeMs{get;set;}}

 [HttpGet("export/attendance/{batchId:guid}")]
 public async Task<IActionResult> ExportAttendance(Guid batchId,CancellationToken ct){if(UserInfo==null||!await _authorization.HasAsync(UserInfo,SecurityFunctionCodes.AttendanceExport,ct))return Forbid();var result=await _excel.ExportAttendanceAsync(batchId,ct);if(!result.IsSuccess||result.Data is null)return BadRequest(ApiResponse<object>.Fail(result.Message??"Xuất chấm công thất bại."));return File(result.Data,"application/vnd.ms-excel",$"BangChamCong_{DateTime.Now:yyyyMMdd_HHmmss}.xls");}
 [HttpGet("export/attendance/job/{jobId:guid}")]
 public async Task<IActionResult> ExportAttendanceByJob(Guid jobId, CancellationToken ct)
 {
  if(UserInfo==null || !await _authorization.HasAsync(UserInfo,SecurityFunctionCodes.AttendanceExport,ct)) return Forbid();
  var result=await _excel.ExportAttendanceByJobAsync(jobId,ct);
  if(!result.IsSuccess||result.Data is null) return BadRequest(ApiResponse<object>.Fail(result.Message??"Xuất chấm công thất bại."));
  return File(result.Data,"application/vnd.ms-excel",$"BangChamCong_Job_{jobId:N}.xls");
 }

 [HttpGet("export/ot/job/{jobId:guid}")]
 public async Task<IActionResult> ExportOtByJob(Guid jobId, CancellationToken ct)
 {
  if(UserInfo==null || !await _authorization.HasAsync(UserInfo,SecurityFunctionCodes.AttendanceExport,ct)) return Forbid();
  var result=await _excel.ExportOtByJobAsync(jobId,ct);
  if(!result.IsSuccess||result.Data is null) return BadRequest(ApiResponse<object>.Fail(result.Message??"Xuất OT thất bại."));
  return File(result.Data,"application/vnd.ms-excel",$"BangLamThem_Job_{jobId:N}.xls");
 }

 [HttpGet("export/ot/{batchId:guid}")]
 public async Task<IActionResult> ExportOt(Guid batchId,CancellationToken ct){if(UserInfo==null||!await _authorization.HasAsync(UserInfo,SecurityFunctionCodes.AttendanceExport,ct))return Forbid();var result=await _excel.ExportOtAsync(batchId,ct);if(!result.IsSuccess||result.Data is null)return BadRequest(ApiResponse<object>.Fail(result.Message??"Xuất OT thất bại."));return File(result.Data,"application/vnd.ms-excel",$"BangLamThem_{DateTime.Now:yyyyMMdd_HHmmss}.xls");}

 [HttpPost("force-recalculate-self")]
 public async Task<IActionResult> ForceRecalculateSelf([FromBody] HrmAttendanceCalculationRequestDto request,CancellationToken ct)
 {
  if(UserInfo==null||string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))return Unauthorized(ApiResponse<object>.Fail("Không xác định được mã nhân viên đăng nhập."));
  var from=request.FromDate.Date;var to=request.ToDate.Date;if(from>to)return BadRequest(ApiResponse<object>.Fail("Khoảng ngày không hợp lệ."));if((to-from).TotalDays>6)return BadRequest(ApiResponse<object>.Fail("Mỗi lần chỉ được buộc tính lại tối đa 7 ngày."));if(to>DateTime.Today)return BadRequest(ApiResponse<object>.Fail("Chỉ được tính lại đến ngày hôm nay, không tính ngày tương lai."));if(from<DateTime.Today.AddDays(-31))return BadRequest(ApiResponse<object>.Fail("Chỉ được buộc tính lại trong vòng 31 ngày gần nhất."));
  var lockedPeriod=await _db.PayrollCalculationPeriods.AsNoTracking().AnyAsync(x=>x.IsActive!=false&&(x.Status=="Locked"||x.Status=="Exported")&&x.FromDate<=DateOnly.FromDateTime(to)&&x.ToDate>=DateOnly.FromDateTime(from),ct);if(lockedPeriod)return BadRequest(ApiResponse<object>.Fail("Khoảng ngày thuộc kỳ lương đã khóa/xuất. Không thể buộc tính lại từ lịch; vui lòng liên hệ HRM/Payroll."));
  var employeeCode=UserInfo.EmployeeCode.Trim();var gate=SelfRecalculationGates.GetOrAdd(employeeCode,_=>new SemaphoreSlim(1,1));if(!await gate.WaitAsync(0,ct))return StatusCode(StatusCodes.Status429TooManyRequests,ApiResponse<object>.Fail("Đang có một yêu cầu tính lại của bạn chạy. Vui lòng chờ hoàn tất."));
  SqlConnection? distributedLockConnection=null;var distributedLockAcquired=false;
  try
  {
   var connectionString=_db.Database.GetConnectionString();if(string.IsNullOrWhiteSpace(connectionString))return StatusCode(StatusCodes.Status503ServiceUnavailable,ApiResponse<object>.Fail("Không tìm thấy cấu hình kết nối SQL để tính lại chấm công."));
   var builder=new SqlConnectionStringBuilder(connectionString){Pooling=false};distributedLockConnection=new SqlConnection(builder.ConnectionString);await distributedLockConnection.OpenAsync(ct);
   await using(var lockCommand=distributedLockConnection.CreateCommand())
   {lockCommand.CommandTimeout=10;lockCommand.CommandText="DECLARE @result int; EXEC @result=sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=0; SELECT @result;";lockCommand.Parameters.Add(new SqlParameter("@Resource",SqlDbType.NVarChar,255){Value=$"FVN_REGISTER:ATTENDANCE:SELF-FORCE:{employeeCode.ToUpperInvariant()}"});var lockResult=Convert.ToInt32(await lockCommand.ExecuteScalarAsync(ct));if(lockResult<0)return StatusCode(StatusCodes.Status429TooManyRequests,ApiResponse<object>.Fail("Đang có yêu cầu tính lại chấm công của bạn ở một phiên khác. Vui lòng chờ."));distributedLockAcquired=true;}
   var now=DateTime.UtcNow;var recentRuns=await _db.Database.SqlQuery<int>($"SELECT COUNT(1) AS [Value] FROM dbo.F03HrmAttendanceCalculationJob WHERE EmployeeCode={employeeCode} AND TriggeredBy=N'CALENDAR-SELF-FORCE' AND CreatedAt>=DATEADD(MINUTE,-5,SYSUTCDATETIME())").SingleAsync(ct);if(recentRuns>0)return StatusCode(StatusCodes.Status429TooManyRequests,ApiResponse<object>.Fail("Yêu cầu buộc tính lại đã được gửi trong 5 phút gần đây. Vui lòng chờ trước khi gửi tiếp."));
   if(SelfRecalculationCooldown.TryGetValue(employeeCode,out var lastRequested)&&now-lastRequested<SelfRecalculationCooldownPeriod){var retrySeconds=Math.Max(1,(int)Math.Ceiling((SelfRecalculationCooldownPeriod-(now-lastRequested)).TotalSeconds));Response.Headers["Retry-After"]=retrySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);return StatusCode(StatusCodes.Status429TooManyRequests,ApiResponse<object>.Fail($"Bạn vừa buộc tính lại gần đây. Vui lòng thử lại sau khoảng {Math.Ceiling(retrySeconds/60d)} phút."));}
   var result=await _jobs.EnqueueAsync(new HrmAttendanceCalculationJobCommandDto{DeptCode=null,EmployeeCode=employeeCode,FromDate=from,ToDate=to,ReopenPayrollPeriod=false},"CALENDAR-SELF-FORCE",ct,skipCoveredDates:false,priority:0);
   if(!result.IsSuccess||result.Data is null)return BadRequest(ApiResponse<object>.Fail(result.Message??"Không thể đưa yêu cầu tính lại vào hàng đợi."));
   SelfRecalculationCooldown[employeeCode]=now;
   return Accepted($"api/hrm-attendance-calculation/jobs/{result.Data.JobId}",ApiResponse<HrmAttendanceCalculationJobDto>.Ok(result.Data,"Đã đưa yêu cầu buộc tính lại vào hàng đợi; có thể rời trang mà job vẫn tiếp tục."));
  }
  finally
  {if(distributedLockAcquired&&distributedLockConnection is not null){try{await using var releaseCommand=distributedLockConnection.CreateCommand();releaseCommand.CommandTimeout=10;releaseCommand.CommandText="EXEC sp_releaseapplock @Resource=@Resource,@LockOwner=N'Session';";releaseCommand.Parameters.Add(new SqlParameter("@Resource",SqlDbType.NVarChar,255){Value=$"FVN_REGISTER:ATTENDANCE:SELF-FORCE:{employeeCode.ToUpperInvariant()}"});await releaseCommand.ExecuteNonQueryAsync(CancellationToken.None);}catch(Exception ex){_logger.LogWarning(ex,"Unable to release self-force attendance applock for {EmployeeCode}",employeeCode);}}if(distributedLockConnection is not null)await distributedLockConnection.DisposeAsync();gate.Release();}
 }

 [HttpPost("calculate")]
 public async Task<IActionResult> Calculate([FromBody] HrmAttendanceCalculationRequestDto request,CancellationToken ct)
 {
  if(UserInfo==null)return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập hết hạn."));
  if(!await _authorization.HasAsync(UserInfo,SecurityFunctionCodes.AttendanceCalculate,ct))return Forbid();
  if(request.FromDate.Date>request.ToDate.Date)return BadRequest(ApiResponse<object>.Fail("Khoảng ngày không hợp lệ."));
  var triggeredBy=string.IsNullOrWhiteSpace(UserInfo.EmployeeCode)?"HRM-UI":UserInfo.EmployeeCode!;
  var command=new HrmAttendanceCalculationJobCommandDto
  {
   DeptCode=request.DeptCode,
   EmployeeCode=request.EmployeeCode,
   FromDate=request.FromDate.Date,
   ToDate=request.ToDate.Date,
   ReopenPayrollPeriod=request.ReopenPayrollPeriod
  };
  var result=await _jobs.EnqueueAsync(command,triggeredBy,ct,skipCoveredDates:false);
  return result.IsSuccess&&result.Data is not null
   ?Accepted($"api/hrm-attendance-calculation/jobs/{result.Data.JobId}",ApiResponse<HrmAttendanceCalculationJobDto>.Ok(result.Data,"Đã đưa phép tính vào hàng đợi. Request HTTP không giữ vòng đời SQL."))
   :BadRequest(ApiResponse<object>.Fail(result.Message??"Không thể đưa phép tính vào hàng đợi."));
}
}
