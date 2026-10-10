using FVN_REGISTER.Contract.Dtos.Jobs;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Shared.Handlers;

namespace FVN_REGISTER.Shared.Services.Jobs;

public sealed class BackgroundJobClientService : IBackgroundJobClientService
{
    private readonly IHttpClientWithAuth _http;
    private const string Base = "api/background-jobs";

    public BackgroundJobClientService(IHttpClientWithAuth http) => _http = http;

    public Task<ApiResponse<List<BackgroundJobScheduleDto>>> GetAllAsync(CancellationToken ct = default)
        => _http.GetAsync<List<BackgroundJobScheduleDto>>(Base, ct);

    public Task<ApiResponse<BackgroundJobScheduleDto>> UpdateAsync(
        string jobKey, UpdateBackgroundJobScheduleRequest request, CancellationToken ct = default)
        => _http.PutAsync<BackgroundJobScheduleDto>($"{Base}/{Uri.EscapeDataString(jobKey)}", request, ct);

    public Task<ApiResponse<object>> RunNowAsync(string jobKey, CancellationToken ct = default)
        => _http.PostAsync<object>($"{Base}/{Uri.EscapeDataString(jobKey)}/run-now", new { }, ct);

    public Task<ApiResponse<BackgroundJobScheduleDto>> ResetAsync(string jobKey, CancellationToken ct = default)
        => _http.PostAsync<BackgroundJobScheduleDto>($"{Base}/{Uri.EscapeDataString(jobKey)}/reset", new { }, ct);
}
