using FVN_REGISTER.Application.Interfaces.HrmSync;
using FVN_REGISTER.Contract.Dtos.HrmSync;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace FVN_REGISTER.Infrastructure.Services.HrmSync;

public class HrmAttendanceCalculationJobService : IHrmAttendanceCalculationJobService
{
    private readonly FVNWEBAPPContext _db;
    public HrmAttendanceCalculationJobService(FVNWEBAPPContext db) => _db=db;

    public async Task<ServiceResult<HrmAttendanceCalculationJobDto>> EnqueueAsync(HrmAttendanceCalculationJobCommandDto request,string triggeredBy,CancellationToken ct=default,bool skipCoveredDates=false,byte priority=1,bool forceNewCalculation=false)
    {
        var from=request.FromDate.Date;var to=request.ToDate.Date;
        if(from>to)return ServiceResult<HrmAttendanceCalculationJobDto>.Fail("Khoảng ngày không hợp lệ.");
        if(to>DateTime.Today)return ServiceResult<HrmAttendanceCalculationJobDto>.Fail("Không được tính ngày tương lai.");
        if((to-from).TotalDays>92)return ServiceResult<HrmAttendanceCalculationJobDto>.Fail("Mỗi job tính công tối đa 93 ngày. Hãy chia nhỏ khoảng ngày.");
        var employee=string.IsNullOrWhiteSpace(request.EmployeeCode)?null:request.EmployeeCode.Trim();
        if(employee is not null&&!await _db.Employees.AsNoTracking().AnyAsync(x=>x.IsActive!=false&&x.EmployeeCode!=null&&x.EmployeeCode.Trim()==employee,ct))return ServiceResult<HrmAttendanceCalculationJobDto>.Fail($"Không tìm thấy nhân viên '{employee}'.");
        var active=await _db.Database.SqlQuery<Guid>($"""
            SELECT TOP(1) JobId AS [Value] FROM dbo.F03HrmAttendanceCalculationJob
            WHERE Status IN(N'Queued',N'Running',N'CancelRequested')
              AND ((DeptCode={request.DeptCode}) OR (DeptCode IS NULL AND {request.DeptCode} IS NULL))
              AND ((EmployeeCode={employee}) OR (EmployeeCode IS NULL AND {employee} IS NULL))
              AND FromDate<={to} AND ToDate>={from} ORDER BY CreatedAt
            """).FirstOrDefaultAsync(ct);
        if(active!=Guid.Empty && !forceNewCalculation)
        {
            var existing=await GetAsync(active,ct);
            // Calendar backfill is idempotent: join an overlapping durable job, wait for it,
            // then re-check coverage. Manual jobs still report the duplicate instead of
            // pretending that an overlapping range satisfies the entire requested range.
            if(existing.IsSuccess && existing.Data is not null)
            {
                var activeFrom=existing.Data.FromDate.Date;
                var activeTo=existing.Data.ToDate.Date;
                if(skipCoveredDates || (activeFrom<=from && activeTo>=to))
                    return existing;
            }
            return ServiceResult<HrmAttendanceCalculationJobDto>.Fail($"Đã có job {active:N} đang xử lý hoặc chờ chạy cho cùng phạm vi.");
        }
        var jobId=Guid.NewGuid();var total=(to-from).Days+1;
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.F03HrmAttendanceCalculationJob(JobId,DeptCode,EmployeeCode,FromDate,ToDate,ReopenPayrollPeriod,SkipCoveredDates,Priority,TriggeredBy,Status,TotalDateCount)
            VALUES({jobId},{request.DeptCode},{employee},{from},{to},{request.ReopenPayrollPeriod},{skipCoveredDates},{priority},{triggeredBy},N'Queued',{total})
            """,CancellationToken.None);
        return await GetAsync(jobId,ct);
    }

    public async Task<ServiceResult<HrmAttendanceCalculationJobDto>> GetAsync(Guid jobId,CancellationToken ct=default)
    {
        var row=(await QueryAsync(jobId,ct)).FirstOrDefault();
        return row is null?ServiceResult<HrmAttendanceCalculationJobDto>.Fail("Không tìm thấy job tính công HRM."):ServiceResult<HrmAttendanceCalculationJobDto>.Ok(row);
    }
    public async Task<ServiceResult<List<HrmAttendanceCalculationJobDto>>> ListAsync(CancellationToken ct=default)=>ServiceResult<List<HrmAttendanceCalculationJobDto>>.Ok(await QueryAsync(null,ct));

    public async Task<ServiceResult> RequestCancelAsync(Guid jobId,string requestedBy,CancellationToken ct=default)
    {
        var changed=await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.F03HrmAttendanceCalculationJob
            SET Status=CASE WHEN Status=N'Queued' THEN N'Cancelled' ELSE N'CancelRequested' END,
                CancelRequestedAt=SYSUTCDATETIME(),CancelRequestedBy={requestedBy},
                FinishedAt=CASE WHEN Status=N'Queued' THEN SYSUTCDATETIME() ELSE FinishedAt END,UpdatedAt=SYSUTCDATETIME()
            WHERE JobId={jobId} AND Status IN(N'Queued',N'Running',N'CancelRequested')
            """,CancellationToken.None);
        return changed==0?ServiceResult.Fail("Job không còn ở trạng thái có thể dừng."):ServiceResult.Ok();
    }

    public async Task<ServiceResult> ResumeAsync(Guid jobId,string requestedBy,CancellationToken ct=default)
    {
        var live=await _db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests r
            WHERE r.database_id=DB_ID(N'FVN_REGISTER') AND r.session_id<>@@SPID
              AND EXISTS(SELECT 1 FROM dbo.F03HrmAttendanceCalculationRun run WHERE run.Status=N'Running' AND run.SqlSessionId=r.session_id
                AND run.TriggeredBy LIKE CONCAT(N'HRM-JOB:',REPLACE(CONVERT(nvarchar(36),{jobId}),N'-',N''),N'%'))
            """).SingleAsync(ct);
        if(live>0)return ServiceResult.Fail("Job vẫn còn SQL session đang chạy. Không thể Resume song song.");
        var changed=await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.F03HrmAttendanceCalculationJob
            SET Status=N'Queued',CancelRequestedAt=NULL,CancelRequestedBy=NULL,ErrorMessage=NULL,FinishedAt=NULL,UpdatedAt=SYSUTCDATETIME()
            WHERE JobId={jobId} AND Status IN(N'Cancelled',N'Failed',N'Stalled',N'Terminated',N'Running',N'CancelRequested')
            """,CancellationToken.None);
        return changed==0?ServiceResult.Fail("Job không ở trạng thái có thể Resume."):ServiceResult.Ok();
    }

    private async Task<List<HrmAttendanceCalculationJobDto>> QueryAsync(Guid? jobId,CancellationToken ct)
    {
        var filter=jobId.HasValue?$"WHERE j.JobId={jobId.Value}":"";var top=jobId.HasValue?"":"TOP(50)";
        var sql=$"""
            SELECT {top} j.JobId,j.DeptCode,j.EmployeeCode,j.FromDate,j.ToDate,j.SkipCoveredDates,j.Priority,j.Status,j.TriggeredBy,j.CurrentWorkDate,j.CompletedDateCount,j.TotalDateCount,j.CreatedAt,j.StartedAt,j.LastProgressAt,j.FinishedAt,j.CancelRequestedAt,j.LastCalculationBatchId,j.ErrorMessage,
                live.SqlSessionId,live.SqlStatus,live.WaitType,live.BlockingSessionId,live.WaitTimeMs,
                CONVERT(bit,CASE WHEN live.BlockingSessionId IS NOT NULL AND live.BlockingSessionId<>0 THEN 1 ELSE 0 END) AS IsBlocked,
                CONVERT(bit,CASE WHEN j.Status IN(N'Running',N'CancelRequested') AND j.LastProgressAt IS NOT NULL AND DATEDIFF(MINUTE,j.LastProgressAt,SYSUTCDATETIME())>=10 AND live.SqlSessionId IS NULL THEN 1 ELSE 0 END) AS IsStalled,
                CASE WHEN j.Status=N'Completed' THEN N'Completed' WHEN j.Status=N'Cancelled' THEN N'Cancelled' WHEN j.Status=N'CancelRequested' THEN N'Cancel requested' WHEN live.BlockingSessionId IS NOT NULL AND live.BlockingSessionId<>0 THEN N'Blocked' WHEN j.Status=N'Running' AND live.SqlSessionId IS NULL AND j.LastProgressAt IS NOT NULL AND DATEDIFF(MINUTE,j.LastProgressAt,SYSUTCDATETIME())>=10 THEN N'Stalled' ELSE j.Status END AS DisplayStatus
            FROM dbo.F03HrmAttendanceCalculationJob j
            OUTER APPLY(SELECT TOP(1) r.SqlSessionId,r.Status AS SqlStatus,er.wait_type AS WaitType,er.blocking_session_id AS BlockingSessionId,er.wait_time AS WaitTimeMs FROM dbo.F03HrmAttendanceCalculationRun r LEFT JOIN sys.dm_exec_requests er ON er.session_id=r.SqlSessionId AND er.database_id=DB_ID(N'FVN_REGISTER') WHERE r.TriggeredBy LIKE CONCAT(N'HRM-JOB:',REPLACE(CONVERT(nvarchar(36),j.JobId),N'-',N''),N'%') ORDER BY r.StartedAt DESC) live
            {filter}
            """;
        if(!jobId.HasValue)sql+=" ORDER BY CASE WHEN j.Status IN(N'Queued',N'Running',N'CancelRequested') THEN 0 ELSE 1 END, CASE WHEN j.TriggeredBy LIKE N'HRM-ATTENDANCE-WORKER%' THEN 1 ELSE 0 END, j.CreatedAt DESC";
        return await _db.Database.SqlQueryRaw<HrmAttendanceCalculationJobDto>(sql).ToListAsync(ct);
    }
}
