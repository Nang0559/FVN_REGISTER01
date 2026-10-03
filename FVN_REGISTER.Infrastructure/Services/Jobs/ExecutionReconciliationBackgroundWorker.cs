using FVN_REGISTER.Application.Interfaces.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.Jobs;

public sealed class ExecutionReconciliationBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExecutionReconciliationBackgroundWorker> _logger;

    public ExecutionReconciliationBackgroundWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ExecutionReconciliationBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await RunOnceAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Execution reconciliation worker failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    private async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<FVNWEBAPPContext>();
        var employeeResolution = services.GetRequiredService<IExecutionEmployeeResolutionService>();
        var policies = await db.ExecutionPolicies.AsNoTracking()
            .Where(x => x.IsActive != false && x.ReconciliationMode != 0)
            .ToDictionaryAsync(x => x.ModuleCode, StringComparer.OrdinalIgnoreCase, ct);

        var providers = services.GetServices<IExecutionReconciliationModuleProvider>();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = today.AddDays(-2);
        var to = today;

        var expiredEmployeeActions = await db.ActionItems.AsNoTracking()
            .Where(x => x.IsActive != false
                && x.ActionType == "EXECUTION_RESULT_CONFIRMATION"
                && (x.Status == FVN_REGISTER.Core.Enums.ActionItemStatus.Open
                    || x.Status == FVN_REGISTER.Core.Enums.ActionItemStatus.InProgress)
                && x.DueAt.HasValue
                && x.DueAt.Value <= DateTime.Now)
            .Select(x => x.SourceId)
            .Distinct()
            .Take(200)
            .ToListAsync(ct);

        foreach (var sourceId in expiredEmployeeActions)
        {
            if (long.TryParse(sourceId, out var reconciliationId))
            {
                try
                {
                    await employeeResolution.ProcessExpiredEmployeeDecisionAsync(reconciliationId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Employee execution decision timeout processing failed for ReconciliationId={ReconciliationId}.",
                        reconciliationId);
                }
            }
        }

        foreach (var provider in providers)
        {
            if (!policies.TryGetValue(provider.ModuleCode, out var policy))
                continue;

            try
            {
                await provider.ReconcileAsync(from, to, policy.ReconciliationMode, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Execution reconciliation provider {ModuleCode} failed.",
                    provider.ModuleCode);
            }
        }
    }
}
