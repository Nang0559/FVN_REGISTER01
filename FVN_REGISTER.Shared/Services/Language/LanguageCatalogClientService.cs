using System.Net.Http.Json;
using FVN_REGISTER.Contract.Dtos.Language;
using FVN_REGISTER.Contract.Responses;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Shared.Services.Language;

public sealed class LanguageCatalogClientService : ILanguageCatalogClientService
{
    private readonly HttpClient _http;
    private readonly ILogger<LanguageCatalogClientService> _logger;

    public LanguageCatalogClientService(HttpClient http, ILogger<LanguageCatalogClientService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ApiResponse<LocalizationRuntimeSnapshotDto>> GetRuntimeSnapshotAsync(long? sinceVersion, CancellationToken ct = default)
    {
        try
        {
            var url = sinceVersion.HasValue
                ? $"api/localization/runtime?sinceVersion={sinceVersion.Value}"
                : "api/localization/runtime";

            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return ApiResponse<LocalizationRuntimeSnapshotDto>.Fail($"HTTP {(int)response.StatusCode}", (int)response.StatusCode);

            return await response.Content.ReadFromJsonAsync<ApiResponse<LocalizationRuntimeSnapshotDto>>(cancellationToken: ct)
                   ?? ApiResponse<LocalizationRuntimeSnapshotDto>.Fail("Empty response.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The embedded catalog keeps the UI fully usable when the runtime texts cannot be loaded.
            _logger.LogWarning(ex, "Runtime language texts could not be loaded.");
            return ApiResponse<LocalizationRuntimeSnapshotDto>.Fail("Cannot load the runtime language texts.");
        }
    }
}
