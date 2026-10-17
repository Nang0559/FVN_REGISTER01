using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Registration;

namespace FVN_REGISTER.Application.Interfaces.OT;

public interface IOtRegistrationPolicy
{
    /// <summary>Registrar của phòng ban: chức vụ approver theo rule, user được chỉ định (rule hoặc Feature Operator OT.RegistrarManage), hoặc rule Everyone.</summary>
    Task<bool> IsRegistrarAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default);

    /// <summary>true khi user là registrar của MỌI phòng ban của các nhân viên được đăng ký (kể cả chính mình).</summary>
    Task<bool> CanRegisterForEmployeesAsync(UserIdentityDto user, IEnumerable<string> employeeCodes, string registerType = "OT", CancellationToken ct = default);

    /// <summary>true khi user là registrar của ÍT NHẤT một phòng ban (phòng mình, hoặc phòng/khối/toàn công ty được chỉ định). Dùng để hiện menu/dashboard cho người được chỉ định.</summary>
    Task<bool> IsAnyRegistrarAsync(UserIdentityDto user, string registerType = "OT", CancellationToken ct = default);

    Task<bool> CanRegisterForOthersAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default);
    Task<bool> CanRegisterSelfOnlyAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default);
    Task<RegistrationPolicyDecisionDto> GetDecisionAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default);
}
