using FVN_REGISTER.Contract.Requests.Approvals;
using FVN_REGISTER.Contract.Requests.LateEarly;
using FVN_REGISTER.Contract.Responses.LateEarly;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.LateEarly;

public interface ILateEarlyService
{
    Task<ServiceResult<LateEarlyRequestDto>> CreateDraftAsync(CreateLateEarlyRequestDto request, CancellationToken ct = default);
    Task<ServiceResult<LateEarlyRequestDto>> SubmitAsync(int requestId, List<ApprovalSelectionDto>? selections = null, CancellationToken ct = default);
    Task<ServiceResult<LateEarlyRequestDto>> GetAsync(int requestId, CancellationToken ct = default);
    Task<ServiceResult<List<LateEarlyRequestDto>>> GetMineAsync(CancellationToken ct = default);
    Task<ServiceResult> CancelAsync(int requestId, string? reason, CancellationToken ct = default);
}
