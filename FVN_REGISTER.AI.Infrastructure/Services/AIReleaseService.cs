using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FVN_REGISTER.AI.Infrastructure.Services;

public sealed class AIReleaseService(AiPlatformDbContext db, IGoldenDatasetService golden) : IAIReleaseService
{
    private const int MinimumGoldenCases = 1;
    private const int MinimumHumanDecisions = 10;

    public async Task<AIRelease> CreateCandidateAsync(string tenantCode, string actorId, string releaseCode, string modelProvider, string modelVersion, string promptVersion, string? schemaCode, int? schemaVersion, string goldenDatasetCode, double minimumAcceptanceRate, CancellationToken cancellationToken = default)
    {
        Require(tenantCode,64,nameof(tenantCode)); Require(actorId,128,nameof(actorId)); Require(releaseCode,128,nameof(releaseCode)); Require(modelProvider,64,nameof(modelProvider)); Require(modelVersion,128,nameof(modelVersion)); Require(promptVersion,128,nameof(promptVersion)); Require(goldenDatasetCode,128,nameof(goldenDatasetCode));
        if (minimumAcceptanceRate is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(minimumAcceptanceRate));
        if (schemaCode is not null) Require(schemaCode,128,nameof(schemaCode));
        if (schemaVersion is < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));

