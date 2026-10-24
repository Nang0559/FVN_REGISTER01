using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

/// <summary>Produces evaluation metrics from human labels; it does not train or deploy models.</summary>
public sealed class FeedbackEvaluationService(AiPlatformDbContext db) : IFeedbackEvaluationService
{
    public async Task<FeedbackEvaluationSummary> GetSummaryAsync(string tenantCode, DateTimeOffset fromUtc,
        DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantCode) || tenantCode.Trim().Length > 64)
            throw new ArgumentException("Tenant code is required.", nameof(tenantCode));
        if (fromUtc >= toUtc || toUtc - fromUtc > TimeSpan.FromDays(366))
            throw new ArgumentException("Evaluation range must be positive and no longer than 366 days.");

        var records = await db.Feedback.AsNoTracking()
            .Where(x => x.TenantCode == tenantCode.Trim() && x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtc)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50000)
            .Select(x => new { x.FeedbackType, x.ParserVersion, x.ModelVersion, x.PromptVersion })
            .ToListAsync(cancellationToken);

        var accepted = records.Count(x => x.FeedbackType == "Accept");
        var rejected = records.Count(x => x.FeedbackType == "Reject");
        var corrected = records.Count(x => x.FeedbackType == "Correct");
        var ratings = records.Count(x => x.FeedbackType == "AnswerRating");
        var decisions = accepted + rejected + corrected;
        var groups = records.GroupBy(x => new { x.ParserVersion, x.ModelVersion, x.PromptVersion })
            .Select(g => new FeedbackVersionMetric(g.Key.ParserVersion, g.Key.ModelVersion, g.Key.PromptVersion,
                g.Count(x => x.FeedbackType == "Accept"),
                g.Count(x => x.FeedbackType == "Reject"),
                g.Count(x => x.FeedbackType == "Correct"),
                g.Count(x => x.FeedbackType == "AnswerRating")))
            .OrderByDescending(x => x.Accepted + x.Rejected + x.Corrected + x.AnswerRatings)
            .ToArray();

        return new FeedbackEvaluationSummary(records.Count, accepted, rejected, corrected, ratings,
            decisions == 0 ? 0 : (double)accepted / decisions, groups);
    }
}
