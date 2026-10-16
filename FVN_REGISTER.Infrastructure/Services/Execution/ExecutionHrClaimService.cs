using System.Data;
using System.Data.Common;
using FVN_REGISTER.Application.Interfaces.Execution;
using FVN_REGISTER.Application.Interfaces.FeatureOperators;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Execution;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.WorkCalendar;
using FVN_REGISTER.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FVN_REGISTER.Infrastructure.Services.Execution;

public sealed class ExecutionHrClaimService : IExecutionHrClaimService
{
    private const string ClaimActionType = "EXECUTION_EVIDENCE_REVIEW";
    private const string ClaimSource = "EXECUTION_REVIEW_CLAIM";
    private static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(30);

    private readonly FVNWEBAPPContext _db;
    private readonly IAuthorizationService _authorization;
    private readonly IFeatureOperatorAssignmentService _operatorAssignments;

    public ExecutionHrClaimService(
        FVNWEBAPPContext db,
        IAuthorizationService authorization,
        IFeatureOperatorAssignmentService operatorAssignments)
    {
        _db = db;
        _authorization = authorization;
        _operatorAssignments = operatorAssignments;
    }

    public async Task EnsureClaimedAsync(
        int userId,
        long reconciliationId,
        CancellationToken cancellationToken = default)
    {
        await AcquireClaimLockAsync(reconciliationId, cancellationToken);

        var action = await _db.ActionItems
            .AsNoTracking()
            .Where(x => x.IsActive != false
                && x.ActionType == ClaimActionType
                && x.SourceId == reconciliationId.ToString()
                && x.Status == ActionItemStatus.InProgress
                && x.AssignedToUserId == userId)
            .OrderByDescending(x => x.ModifiedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (action is null || !IsActiveClaim(action, DateTime.Now))
            throw new FVN_REGISTER.Core.Exceptions.ForbiddenAccessException(
                "Yêu cầu chưa được bạn nhận xử lý hoặc phiên xử lý đã hết hạn. Vui lòng nhận xử lý lại.");
    }

    public Task<ServiceResult<ExecutionHrClaimDto>> ClaimAsync(
        int userId,
        string employeeCode,
        long reconciliationId,
        CancellationToken cancellationToken = default)
        => GuardAsync(() => ClaimCoreAsync(userId, employeeCode, reconciliationId, cancellationToken));

    public Task<ServiceResult<ExecutionHrClaimDto>> ReleaseAsync(
        int userId,
        string employeeCode,
        long reconciliationId,
        CancellationToken cancellationToken = default)
        => GuardAsync(() => ReleaseCoreAsync(userId, employeeCode, reconciliationId, cancellationToken));

    public Task<ServiceResult<IReadOnlyList<ExecutionHrClaimDto>>> GetClaimsAsync(
        int userId,
        string employeeCode,
        IReadOnlyCollection<long> reconciliationIds,
        CancellationToken cancellationToken = default)
        => GuardAsync(() => GetClaimsCoreAsync(userId, employeeCode, reconciliationIds, cancellationToken));

    private async Task<ExecutionHrClaimDto> ClaimCoreAsync(
        int userId,
        string employeeCode,
        long reconciliationId,
        CancellationToken cancellationToken)
    {
        await EnsureHrPermissionAsync(userId, cancellationToken);

        var target = await GetTargetAsync(reconciliationId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy reconciliation.");

        await EnsureTargetScopeAsync(userId, employeeCode, target.EmployeeCode, target.DeptCode, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await AcquireClaimLockAsync(reconciliationId, cancellationToken);

        var now = DateTime.Now;
        var existing = await _db.ActionItems
            .Where(x => x.IsActive != false
                && x.ActionType == ClaimActionType
                && x.SourceId == reconciliationId.ToString()
                && x.Status == ActionItemStatus.InProgress)
            .OrderByDescending(x => x.ModifiedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null && IsActiveClaim(existing, now))
        {
            var claimant = existing.AssignedToUserId.HasValue
                ? await _db.Users.AsNoTracking()
                    .Where(x => x.Id == existing.AssignedToUserId.Value && x.IsActive != false)
                    .Select(x => new { x.Id, x.EmployeeCode, x.FullName })
                    .SingleOrDefaultAsync(cancellationToken)
                : null;

            if (existing.AssignedToUserId == userId)
                return await BuildClaimDtoAsync(reconciliationId, userId, existing, cancellationToken);

            return new ExecutionHrClaimDto(
                reconciliationId,
                true,
                false,
                false,
                claimant?.Id ?? existing.AssignedToUserId,
                claimant?.EmployeeCode,
                claimant?.FullName,
                existing.ModifiedAt);
        }

        if (existing is not null)
        {
            existing.Status = ActionItemStatus.Open;
            existing.ModifiedBy = userId;
            existing.ModifiedAt = now;
            existing.LastModifiedSource = ClaimSource + "_EXPIRED";
        }

        var ownAction = await _db.ActionItems
            .Where(x => x.IsActive != false
                && x.ActionType == ClaimActionType
                && x.SourceId == reconciliationId.ToString()
                && x.AssignedToUserId == userId
                && (x.Status == ActionItemStatus.Open || x.Status == ActionItemStatus.InProgress || x.Status == ActionItemStatus.Expired))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownAction is null)
            throw new InvalidOperationException("Bạn chưa được phân công xử lý phản hồi này.");

        ownAction.Status = ActionItemStatus.InProgress;
        ownAction.ModifiedBy = userId;
        ownAction.ModifiedAt = now;
        ownAction.LastModifiedSource = ClaimSource;

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ExecutionHrClaimDto(
            reconciliationId,
            true,
            true,
            true,
            userId,
            employeeCode,
            await GetUserNameAsync(userId, cancellationToken),
            now);
    }

    private async Task<ExecutionHrClaimDto> ReleaseCoreAsync(
        int userId,
        string employeeCode,
        long reconciliationId,
        CancellationToken cancellationToken)
    {
        await EnsureHrPermissionAsync(userId, cancellationToken);

        var target = await GetTargetAsync(reconciliationId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy reconciliation.");

        await EnsureTargetScopeAsync(userId, employeeCode, target.EmployeeCode, target.DeptCode, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await AcquireClaimLockAsync(reconciliationId, cancellationToken);

        var action = await _db.ActionItems
            .Where(x => x.IsActive != false
                && x.ActionType == ClaimActionType
                && x.SourceId == reconciliationId.ToString()
                && x.Status == ActionItemStatus.InProgress)
            .OrderByDescending(x => x.ModifiedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (action is null)
            return new ExecutionHrClaimDto(reconciliationId, false, false, false, null, null, null, null);

        if (action.AssignedToUserId != userId)
            throw new FVN_REGISTER.Core.Exceptions.ForbiddenAccessException(
                "Yêu cầu đang được xử lý bởi người khác. Bạn chỉ có thể xem.");

        action.Status = ActionItemStatus.Open;
        action.ModifiedBy = userId;
        action.ModifiedAt = DateTime.Now;
        action.LastModifiedSource = ClaimSource + "_RELEASED";

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ExecutionHrClaimDto(reconciliationId, false, false, false, null, null, null, null);
    }

    private async Task<IReadOnlyList<ExecutionHrClaimDto>> GetClaimsCoreAsync(
        int userId,
        string employeeCode,
        IReadOnlyCollection<long> reconciliationIds,
        CancellationToken cancellationToken)
    {
        await EnsureHrPermissionAsync(userId, cancellationToken);

        var ids = reconciliationIds
            .Where(x => x > 0)
            .Distinct()
            .Take(500)
            .ToArray();

        if (ids.Length == 0)
            return Array.Empty<ExecutionHrClaimDto>();

        var sourceIds = ids.Select(x => x.ToString()).ToArray();

        var actions = await (
            from action in _db.ActionItems.AsNoTracking()
            join user in _db.Users.AsNoTracking()
                on action.AssignedToUserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            where action.IsActive != false
                && action.ActionType == ClaimActionType
                && action.Status == ActionItemStatus.InProgress
                && sourceIds.Contains(action.SourceId)
            select new
            {
                ReconciliationId = action.SourceId,
                action.AssignedToUserId,
                ClaimedAt = action.ModifiedAt,
                EmployeeCode = user != null ? user.EmployeeCode : null,
                FullName = user != null ? user.FullName : null,
                LastModifiedSource = action.LastModifiedSource
            })
            .ToListAsync(cancellationToken);

        var result = new List<ExecutionHrClaimDto>(ids.Length);
        foreach (var id in ids)
        {
            var action = actions
                .Where(x => x.ReconciliationId == id.ToString())
                .OrderByDescending(x => x.ClaimedAt)
                .FirstOrDefault();

            if (action is null || !IsActiveClaim(action.ClaimedAt, action.LastModifiedSource, DateTime.Now))
            {
                result.Add(new ExecutionHrClaimDto(id, false, false, false, null, null, null, null));
                continue;
            }

            var isMine = action.AssignedToUserId == userId;
            result.Add(new ExecutionHrClaimDto(
                id,
                true,
                isMine,
                isMine,
                action.AssignedToUserId,
                action.EmployeeCode,
                action.FullName,
                action.ClaimedAt));
        }

        return result;
    }

    private async Task AcquireClaimLockAsync(long reconciliationId, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
DECLARE @result int;
EXEC @result = sp_getapplock
    @Resource = @resource,
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 0;
SELECT @result;
""";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = $"FVN_REGISTER:ExecutionReview:{reconciliationId}";
        command.Parameters.Add(parameter);

        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (result < 0)
            throw new InvalidOperationException("Yêu cầu vừa được người khác nhận xử lý. Vui lòng tải lại danh sách.");
    }

    private static bool IsActiveClaim(F03ActionItem action, DateTime now)
        => IsActiveClaim(action.ModifiedAt, action.LastModifiedSource, now);

    private static bool IsActiveClaim(DateTime? claimedAt, string? source, DateTime now)
        => string.Equals(source, ClaimSource, StringComparison.OrdinalIgnoreCase)
            && claimedAt.HasValue
            && now - claimedAt.Value < ClaimTimeout;

    private async Task<dynamic?> GetTargetAsync(long reconciliationId, CancellationToken cancellationToken)
    {
        return await (
            from reconciliation in _db.ExecutionReconciliations.AsNoTracking()
            join employee in _db.Employees.AsNoTracking()
                on reconciliation.EmployeeId equals employee.Id
            where reconciliation.Id == reconciliationId
                && reconciliation.IsActive != false
                && reconciliation.ReconciliationStatus != "Resolved"
            select new
            {
                EmployeeCode = employee.EmployeeCode,
                DeptCode = employee.DeptCode
            }).SingleOrDefaultAsync(cancellationToken);
    }

    private async Task EnsureHrPermissionAsync(int userId, CancellationToken cancellationToken)
    {
        var actor = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive != false)
            .Select(x => new UserIdentityDto
            {
                UserId = x.Id,
                EmployeeCode = x.EmployeeCode,
                DeptCode = x.DeptCode,
                Permission = x.PermissionCode,
                FullName = x.FullName,
                LevelApprove = x.LevelApprove,
                IsLoggedIn = true
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Tài khoản không tồn tại hoặc đã bị khóa.");

        var hasPermission = await _authorization.HasAsync(
            actor,
            SecurityFunctionCodes.ExecutionReview,
            cancellationToken);

        var assigned = await _operatorAssignments.CanOperateAsync(
            userId,
            actor.EmployeeCode,
            SecurityFunctionCodes.ExecutionReview,
            "EXECUTION_REVIEW",
            null,
            cancellationToken);

        if (!FeatureOperatorAuthorizationPolicy.CanOperate(SecurityFunctionCodes.ExecutionReview, hasPermission, assigned))
            throw new FVN_REGISTER.Core.Exceptions.ForbiddenAccessException(
                "Tài khoản không có quyền Execution Review (qua role hoặc chỉ định operator).");
    }

    private async Task EnsureTargetScopeAsync(
        int userId,
        string actorEmployeeCode,
        string targetEmployeeCode,
        int? targetDeptCode,
        CancellationToken cancellationToken)
    {
        var actor = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive != false)
            .Select(x => new UserIdentityDto
            {
                UserId = x.Id,
                EmployeeCode = x.EmployeeCode,
                DeptCode = x.DeptCode,
                Permission = x.PermissionCode,
                FullName = x.FullName,
                LevelApprove = x.LevelApprove,
                IsLoggedIn = true
            })
            .SingleAsync(cancellationToken);

        var hasPermission = await _authorization.HasAsync(
            actor,
            SecurityFunctionCodes.ExecutionReview,
            cancellationToken);

        var assigned = await _operatorAssignments.CanOperateAsync(
            userId,
            actorEmployeeCode,
            SecurityFunctionCodes.ExecutionReview,
            "EXECUTION_REVIEW",
            null,
            cancellationToken);

        if (!FeatureOperatorAuthorizationPolicy.CanOperate(SecurityFunctionCodes.ExecutionReview, hasPermission, assigned))
            throw new FVN_REGISTER.Core.Exceptions.ForbiddenAccessException(
                "Tài khoản không có quyền Execution Review (qua role hoặc chỉ định operator).");

        if (!await _authorization.CanAccessAsync(
                actor,
                SecurityFunctionCodes.ExecutionReview,
                targetEmployeeCode,
                targetDeptCode,
                cancellationToken))
        {
            throw new FVN_REGISTER.Core.Exceptions.ForbiddenAccessException(
                $"HR không có scope xử lý nhân viên {targetEmployeeCode}.");
        }
    }

    private Task<string?> GetUserNameAsync(int userId, CancellationToken cancellationToken)
        => _db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive != false)
            .Select(x => x.FullName)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<ExecutionHrClaimDto> BuildClaimDtoAsync(
        long reconciliationId,
        int userId,
        F03ActionItem action,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive != false)
            .Select(x => new { x.Id, x.EmployeeCode, x.FullName })
            .SingleOrDefaultAsync(cancellationToken);

        return new ExecutionHrClaimDto(
            reconciliationId,
            true,
            true,
            true,
            user?.Id ?? userId,
            user?.EmployeeCode,
            user?.FullName,
            action.ModifiedAt);
    }

    private static Task<ServiceResult<T>> GuardAsync<T>(Func<Task<T>> action)
    {
        return ExecuteAsync(action);
    }

    private static async Task<ServiceResult<T>> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return ServiceResult<T>.Ok(await action());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
            or KeyNotFoundException
            or ArgumentException
            or InvalidOperationException
            or FVN_REGISTER.Core.Exceptions.ForbiddenAccessException)
        {
            return ServiceResult<T>.Fail(ex.Message);
        }
    }
}
