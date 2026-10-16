using FVN_REGISTER.Contract.Dtos.EmailTemplates;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Shared.Handlers;

namespace FVN_REGISTER.Shared.Services.Emails
{
    public sealed class EmailAdminClientService : IEmailAdminClientService
    {
        private const string BaseUrl = "api/email-admin";
        private readonly IHttpClientWithAuth _http;

        public EmailAdminClientService(IHttpClientWithAuth http) => _http = http;

        public Task<ApiResponse<List<EmailProfileDto>>> GetProfilesAsync(CancellationToken ct = default)
            => _http.GetAsync<List<EmailProfileDto>>($"{BaseUrl}/profiles", ct);

        public Task<ApiResponse<object>> SaveProfileAsync(EmailProfileDto dto, CancellationToken ct = default)
            => _http.PostAsync<object>($"{BaseUrl}/profiles", dto, ct);

        // SMTP handshake can take up to the profile timeout; the page shows its own busy state.
        public Task<ApiResponse<object>> TestProfileAsync(int id, string toEmail, CancellationToken ct = default)
            => _http.PostAsync<object>($"{BaseUrl}/profiles/{id}/test", new EmailTestRequest { ToEmail = toEmail }, ct, showLoading: false);

        public Task<ApiResponse<List<EmailDispatchPolicyDto>>> GetPoliciesAsync(CancellationToken ct = default)
            => _http.GetAsync<List<EmailDispatchPolicyDto>>($"{BaseUrl}/policies", ct);

        public Task<ApiResponse<object>> SavePolicyAsync(EmailDispatchPolicyDto dto, CancellationToken ct = default)
            => _http.PostAsync<object>($"{BaseUrl}/policies", dto, ct);

        public Task<ApiResponse<object>> TogglePolicyAsync(int id, CancellationToken ct = default)
            => _http.PostAsync<object>($"{BaseUrl}/policies/{id}/toggle", new { }, ct);
    }
}