        var tenant=tenantCode.Trim(); var exists=await db.Set<AiModelReleaseRecord>().AnyAsync(x=>x.TenantCode==tenant&&x.ReleaseCode==releaseCode.Trim(),cancellationToken);
        if(exists) throw new InvalidOperationException("RELEASE_CODE_ALREADY_EXISTS");
        var record=new AiModelReleaseRecord{Id=Guid.NewGuid(),TenantCode=tenant,ReleaseCode=releaseCode.Trim(),ModelProvider=modelProvider.Trim(),ModelVersion=modelVersion.Trim(),PromptVersion=promptVersion.Trim(),SchemaCode=schemaCode?.Trim(),SchemaVersion=schemaVersion,GoldenDatasetCode=goldenDatasetCode.Trim(),Status="Candidate",MinimumAcceptanceRate=minimumAcceptanceRate,CreatedAtUtc=DateTimeOffset.UtcNow,CreatedBy=actorId.Trim()};
        db.Set<AiModelReleaseRecord>().Add(record); await db.SaveChangesAsync(cancellationToken); return ToContract(record);
    }

    public async Task<AIReleaseGateResult> EvaluateGateAsync(string tenantCode, Guid releaseId, CancellationToken cancellationToken = default)
    {
        var tenant=tenantCode.Trim(); var release=await db.Set<AiModelReleaseRecord>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==releaseId&&x.TenantCode==tenant,cancellationToken)??throw new KeyNotFoundException("AI release was not found.");
        var feedback=await db.Feedback.AsNoTracking().Where(x=>x.TenantCode==tenant&&x.ReleaseId==release.Id).Select(x=>x.FeedbackType).ToListAsync(cancellationToken);
        var accepted=feedback.Count(x=>x=="Accept"); var rejected=feedback.Count(x=>x=="Reject"); var corrected=feedback.Count(x=>x=="Correct"); var decisions=accepted+rejected+corrected; var acceptance=decisions==0?0:(double)accepted/decisions;
        var dataset=await golden.GetSummaryAsync(tenant,release.GoldenDatasetCode,release.Id,cancellationToken); var failures=new List<string>();
        if(decisions<MinimumHumanDecisions) failures.Add($"HUMAN_DECISION_COUNT_LT_{MinimumHumanDecisions}");
        if(acceptance<release.MinimumAcceptanceRate) failures.Add("ACCEPTANCE_RATE_BELOW_GATE");
        if(dataset.ActiveCases<MinimumGoldenCases) failures.Add("GOLDEN_DATASET_NOT_READY"); else if(dataset.CompletedCases<dataset.ActiveCases) failures.Add("GOLDEN_DATASET_INCOMPLETE"); else if(dataset.PassRate<1) failures.Add("GOLDEN_DATASET_HAS_FAILURES");
        return new(failures.Count==0,acceptance,decisions,dataset.CompletedCases,dataset.PassedCases,failures.ToArray());
    }

    public async Task PromoteAsync(string tenantCode,string actorId,Guid releaseId,CancellationToken cancellationToken=default)
    {
        Require(tenantCode,64,nameof(tenantCode)); Require(actorId,128,nameof(actorId));
        await using var transaction = await BeginProductionTransactionAsync(cancellationToken);
        var tenant=tenantCode.Trim();
        var release=await db.Set<AiModelReleaseRecord>().SingleOrDefaultAsync(x=>x.Id==releaseId&&x.TenantCode==tenant,cancellationToken)??throw new KeyNotFoundException("AI release was not found.");
        if(release.Status!="Candidate") throw new InvalidOperationException("RELEASE_NOT_CANDIDATE");
        var gate=await EvaluateGateAsync(tenant,releaseId,cancellationToken);
        if(!gate.Passed) throw new InvalidOperationException("RELEASE_GATE_FAILED:"+string.Join(',',gate.Failures));
        var active=await db.Set<AiModelReleaseRecord>().Where(x=>x.TenantCode==tenant&&x.Status=="Active").ToListAsync(cancellationToken);
        foreach(var old in active){old.Status="Retired";old.RetiredAtUtc=DateTimeOffset.UtcNow;}
        release.Status="Active";release.PromotedAtUtc=DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        if(transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task RollbackAsync(string tenantCode,string actorId,Guid releaseId,CancellationToken cancellationToken=default)
    {
        Require(tenantCode,64,nameof(tenantCode)); Require(actorId,128,nameof(actorId));
        await using var transaction = await BeginProductionTransactionAsync(cancellationToken);
        var tenant=tenantCode.Trim();
        var active=await db.Set<AiModelReleaseRecord>().SingleOrDefaultAsync(x=>x.Id==releaseId&&x.TenantCode==tenant,cancellationToken)??throw new KeyNotFoundException("AI release was not found.");
        if(active.Status!="Active") throw new InvalidOperationException("RELEASE_NOT_ACTIVE");
        var previous=await db.Set<AiModelReleaseRecord>().Where(x=>x.TenantCode==tenant&&x.Status=="Retired"&&x.PromotedAtUtc!=null&&x.Id!=releaseId).OrderByDescending(x=>x.PromotedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if(previous is null) throw new InvalidOperationException("NO_PREVIOUS_RELEASE_TO_ROLLBACK");
        active.Status="RolledBack";active.RetiredAtUtc=DateTimeOffset.UtcNow;
        previous.Status="Active";previous.RetiredAtUtc=null;
        await db.SaveChangesAsync(cancellationToken);
        if(transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AIRelease?> GetActiveAsync(string tenantCode,CancellationToken cancellationToken=default)
    {
        var record=await db.Set<AiModelReleaseRecord>().AsNoTracking().SingleOrDefaultAsync(x=>x.TenantCode==tenantCode.Trim()&&x.Status=="Active",cancellationToken);
        return record is null?null:ToContract(record);
    }

    private async Task<IDbContextTransaction?> BeginProductionTransactionAsync(CancellationToken cancellationToken)
    {
        if (string.Equals(db.Database.ProviderName,"Microsoft.EntityFrameworkCore.InMemory",StringComparison.Ordinal)) return null;
        return await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
    }

    private static AIRelease ToContract(AiModelReleaseRecord x)=>new(x.Id,x.TenantCode,x.ReleaseCode,x.ModelProvider,x.ModelVersion,x.PromptVersion,x.SchemaCode,x.SchemaVersion,x.GoldenDatasetCode,x.Status,x.MinimumAcceptanceRate,x.CreatedAtUtc,x.PromotedAtUtc,x.RetiredAtUtc);
    private static void Require(string value,int max,string name){if(string.IsNullOrWhiteSpace(value)||value.Trim().Length>max)throw new ArgumentException($"{name} is required and must be <= {max} characters.",name);}
}
