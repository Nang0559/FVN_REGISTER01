using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

public sealed class HRCaseReviewService(AiPlatformDbContext db, IHRCaseReviewerAuthorization authorization) : IHRCaseReviewService
{
    public async Task<HRCaseReviewReceipt> ReviewAsync(string tenantCode, string actorId, Guid caseId,
        HRCaseReviewRequest request, CancellationToken cancellationToken = default)
    {
        tenantCode = Required(tenantCode, 64, nameof(tenantCode));
        actorId = Required(actorId, 128, nameof(actorId));
        var decision = Required(request.Decision, 16, nameof(request.Decision));
        if (decision is not ("Approved" or "Rejected" or "Edited"))
            throw new ArgumentException("Decision must be Approved, Rejected or Edited.", nameof(request));
        if ((decision is "Approved" or "Edited") && string.IsNullOrWhiteSpace(request.FinalReply))
            throw new ArgumentException("FinalReply is required for Approved or Edited decisions.", nameof(request));
        if (request.FinalReply?.Length > 4000 || request.Comment?.Length > 2000)
            throw new ArgumentException("Review text exceeds the permitted length.", nameof(request));

        if (!await authorization.CanReviewAsync(tenantCode, actorId, cancellationToken))
            throw new UnauthorizedAccessException("Actor is not authorized to review HR cases.");

        var item = await db.HRCases.SingleOrDefaultAsync(x => x.Id == caseId && x.TenantCode == tenantCode, cancellationToken);
        if (item is null) throw new KeyNotFoundException("HR case was not found.");
        if (item.Status != "PendingHRReview")
            throw new InvalidOperationException("HR case has already been reviewed.");

        var now = DateTimeOffset.UtcNow;
        item.ReviewedBy = actorId;
        item.ReviewDecision = decision;
        item.FinalReply = decision == "Rejected" ? null : request.FinalReply!.Trim();
        item.ReviewComment = request.Comment?.Trim();
        item.ReviewedAtUtc = now;
        item.Status = decision == "Rejected" ? "Rejected" : "Approved";
        await db.SaveChangesAsync(cancellationToken);
        return new HRCaseReviewReceipt(item.Id, item.Status, decision, now);
    }

    private static string Required(string value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new ArgumentException($"Value must contain 1 to {max} characters.", name);
        return value.Trim();
    }
}
