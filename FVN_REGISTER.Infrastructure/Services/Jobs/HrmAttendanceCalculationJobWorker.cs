using FVN_REGISTER.Application.Interfaces.HrmSync;
using FVN_REGISTER.Contract.Dtos.HrmSync;
using FVN_REGISTER.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;

namespace FVN_REGISTER.Infrastructure.Services.Jobs;

public sealed class HrmAttendanceCalculationJobWorker : BackgroundService
{
    private const string GlobalJobGuard="FVN_REGISTER:ATTENDANCE:CALCULATION-JOB";
    private readonly IServiceProvider _services;
    private readonly ILogger<HrmAttendanceCalculationJobWorker> _logger;
    private readonly BackgroundWorkerHealthRegistry _health;

    public HrmAttendanceCalculationJobWorker(IServiceProvider services,ILogger<HrmAttendanceCalculationJobWorker> logger,BackgroundWorkerHealthRegistry health)
    { _services=services;_logger=logger;_health=health; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _health.Started(nameof(HrmAttendanceCalculationJobWorker));
        _logger.LogInformation("[HRM_ATTENDANCE_JOB] Worker started.");
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var didWork=await ProcessOneAsync(stoppingToken);
                if(!didWork)await Task.Delay(TimeSpan.FromSeconds(2),stoppingToken);
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex)
            {
                _health.Failure(nameof(HrmAttendanceCalculationJobWorker),ex);
                _logger.LogError(ex,"[HRM_ATTENDANCE_JOB] Worker loop failed.");
                await Task.Delay(TimeSpan.FromSeconds(5),stoppingToken);
            }
        }
        _logger.LogInformation("[HRM_ATTENDANCE_JOB] Worker stopped.");
    }

    private async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        await using var scope=_services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<FVNWEBAPPContext>();
        var calculation=scope.ServiceProvider.GetRequiredService<IHrmAttendanceCalculationService>();
        await using var guard=await TryAcquireGuardAsync(db,ct);
        if(guard is null)return false;

        var job=await db.Database.SqlQuery<JobRow>($"""
            SELECT TOP(1) JobId,DeptCode,EmployeeCode,FromDate,ToDate,ReopenPayrollPeriod,SkipCoveredDates,TriggeredBy,Status,CancelRequestedAt
            FROM dbo.F03HrmAttendanceCalculationJob WITH(READPAST,UPDLOCK,ROWLOCK)
            WHERE Status IN(N'Queued',N'Running',N'CancelRequested') ORDER BY CreatedAt
            """).FirstOrDefaultAsync(ct);
        if(job is null)
        {
            _health.Success(nameof(HrmAttendanceCalculationJobWorker)); // Successful idle poll is a worker heartbeat.
            return false;
        }

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.F03HrmAttendanceCalculationJob
            SET Status=CASE WHEN CancelRequestedAt IS NULL THEN N'Running' ELSE N'CancelRequested' END,
                StartedAt=COALESCE(StartedAt,SYSUTCDATETIME()),LastProgressAt=COALESCE(LastProgressAt,SYSUTCDATETIME()),UpdatedAt=SYSUTCDATETIME()
            WHERE JobId={job.JobId}
            """,CancellationToken.None);

        try
        {
            var doneDates=await db.Database.SqlQuery<DateOnly>($"""
                SELECT r.FromDate AS [Value] FROM dbo.F03HrmAttendanceCalculationRun r
                WHERE r.Status=N'Succeeded' AND r.CalculationVersion=N'HRM-PORT-1.1-C3-OVERNIGHT'
                  AND r.TriggeredBy LIKE CONCAT(N'HRM-JOB:',CONVERT(nvarchar(36),{job.JobId}),N'%') AND r.FromDate=r.ToDate
                """).ToListAsync(ct);
            var done=doneDates.ToHashSet();
            var covered=new HashSet<DateOnly>();
            if(job.SkipCoveredDates && !string.IsNullOrWhiteSpace(job.EmployeeCode))
            {
                var coveredRuns=await db.Database.SqlQuery<CoverageRun>($"""
                    SELECT r.FromDate,r.ToDate
                    FROM dbo.F03HrmAttendanceCalculationRun r
                    WHERE r.Status=N'Succeeded'
                      AND r.CalculationVersion=N'HRM-PORT-1.1-C3-OVERNIGHT'
                      AND r.FromDate<={DateOnly.FromDateTime(job.ToDate)}
                      AND r.ToDate>={DateOnly.FromDateTime(job.FromDate)}
                      AND (
                           r.EmployeeCode={job.EmployeeCode}
                           OR (r.EmployeeCode IS NULL AND (r.DeptCode IS NULL OR r.DeptCode={job.DeptCode}))
                      )
                    """).ToListAsync(ct);
                foreach(var run in coveredRuns)
                {
                    var first=run.FromDate<DateOnly.FromDateTime(job.FromDate)?DateOnly.FromDateTime(job.FromDate):run.FromDate;
                    var last=run.ToDate>DateOnly.FromDateTime(job.ToDate)?DateOnly.FromDateTime(job.ToDate):run.ToDate;
                    for(var day=first;day<=last;day=day.AddDays(1)) covered.Add(day);
                }
            }
            var locked=await db.PayrollCalculationPeriods.AsNoTracking()
                .Where(x=>x.IsActive!=false&&(x.Status=="Locked"||x.Status=="Exported")&&x.FromDate<=DateOnly.FromDateTime(job.ToDate)&&x.ToDate>=DateOnly.FromDateTime(job.FromDate))
                .Select(x=>new DateRange(x.FromDate,x.ToDate)).ToListAsync(ct);
            var total=(job.ToDate-job.FromDate).Days+1;var completed=done.Count;

            for(var date=job.FromDate;date<=job.ToDate;date=date.AddDays(1))
            {
                ct.ThrowIfCancellationRequested();
                if(await IsCancelRequestedAsync(db,job.JobId,ct))
                { await MarkAsync(db,job.JobId,"Cancelled","Người dùng yêu cầu dừng tại boundary ngày.",CancellationToken.None);return true; }
                var dateOnly=DateOnly.FromDateTime(date);
                if(done.Contains(dateOnly)){await UpdateProgressAsync(db,job.JobId,dateOnly,completed,total,null,CancellationToken.None);continue;}
                if(covered.Contains(dateOnly)){completed++;await UpdateProgressAsync(db,job.JobId,dateOnly,completed,total,null,CancellationToken.None);continue;}
                if(locked.Any(x=>x.From<=dateOnly&&x.To>=dateOnly)){completed++;await UpdateProgressAsync(db,job.JobId,dateOnly,completed,total,null,CancellationToken.None);continue;}

                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE dbo.F03HrmAttendanceCalculationJob
                    SET CurrentWorkDate={dateOnly},LastProgressAt=SYSUTCDATETIME(),UpdatedAt=SYSUTCDATETIME() WHERE JobId={job.JobId}
                    """,CancellationToken.None);

                var result=await calculation.CalculateAsync(new HrmAttendanceCalculationRequestDto
                {
                    DeptCode=job.DeptCode,EmployeeCode=job.EmployeeCode,FromDate=date,ToDate=date,ReopenPayrollPeriod=job.ReopenPayrollPeriod
                },$"HRM-JOB:{job.JobId:N}:{dateOnly:yyyyMMdd}",CancellationToken.None);
                if(!result.IsSuccess)
                {
                    await MarkAsync(db,job.JobId,"Failed",result.Message,CancellationToken.None);
                    _logger.LogError("[HRM_ATTENDANCE_JOB] Job {JobId} stopped at {Date}: {Message}",job.JobId,dateOnly,result.Message);
                    return true;
                }
                completed++;
                await UpdateProgressAsync(db,job.JobId,dateOnly,completed,total,result.Data?.CalculationBatchId,CancellationToken.None);
            }
            await MarkAsync(db,job.JobId,"Completed",null,CancellationToken.None);
            _health.Success(nameof(HrmAttendanceCalculationJobWorker));
            return true;
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception ex)
        {
            await MarkAsync(db,job.JobId,"Failed",ex.Message,CancellationToken.None);
            _health.Failure(nameof(HrmAttendanceCalculationJobWorker),ex);
            _logger.LogError(ex,"[HRM_ATTENDANCE_JOB] Job {JobId} failed.",job.JobId);
            return true;
        }
    }

    private static async Task<bool> IsCancelRequestedAsync(FVNWEBAPPContext db,Guid jobId,CancellationToken ct)
        =>await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.F03HrmAttendanceCalculationJob WHERE JobId={jobId} AND Status=N'CancelRequested'").SingleAsync(ct)>0;

    private static async Task UpdateProgressAsync(FVNWEBAPPContext db,Guid jobId,DateOnly date,int completed,int total,Guid? batchId,CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.F03HrmAttendanceCalculationJob
            SET CurrentWorkDate={date},CompletedDateCount={completed},TotalDateCount={total},LastProgressAt=SYSUTCDATETIME(),LastCalculationBatchId=COALESCE({batchId},LastCalculationBatchId),UpdatedAt=SYSUTCDATETIME()
            WHERE JobId={jobId}
            """,ct);
    }

    private static async Task MarkAsync(FVNWEBAPPContext db,Guid jobId,string status,string? error,CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.F03HrmAttendanceCalculationJob
            SET Status={status},ErrorMessage={error},FinishedAt=CASE WHEN {status} IN(N'Completed',N'Cancelled') THEN SYSUTCDATETIME() ELSE FinishedAt END,UpdatedAt=SYSUTCDATETIME()
            WHERE JobId={jobId}
            """,ct);
    }

    private static async Task<JobGuard?> TryAcquireGuardAsync(FVNWEBAPPContext db,CancellationToken ct)
    {
        var connectionString=db.Database.GetConnectionString();if(string.IsNullOrWhiteSpace(connectionString))return null;
        var builder=new SqlConnectionStringBuilder(connectionString){Pooling=false};var connection=new SqlConnection(builder.ConnectionString);await connection.OpenAsync(ct);
        await using var command=connection.CreateCommand();command.CommandTimeout=10;
        command.CommandText="DECLARE @r int; EXEC @r=sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=0; SELECT @r;";
        command.Parameters.Add(new SqlParameter("@Resource",SqlDbType.NVarChar,255){Value=GlobalJobGuard});
        var result=Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        if(result<0){await connection.DisposeAsync();return null;}
        return new JobGuard(connection);
    }

    private sealed class JobGuard:IAsyncDisposable
    {
        private readonly SqlConnection _connection;public JobGuard(SqlConnection connection)=>_connection=connection;
        public async ValueTask DisposeAsync()
        {
            try{await using var command=_connection.CreateCommand();command.CommandTimeout=10;command.CommandText="EXEC sp_releaseapplock @Resource=@Resource,@LockOwner=N'Session';";command.Parameters.Add(new SqlParameter("@Resource",SqlDbType.NVarChar,255){Value=GlobalJobGuard});await command.ExecuteNonQueryAsync(CancellationToken.None);}catch{}
            await _connection.DisposeAsync();
        }
    }

    private sealed class JobRow
    {
        public Guid JobId{get;set;}public int? DeptCode{get;set;}public string? EmployeeCode{get;set;}public DateTime FromDate{get;set;}public DateTime ToDate{get;set;}
        public bool ReopenPayrollPeriod{get;set;}public bool SkipCoveredDates{get;set;}public string TriggeredBy{get;set;}=string.Empty;public string Status{get;set;}=string.Empty;public DateTime? CancelRequestedAt{get;set;}
    }
    private sealed record DateRange(DateOnly From,DateOnly To);
    private sealed record CoverageRun(DateOnly FromDate,DateOnly ToDate);
}
