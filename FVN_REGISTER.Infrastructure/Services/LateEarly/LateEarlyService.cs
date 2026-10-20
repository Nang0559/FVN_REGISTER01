using FVN_REGISTER.Application.Interfaces.Approvals;
using FVN_REGISTER.Application.Interfaces.LateEarly;
using FVN_REGISTER.Application.Interfaces.OT;
using FVN_REGISTER.Application.Interfaces.Orchestrators;
using FVN_REGISTER.Application.Interfaces.Security;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Application.Models.Subjects;
using FVN_REGISTER.Contract.Requests.Approvals;
using FVN_REGISTER.Contract.Requests.LateEarly;
using FVN_REGISTER.Contract.Responses.LateEarly;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Constants;
using FVN_REGISTER.Core.Entities.HR;
using FVN_REGISTER.Core.Entities.LateEarly;
using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.LateEarly;

/// <summary>
/// Request lifecycle for late arrival / early leave. Registrar eligibility is delegated to
/// the same OT registration policy tables and Feature Operator scope; no separate registrar model.
/// </summary>
public sealed class LateEarlyService : ILateEarlyService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IOtRegistrationPolicy _registrationPolicy;
    private readonly IApprovalWorkflowOrchestrator<LateEarlyRequestSubject> _workflow;
    private readonly IApprovalSelectionService _approvalSelections;

    public LateEarlyService(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IAuthorizationService authorization,
        IOtRegistrationPolicy registrationPolicy,
        IApprovalWorkflowOrchestrator<LateEarlyRequestSubject> workflow,
        IApprovalSelectionService approvalSelections)
    {
        _uow = uow;
        _currentUser = currentUser;
        _authorization = authorization;
        _registrationPolicy = registrationPolicy;
        _workflow = workflow;
        _approvalSelections = approvalSelections;
    }

    public async Task<ServiceResult<LateEarlyRequestDto>> CreateDraftAsync(
        CreateLateEarlyRequestDto request, CancellationToken ct = default)
    {
        try
        {
            var user = _currentUser.GetCurrentUser()
                ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
            if (!Enum.IsDefined(request.LateEarlyType))
                throw new ArgumentException("Loại đăng ký không hợp lệ.");
            if (string.IsNullOrWhiteSpace(request.EmployeeCode))
                throw new ArgumentException("Phải chọn nhân viên cần đăng ký.");
            if (string.IsNullOrWhiteSpace(request.ReasonCode))
                throw new ArgumentException("Lý do đăng ký là bắt buộc.");

            var employee = await _uow.Repository<F03Employee>().Query().AsNoTracking()
                .Where(x => x.EmployeeCode == request.EmployeeCode.Trim())
                .Select(x => new { x.EmployeeCode, x.EmployeeName, x.DeptCode, x.PositionCode })
                .FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Không tìm thấy nhân viên được đăng ký.");

            if (!await _authorization.CanAccessAsync(
                    user, SecurityFunctionCodes.LateEarlyCreate, employee.EmployeeCode, employee.DeptCode, ct))
                throw new UnauthorizedAccessException("Bạn không có quyền tạo đăng ký đi muộn/về sớm trong phạm vi nhân viên này.");

            var onBehalf = !string.Equals(user.EmployeeCode, employee.EmployeeCode, StringComparison.OrdinalIgnoreCase);
            if (onBehalf && !await _registrationPolicy.CanRegisterForEmployeesAsync(
                    user, new[] { employee.EmployeeCode }, "LATE_EARLY", ct))
                throw new UnauthorizedAccessException("Bạn không phải người đăng ký được chỉ định cho phòng ban của nhân viên này.");

            var now = DateTime.Now;
            var retroactive = request.WorkDate.Date < now.Date
                || (request.WorkDate.Date == now.Date && request.PlannedTime < now.TimeOfDay);
            if (retroactive && string.IsNullOrWhiteSpace(request.ReasonNote))
                throw new ArgumentException("Đăng ký bổ sung sau thời điểm phát sinh phải ghi rõ lý do.");

            var duplicate = await _uow.Repository<F03LateEarlyRequest>().Query()
                .AnyAsync(x => x.IsActive == true
                    && x.EmployeeCode == employee.EmployeeCode
                    && x.WorkDate == request.WorkDate.Date
                    && x.LateEarlyType == request.LateEarlyType
                    && x.RequestStatus != ApprovalStatus.Cancelled
                    && x.RequestStatus != ApprovalStatus.Rejected, ct);
            if (duplicate)
                throw new InvalidOperationException("Nhân viên đã có đăng ký cùng loại trong ngày này.");

            var entity = new F03LateEarlyRequest
            {
                EmployeeCode = employee.EmployeeCode,
                DeptCode = employee.DeptCode,
                CreatedBy = user.UserId,
                RequestCode = $"LE-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..30],
                LateEarlyType = request.LateEarlyType,
                WorkDate = request.WorkDate.Date,
                PlannedTime = request.PlannedTime,
                ReasonCode = request.ReasonCode.Trim(),
                ReasonNote = request.ReasonNote?.Trim(),
                RegisteredByEmployeeCode = user.EmployeeCode ?? employee.EmployeeCode,
                IsOnBehalf = onBehalf,
                IsRetroactive = retroactive,
                RequestStatus = ApprovalStatus.Draft
            };

            await _uow.Repository<F03LateEarlyRequest>().AddAsync(entity, ct);
            await _uow.SaveChangesAsync(ct);
            return ServiceResult<LateEarlyRequestDto>.Ok(ToDto(entity, employee.EmployeeName), "Đã tạo bản nháp đăng ký.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException
            or ArgumentException or InvalidOperationException)
        {
            return ServiceResult<LateEarlyRequestDto>.Fail(ex.Message);
        }
    }

    public async Task<ServiceResult<LateEarlyRequestDto>> SubmitAsync(
        int requestId, List<ApprovalSelectionDto>? selections = null, CancellationToken ct = default)
    {
        try
        {
            var user = _currentUser.GetCurrentUser()
                ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
            var entity = await _uow.Repository<F03LateEarlyRequest>().Query()
                .FirstOrDefaultAsync(x => x.Id == requestId && x.IsActive == true, ct)
                ?? throw new KeyNotFoundException("Không tìm thấy đăng ký đi muộn/về sớm.");

            if (!await _authorization.CanAccessAsync(
                    user, SecurityFunctionCodes.LateEarlyEdit, entity.EmployeeCode, entity.DeptCode, ct))
                throw new UnauthorizedAccessException("Bạn không có quyền gửi đăng ký này.");

            if (entity.RequestStatus is not (ApprovalStatus.Draft or ApprovalStatus.NeedsRevision))
                throw new InvalidOperationException("Chỉ bản nháp hoặc yêu cầu cần chỉnh sửa mới được gửi duyệt.");
            if (entity.IsRetroactive && string.IsNullOrWhiteSpace(entity.ReasonNote))
                throw new ArgumentException("Đăng ký bổ sung phải có lý do chi tiết.");

            if (selections is { Count: > 0 })
                await _approvalSelections.ReplaceAsync(RequestModule.LateEarly, entity.Id, selections, user.UserId, ct);

            var employee = await _uow.Repository<F03Employee>().Query().AsNoTracking()
                .Where(x => x.EmployeeCode == entity.EmployeeCode)
                .Select(x => new { x.DeptCode, x.PositionCode })
                .FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Không tìm thấy thông tin chức vụ của nhân viên.");

            entity.RequestStatus = ApprovalStatus.Pending;
            await _uow.SaveChangesAsync(ct);

            var context = ApprovalBuildContext.ForLateEarly(
                entity.Id, entity.EmployeeCode, entity.DeptCode ?? employee.DeptCode,
                employee.PositionCode, entity.IsRetroactive, entity.LateEarlyType.ToString());

            var approval = await _workflow.InitApprovalAsync(entity.Id, context, ct);
            if (!approval.IsSuccess)
            {
                entity.RequestStatus = ApprovalStatus.Draft;
                await _uow.SaveChangesAsync(ct);
                return ServiceResult<LateEarlyRequestDto>.Fail(
                    approval.Message ?? "Không thể khởi tạo luồng duyệt đi muộn/về sớm.");
            }

            return ServiceResult<LateEarlyRequestDto>.Ok(await MapAsync(entity, ct), "Đã gửi đăng ký vào quy trình phê duyệt.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException
            or ArgumentException or InvalidOperationException)
        {
            return ServiceResult<LateEarlyRequestDto>.Fail(ex.Message);
        }
    }

    public async Task<ServiceResult<LateEarlyRequestDto>> GetAsync(int requestId, CancellationToken ct = default)
    {
        try
        {
            var user = _currentUser.GetCurrentUser()
                ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
            var entity = await _uow.Repository<F03LateEarlyRequest>().Query().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == requestId && x.IsActive == true, ct)
                ?? throw new KeyNotFoundException("Không tìm thấy đăng ký.");
            if (!await _authorization.CanAccessAsync(
                    user, SecurityFunctionCodes.LateEarlyView, entity.EmployeeCode, entity.DeptCode, ct))
                throw new UnauthorizedAccessException("Bạn không có quyền xem đăng ký này.");
            return ServiceResult<LateEarlyRequestDto>.Ok(await MapAsync(entity, ct));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException
            or ArgumentException or InvalidOperationException)
        {
            return ServiceResult<LateEarlyRequestDto>.Fail(ex.Message);
        }
    }

    public async Task<ServiceResult<List<LateEarlyRequestDto>>> GetMineAsync(CancellationToken ct = default)
    {
        try
        {
            var user = _currentUser.GetCurrentUser()
                ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
            var scope = await _authorization.GetScopeAsync(user.UserId, SecurityFunctionCodes.LateEarlyView, ct);
            var query = _uow.Repository<F03LateEarlyRequest>().Query().AsNoTracking()
                .Where(x => x.IsActive == true);

            if (scope == AuthorizationScopeCodes.Own || scope == AuthorizationScopeCodes.Employee)
                query = query.Where(x => x.EmployeeCode == user.EmployeeCode || x.RegisteredByEmployeeCode == user.EmployeeCode);
            else if (scope == AuthorizationScopeCodes.Department)
                query = query.Where(x => x.DeptCode == user.DeptCode);
            else if (scope != AuthorizationScopeCodes.All)
                query = query.Where(x => false);

            var entities = await query.OrderByDescending(x => x.WorkDate).ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
            var codes = entities.Select(x => x.EmployeeCode).Distinct().ToList();
            var names = await _uow.Repository<F03Employee>().Query().AsNoTracking()
                .Where(x => codes.Contains(x.EmployeeCode))
                .Select(x => new { x.EmployeeCode, x.EmployeeName })
                .ToDictionaryAsync(x => x.EmployeeCode, x => x.EmployeeName, ct);
            return ServiceResult<List<LateEarlyRequestDto>>.Ok(entities
                .Select(x => ToDto(x, names.GetValueOrDefault(x.EmployeeCode))).ToList());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException
            or ArgumentException or InvalidOperationException)
        {
            return ServiceResult<List<LateEarlyRequestDto>>.Fail(ex.Message);
        }
    }

    public async Task<ServiceResult> CancelAsync(int requestId, string? reason, CancellationToken ct = default)
    {
        try
        {
            var user = _currentUser.GetCurrentUser()
                ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
            var entity = await _uow.Repository<F03LateEarlyRequest>().Query()
                .FirstOrDefaultAsync(x => x.Id == requestId && x.IsActive == true, ct)
                ?? throw new KeyNotFoundException("Không tìm thấy đăng ký.");
            if (!await _authorization.CanAccessAsync(
                    user, SecurityFunctionCodes.LateEarlyCancel, entity.EmployeeCode, entity.DeptCode, ct))
                throw new UnauthorizedAccessException("Bạn không có quyền hủy đăng ký này.");
            if (entity.RequestStatus is not (ApprovalStatus.Draft or ApprovalStatus.Pending or ApprovalStatus.NeedsRevision))
                throw new InvalidOperationException("Chỉ bản nháp hoặc yêu cầu chưa hoàn tất mới được hủy.");

            entity.RequestStatus = ApprovalStatus.Cancelled;
            entity.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            await _uow.SaveChangesAsync(ct);
            return ServiceResult.Ok("Đã hủy đăng ký.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or KeyNotFoundException
            or ArgumentException or InvalidOperationException)
        {
            return ServiceResult.Fail(ex.Message);
        }
    }

    private async Task<LateEarlyRequestDto> MapAsync(F03LateEarlyRequest entity, CancellationToken ct)
    {
        var name = await _uow.Repository<F03Employee>().Query().AsNoTracking()
            .Where(x => x.EmployeeCode == entity.EmployeeCode)
            .Select(x => x.EmployeeName)
            .FirstOrDefaultAsync(ct);
        return ToDto(entity, name);
    }

    private static LateEarlyRequestDto ToDto(F03LateEarlyRequest entity, string? employeeName) => new()
    {
        Id = entity.Id,
        RequestCode = entity.RequestCode,
        EmployeeCode = entity.EmployeeCode,
        EmployeeName = employeeName,
        DeptCode = entity.DeptCode,
        LateEarlyType = entity.LateEarlyType,
        WorkDate = entity.WorkDate,
        PlannedTime = entity.PlannedTime,
        ReasonCode = entity.ReasonCode,
        ReasonNote = entity.ReasonNote,
        RegisteredByEmployeeCode = entity.RegisteredByEmployeeCode,
        IsOnBehalf = entity.IsOnBehalf,
        IsRetroactive = entity.IsRetroactive,
        RequestStatus = entity.RequestStatus,
        CreatedAt = entity.CreatedAt
    };
}
