using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.AI.Infrastructure.Services;

public sealed class GoldenDatasetService(AiPlatformDbContext db) : IGoldenDatasetService, IGoldenDatasetAdministration
{
    public async Task<Guid> AddCaseAsync(string tenantCode, string actorId, string datasetCode, string caseType,
        string inputJson, string expectedJson, string? rubricJson, CancellationToken cancellationToken = default)
    {
        Require(tenantCode, 64, nameof(tenantCode)); Require(actorId, 128, nameof(actorId)); Require(datasetCode, 128, nameof(datasetCode)); Require(caseType, 64, nameof(caseType));
        ValidateJson(inputJson, nameof(inputJson)); ValidateJson(expectedJson, nameof(expectedJson)); if (rubricJson is not null) ValidateJson(rubricJson, nameof(rubricJson));
        var record = new AiGoldenDatasetCaseRecord { Id = Guid.NewGuid(), TenantCode = tenantCode.Trim(), DatasetCode = datasetCode.Trim(), CaseType = caseType.Trim(), InputJson = inputJson, ExpectedJson = expectedJson, RubricJson = rubricJson, IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow };
        db.Set<AiGoldenDatasetCaseRecord>().Add(record); await db.SaveChangesAsync(cancellationToken); return record.Id;
    }

    public async Task EvaluateCaseAsync(string tenantCode, Guid caseId, Guid? releaseId, string actualJson, bool passed, string? failureReason, CancellationToken cancellationToken = default)
    {
        Require(tenantCode, 64, nameof(tenantCode)); ValidateJson(actualJson, nameof(actualJson));
        var tenant = tenantCode.Trim();
        var exists = await db.Set<AiGoldenDatasetCaseRecord>().AnyAsync(x => x.Id == caseId && x.TenantCode == tenant && x.IsActive, cancellationToken);
        if (!exists) throw new KeyNotFoundException("Golden dataset case was not found.");
        if (releaseId.HasValue && !await db.Set<AiModelReleaseRecord>().AnyAsync(x => x.Id == releaseId.Value && x.TenantCode == tenant, cancellationToken)) throw new KeyNotFoundException("AI release was not found.");
        db.Set<AiGoldenDatasetEvaluationRecord>().Add(new AiGoldenDatasetEvaluationRecord { Id = Guid.NewGuid(), TenantCode = tenant, CaseId = caseId, ReleaseId = releaseId, Passed = passed, ActualJson = actualJson, FailureReason = failureReason?.Trim(), EvaluatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<GoldenDatasetSummary> GetSummaryAsync(string tenantCode, string datasetCode, CancellationToken cancellationToken = default)
        => GetSummaryCoreAsync(tenantCode, datasetCode, null, cancellationToken);

    public Task<GoldenDatasetSummary> GetSummaryAsync(string tenantCode, string datasetCode, Guid releaseId, CancellationToken cancellationToken = default)
        => GetSummaryCoreAsync(tenantCode, datasetCode, releaseId, cancellationToken);

    private async Task<GoldenDatasetSummary> GetSummaryCoreAsync(string tenantCode, string datasetCode, Guid? releaseId, CancellationToken cancellationToken)
    {
        Require(tenantCode, 64, nameof(tenantCode)); Require(datasetCode, 128, nameof(datasetCode));
        var tenant = tenantCode.Trim(); var dataset = datasetCode.Trim();
        var cases = await db.Set<AiGoldenDatasetCaseRecord>().AsNoTracking()
            .Where(x => x.TenantCode == tenant && x.DatasetCode == dataset)
            .Select(x => new { x.Id, x.IsActive }).ToListAsync(cancellationToken);
        var ids = cases.Where(x => x.IsActive).Select(x => x.Id).ToArray();
        if (ids.Length == 0) return new(dataset, cases.Count, 0, 0, 0, 0, null);
        var query = db.Set<AiGoldenDatasetEvaluationRecord>().AsNoTracking().Where(x => ids.Contains(x.CaseId) && x.TenantCode == tenant);
        if (releaseId.HasValue) query = query.Where(x => x.ReleaseId == releaseId.Value);
        var evaluations = await query.GroupBy(x => x.CaseId).Select(g => g.OrderByDescending(x => x.EvaluatedAtUtc).First()).ToListAsync(cancellationToken);
        var passed = evaluations.Count(x => x.Passed);
        return new(dataset, cases.Count, ids.Length, evaluations.Count, passed, evaluations.Count == 0 ? 0 : (double)passed / evaluations.Count, evaluations.Count == 0 ? null : evaluations.Max(x => x.EvaluatedAtUtc));
    }

    private static void Require(string value, int max, string name) { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max) throw new ArgumentException($"{name} is required and must be <= {max} characters.", name); }
    private static void ValidateJson(string value, string name) { if (string.IsNullOrWhiteSpace(value) || value.Length > 200_000) throw new ArgumentException($"{name} must contain valid JSON and be <= 200000 characters.", name); try { using var _ = JsonDocument.Parse(value); } catch (JsonException) { throw new ArgumentException($"{name} must contain valid JSON.", name); } }
}
