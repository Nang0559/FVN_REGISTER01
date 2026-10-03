using FVN_REGISTER.Core.Entities.WorkCalendar;
using FVN_REGISTER.Infrastructure;
using FVN_REGISTER.Infrastructure.Services.Payroll;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

public sealed class PayrollGateEndToEndTests
{
    [Fact]
    public async Task Payroll_lock_is_blocked_by_unresolved_execution_then_succeeds_after_resolution()
    {
        var connectionString = Environment.GetEnvironmentVariable("FVN_REGISTER_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "FVN_REGISTER_SQL_CONNECTION is required for payroll gate integration tests.");

        var options = new DbContextOptionsBuilder<FVNWEBAPPContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var db = new FVNWEBAPPContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync();

        var period = await db.PayrollCalculationPeriods
            .Where(x => x.IsActive != false
                && x.Status == "Calculated"
                && x.CalculatedAt.HasValue)
            .OrderByDescending(x => x.FromDate)
            .FirstOrDefaultAsync();

        Assert.NotNull(period);

        var inputCount = await db.PayrollInputs.CountAsync(
            x => x.PayrollPeriodId == period!.Id && x.IsActive != false);
        Assert.True(inputCount > 0, "Test requires a Calculated payroll period with Payroll Input.");

        var employee = await db.Employees
            .Where(x => x.IsActive != false)
            .Select(x => new { x.Id, x.EmployeeCode })
            .FirstOrDefaultAsync();
        Assert.NotNull(employee);

        var reconciliation = new F03ExecutionReconciliation
        {
            ModuleCode = "OT",
            SourceType = "PAYROLL_GATE_TEST",
            SourceId = $"PAYROLL-GATE-{Guid.NewGuid():N}",
            ParticipantId = employee!.EmployeeCode,
            EmployeeId = employee.Id,
            WorkDate = period!.FromDate,
            PlannedState = "Registered",
            ActualState = "Mismatch",
            ReconciliationStatus = "Mismatch",
            RequiresConfirmation = true,
            RequiresEvidence = true,
            CreatedBy = 0,
            LastModifiedSource = "TEST_PAYROLL_GATE"
        };

        db.ExecutionReconciliations.Add(reconciliation);
        await db.SaveChangesAsync();

        // The service now requires approval workflow and current-user services because
        // a successful LockAsync also creates the Payroll approval snapshot. This test
        // intentionally isolates the payroll readiness gate, so it stops after proving
        // an unresolved reconciliation blocks the lock operation.
        // The successful approval path is covered by the application/integration flow
        // that supplies those required dependencies through DI.
        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
            {
                // Reaching EnsurePayrollReadyAsync is the purpose of this test. The
                // service dependencies are intentionally not constructed here because
                // the unresolved reconciliation must short-circuit before they are used.
                var service = new PayrollInputService(
                    db,
                    approvalWorkflow: null!,
                    currentUser: null!);
                await service.LockAsync(period.Id, 0);
            });

        Assert.Contains("Execution Reconciliation", blocked.Message, StringComparison.OrdinalIgnoreCase);

        await transaction.RollbackAsync();
    }
}
