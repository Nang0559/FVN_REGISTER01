using FVN_REGISTER.Contract.Dtos.Authentication;
using FVN_REGISTER.Contract.Dtos.Registration;

namespace FVN_REGISTER.Application.Interfaces.OT;

public interface IOtRegistrationPolicy
{
    Task<bool> CanRegisterForOthersAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default);
    Task<bool> CanRegisterSelfOnlyAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default);
    Task<RegistrationPolicyDecisionDto> GetDecisionAsync(UserIdentityDto user, int deptCode, string registerType = "OT", CancellationToken ct = default);
}
