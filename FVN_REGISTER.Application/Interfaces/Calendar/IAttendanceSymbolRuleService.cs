using FVN_REGISTER.Contract.Dtos.Calendar;
using FVN_REGISTER.Contract.Utils;

namespace FVN_REGISTER.Application.Interfaces.Calendar;

public interface IAttendanceSymbolRuleService
{
    Task<ServiceResult<IReadOnlyList<AttendanceSymbolRuleDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ServiceResult<AttendanceSymbolRuleDto>> SaveAsync(int? id, AttendanceSymbolRuleUpsertRequest request, int actorUserId, CancellationToken ct = default);
    Task<ServiceResult<object>> DeactivateAsync(int id, int actorUserId, CancellationToken ct = default);
    Task<AttendanceSymbolRuleTestResultDto> EvaluateAsync(DateOnly workDate, string dayType, string? shiftCode, int? shiftId, DateTime? checkIn, DateTime? checkOut, int? actualOtMinutes = null, CancellationToken ct = default);
    Task<AttendanceSymbolRuleTestResultDto> TestAsync(AttendanceSymbolRuleTestRequest request, CancellationToken ct = default);
    /// <summary>Re-resolve current DayType values and refresh derived symbols after HRM holiday synchronization.</summary>
    Task RefreshSymbolsAsync(CancellationToken ct = default);
}
