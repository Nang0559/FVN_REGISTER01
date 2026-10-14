using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

public sealed class HRCaseQueryService(AiPlatformDbContext db, IHRCaseReviewerAuthorization authorization) : IHRCaseQueryService
{
    public async Task<IReadOnlyList<HRCaseInboxItem>> GetPendingAsync(string tenantCode, string actorId, int limit,
        CancellationToken cancellationToken = default)
    {
        await EnsureReviewerAsync(tenantCode, actorId, cancellationToken);
        limit = Math.Clamp(limit, 1, 100);
        var rows = await db.HRCases.AsNoTracking()
            .Where(x => x.TenantCode == tenantCode && x.Status == "PendingHRReview")
            .OrderBy(x => x.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<HRCaseInboxItem?> GetAsync(string tenantCode, string actorId, Guid caseId,
        CancellationToken cancellationToken = default)
    {
        await EnsureReviewerAsync(tenantCode, actorId, cancellationToken);
        var row = await db.HRCases.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == caseId && x.TenantCode == tenantCode, cancellationToken);
        return row is null ? null : Map(row);
    }

    private async Task EnsureReviewerAsync(string tenantCode, string actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantCode) || string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("Signed tenant and actor context is required.");
        if (!await authorization.CanReviewAsync(tenantCode, actorId, ct))
            throw new UnauthorizedAccessException("Actor is not authorized to view HR cases.");
    }

    private static HRCaseInboxItem Map(AiHRCaseRecord x)
        => new(x.Id, x.EmployeeCode, x.CaseType, x.WorkDate, x.Outcome, x.Status, x.UserStatement,
            x.SuggestedReply, x.InternalHRNote, x.AnalysisJson, x.EvidenceReference,
            x.OfficialSource, x.OfficialRecordReference, x.CreatedAtUtc, x.CreatedBy);
}
