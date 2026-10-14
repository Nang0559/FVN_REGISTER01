using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

public sealed class HRCaseQueryService(AiPlatformDbContext db, IHRCaseReviewerAuthorization authorization) : IHRCaseQueryService
{
    public async Task<IReadOnlyList<HRCaseInboxItem>> GetPendingAsync(string tenantCode, string actorId, int limit,
        CancellationToken cancellationToken = default)
    {
        EnsureContext(tenantCode, actorId);
        limit = Math.Clamp(limit, 1, 100);
        var rows = await db.HRCases.AsNoTracking()
            .Where(x => x.TenantCode == tenantCode && x.Status == "PendingHRReview")
            .OrderBy(x => x.CreatedAtUtc)
            .Take(500)
            .ToListAsync(cancellationToken);
        var visible = new List<HRCaseInboxItem>();
        foreach (var row in rows)
        {
            if (Enum.TryParse<HRCaseType>(row.CaseType, out var caseType) && await authorization.CanReviewCaseAsync(tenantCode, actorId, row.EmployeeCode, caseType, cancellationToken))
                visible.Add(Map(row));
            if (visible.Count >= limit) break;
        }
        return visible;
    }

    public async Task<HRCaseInboxItem?> GetAsync(string tenantCode, string actorId, Guid caseId,
        CancellationToken cancellationToken = default)
    {
        EnsureContext(tenantCode, actorId);
        var row = await db.HRCases.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == caseId && x.TenantCode == tenantCode, cancellationToken);
        if (row is null) return null;
        return Enum.TryParse<HRCaseType>(row.CaseType, out var caseType) &&
            await authorization.CanReviewCaseAsync(tenantCode, actorId, row.EmployeeCode, caseType, cancellationToken)
            ? Map(row) : null;
    }

    private static void EnsureContext(string tenantCode, string actorId)
    {
        if (string.IsNullOrWhiteSpace(tenantCode) || string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("Signed tenant and actor context is required.");
    }

    private static HRCaseInboxItem Map(AiHRCaseRecord x)
        => new(x.Id, x.EmployeeCode, x.CaseType, x.WorkDate, x.Outcome, x.Status, x.UserStatement,
            x.SuggestedReply, x.InternalHRNote, x.AnalysisJson, x.EvidenceReference, x.EvidenceCandidateJson,
            x.OfficialSource, x.OfficialRecordReference, x.CreatedAtUtc, x.CreatedBy);
}
