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
 private readonly IAuthorizationService _authorization;
 private readonly IHrmAttendanceExcelExportService _excel;
 private readonly FVNWEBAPPContext _db;
 private static readonly ConcurrentDictionary<string, DateTime> SelfRecalculationCooldown = new(StringComparer.OrdinalIgnoreCase);
 private static readonly ConcurrentDictionary<string, SemaphoreSlim> SelfRecalculationGates = new(StringComparer.OrdinalIgnoreCase);
 private static readonly TimeSpan SelfRecalculationCooldownPeriod = TimeSpan.FromMinutes(5);
 public HrmAttendanceCalculationController(IHrmAttendanceCalculationService service,IHrmAttendanceExcelExportService excel,FVN_REGISTER.Application.Interfaces.Users.ICurrentUserService currentUser,FVN_REGISTER.Application.Interfaces.Users.IUserLogService userLog,IAuthorizationService authorization,FVNWEBAPPContext db,ILogger<HrmAttendanceCalculationController> logger,Microsoft.Extensions.Options.IOptionsMonitor<FVN_REGISTER.Application.Configuration.AuthDebugOptions> options):base(currentUser,userLog,logger,options){_service=service;_excel=excel;_authorization=authorization;_db=db;}
 [HttpGet("export/attendance/{batchId:guid}")]
 public async Task<IActionResult> ExportAttendance(Guid batchId, CancellationToken ct)
 {
  if(UserInfo==null || !await _authorization.HasAsync(UserInfo,SecurityFunctionCodes.AttendanceExport,ct)) return Forbid();
  var result=await _excel.ExportAttendanceAsync(batchId,ct);
  if(!result.IsSuccess||result.Data is null) return BadRequest(ApiResponse<object>.Fail(result.Message??"Xuất chấm công thất bại."));
  return File(result.Data,"application/vnd.ms-excel",$"BangChamCong_{DateTime.Now:yyyyMMdd_HHmmss}.xls");
 }

 [HttpGet("export/ot/{batchId:guid}")]
 public async Task<IActionResult> ExportOt(Guid batchId, CancellationToken ct)
 {
  if(UserInfo==null || !await _authorization.HasAsync(UserInfo,SecurityFunctionCodes.AttendanceExport,ct)) return Forbid();
  var result=await _excel.ExportOtAsync(batchId,ct);
  if(!result.IsSuccess||result.Data is null) return BadRequest(ApiResponse<object>.Fail(result.Message??"Xuất OT thất bại."));
  return File(result.Data,"application/vnd.ms-excel",$"BangLamThem_{DateTime.Now:yyyyMMdd_HHmmss}.xls");
 }

 [HttpPost("force-recalculate-self")]
 public async Task<IActionResult> ForceRecalculateSelf([FromBody] HrmAttendanceCalculationRequestDto request, CancellationToken ct)
 {
  if (UserInfo == null || string.IsNullOrWhiteSpace(UserInfo.EmployeeCode))
   return Unauthorized(ApiResponse<object>.Fail("Không xác định được mã nhân viên đăng nhập."));

  var from = request.FromDate.Date;
  var to = request.ToDate.Date;
  if (from > to)
   return BadRequest(ApiResponse<object>.Fail("Khoảng ngày không hợp lệ."));
  if ((to - from).TotalDays > 6)
   return BadRequest(ApiResponse<object>.Fail("Mỗi lần chỉ được buộc tính lại tối đa 7 ngày."));
  if (to > DateTime.Today)
   return BadRequest(ApiResponse<object>.Fail("Chỉ được tính lại đến ngày hôm nay, không tính ngày tương lai."));

  if (from < DateTime.Today.AddDays(-31))
   return BadRequest(ApiResponse<object>.Fail("Chỉ được buộc tính lại trong vòng 31 ngày gần nhất."));

  var lockedPeriod = await _db.PayrollCalculationPeriods.AsNoTracking()
   .AnyAsync(x => x.IsActive != false
       && (x.Status == "Locked" || x.Status == "Exported")
       && x.FromDate <= DateOnly.FromDateTime(to)
       && x.ToDate >= DateOnly.FromDateTime(from), ct);
  if (lockedPeriod)
   return BadRequest(ApiResponse<object>.Fail("Khoảng ngày thuộc kỳ lương đã khóa/xuất. Không thể buộc tính lại từ lịch; vui lòng liên hệ HRM/Payroll."));

  // This public-to-authenticated-user action can only recalculate the signed-in employee.
  // A per-employee gate prevents concurrent requests; a cooldown limits repeated SQL work.
  var employeeCode = UserInfo.EmployeeCode.Trim();
  var gate = SelfRecalculationGates.GetOrAdd(employeeCode, _ => new SemaphoreSlim(1, 1));
  if (!await gate.WaitAsync(0, ct))
   return StatusCode(StatusCodes.Status429TooManyRequests,
    ApiResponse<object>.Fail("Đang có một yêu cầu tính lại của bạn chạy. Vui lòng chờ hoàn tất."));

  SqlConnection? distributedLockConnection = null;
  var distributedLockAcquired = false;
  try
  {
   // A SQL session applock closes the cross-instance race: at most one self-force
   // recalculation per employee can enter the expensive procedure at a time.
   var connectionString = _db.Database.GetConnectionString();
   if (string.IsNullOrWhiteSpace(connectionString))
    return StatusCode(StatusCodes.Status503ServiceUnavailable,
     ApiResponse<object>.Fail("Không tìm thấy cấu hình kết nối SQL để tính lại chấm công."));
   var builder = new SqlConnectionStringBuilder(connectionString) { Pooling = false };
   distributedLockConnection = new SqlConnection(builder.ConnectionString);
   await distributedLockConnection.OpenAsync(ct);
   await using (var lockCommand = distributedLockConnection.CreateCommand())
   {
    lockCommand.CommandTimeout = 10;
    lockCommand.CommandText = """
       DECLARE @result int;
       EXEC @result=sp_getapplock
         @Resource=@Resource,
         @LockMode=N'Exclusive',
         @LockOwner=N'Session',
         @LockTimeout=0;
       SELECT @result;
       """;
    lockCommand.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255)
    {
     Value = $"FVN_REGISTER:ATTENDANCE:SELF-FORCE:{employeeCode.ToUpperInvariant()}"
    });
    var lockResult = Convert.ToInt32(await lockCommand.ExecuteScalarAsync(ct));
    if (lockResult < 0)
     return StatusCode(StatusCodes.Status429TooManyRequests,
      ApiResponse<object>.Fail("Đang có yêu cầu tính lại chấm công của bạn ở một phiên khác. Vui lòng chờ."));
    distributedLockAcquired = true;
   }

   var now = DateTime.UtcNow;
   // Database-backed cooldown also applies across multiple API instances.
   var recentRuns = await _db.Database.SqlQuery<int>($"""
       SELECT COUNT(1) AS [Value]
       FROM dbo.F03HrmAttendanceCalculationRun
       WHERE EmployeeCode={employeeCode}
         AND TriggeredBy=N'CALENDAR-SELF-FORCE'
         AND StartedAt >= DATEADD(MINUTE,-5,GETDATE())
       """).SingleAsync(ct);
   if (recentRuns > 0)
    return StatusCode(StatusCodes.Status429TooManyRequests,
     ApiResponse<object>.Fail("Yêu cầu buộc tính lại đã được gửi trong 5 phút gần đây. Vui lòng chờ trước khi gửi tiếp."));

   if (SelfRecalculationCooldown.TryGetValue(employeeCode, out var lastRequested)
       && now - lastRequested < SelfRecalculationCooldownPeriod)
   {
    var retrySeconds = Math.Max(1, (int)Math.Ceiling((SelfRecalculationCooldownPeriod - (now - lastRequested)).TotalSeconds));
    Response.Headers["Retry-After"] = retrySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
    return StatusCode(StatusCodes.Status429TooManyRequests,
     ApiResponse<object>.Fail($"Bạn vừa buộc tính lại gần đây. Vui lòng thử lại sau khoảng {Math.Ceiling(retrySeconds / 60d)} phút."));
   }

   SelfRecalculationCooldown[employeeCode] = now;
   var scopedRequest = new HrmAttendanceCalculationRequestDto
   {
    DeptCode = null,
    EmployeeCode = employeeCode,
    FromDate = from,
    ToDate = to,
    ReopenPayrollPeriod = false
   };
   var result = await _service.CalculateAsync(scopedRequest, "CALENDAR-SELF-FORCE", ct);
   if (!result.IsSuccess || result.Data is null)
    return BadRequest(ApiResponse<object>.Fail(result.Message ?? "Buộc tính lại chấm công thất bại."));
   return Ok(ApiResponse<HrmAttendanceCalculationResultDto>.Ok(result.Data));
  }
  finally
  {
   if (distributedLockAcquired && distributedLockConnection is not null)
   {
    try
    {
     await using var releaseCommand = distributedLockConnection.CreateCommand();
     releaseCommand.CommandTimeout = 10;
     releaseCommand.CommandText = """
       EXEC sp_releaseapplock
         @Resource=@Resource,
         @LockOwner=N'Session';
       """;
     releaseCommand.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255)
     {
      Value = $"FVN_REGISTER:ATTENDANCE:SELF-FORCE:{employeeCode.ToUpperInvariant()}"
     });
     await releaseCommand.ExecuteNonQueryAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
     Logger.LogWarning(ex, "Unable to release self-force attendance applock for {EmployeeCode}", employeeCode);
    }
   }
   if (distributedLockConnection is not null)
    await distributedLockConnection.DisposeAsync();
   gate.Release();
  }
 }

 [HttpPost("calculate")]
 public async Task<IActionResult> Calculate([FromBody] HrmAttendanceCalculationRequestDto request,CancellationToken ct)
 {
  if(UserInfo==null)return Unauthorized(ApiResponse<object>.Fail("Phiên đăng nhập hết hạn."));
  if(!await _authorization.HasAsync(UserInfo,SecurityFunctionCodes.AttendanceCalculate,ct)) return Forbid();
  if(request.FromDate.Date>request.ToDate.Date)return BadRequest(ApiResponse<object>.Fail("Khoảng ngày không hợp lệ."));
  var result=await _service.CalculateAsync(request,UserInfo.EmployeeCode,ct);
  return result.IsSuccess?Ok(ApiResponse<HrmAttendanceCalculationResultDto>.Ok(result.Data!)):BadRequest(ApiResponse<object>.Fail(result.Message??"Tính giờ thất bại."));
 }
}
