using FVN_REGISTER.Contract.Dtos.EmailTemplates;
using FVN_REGISTER.Contract.Responses;

namespace FVN_REGISTER.Shared.Services.Emails
{
    /// <summary>Client for api/email-admin: SMTP Email Profiles and per-template dispatch policies.</summary>
    public interface IEmailAdminClientService
    {
        Task<ApiResponse<List<EmailProfileDto>>> GetProfilesAsync(CancellationToken ct = default);
        Task<ApiResponse<object>> SaveProfileAsync(EmailProfileDto dto, CancellationToken ct = default);
        Task<ApiResponse<object>> TestProfileAsync(int id, string toEmail, CancellationToken ct = default);
        Task<ApiResponse<List<EmailDispatchPolicyDto>>> GetPoliciesAsync(CancellationToken ct = default);
        Task<ApiResponse<object>> SavePolicyAsync(EmailDispatchPolicyDto dto, CancellationToken ct = default);
        Task<ApiResponse<object>> TogglePolicyAsync(int id, CancellationToken ct = default);
    }
}
