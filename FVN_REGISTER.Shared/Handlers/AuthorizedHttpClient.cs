using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Shared.Utils;
using Microsoft.Extensions.Logging;
using FVN_REGISTER.Shared.Services.Loading;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FVN_REGISTER.Shared.Handlers
{
    public class AuthorizedHttpClient : IHttpClientWithAuth
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AuthorizedHttpClient> _logger;
        private readonly ITokenStorage _tokenStorage;
        private readonly ILoadingService _loading;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public AuthorizedHttpClient(HttpClient httpClient, ILogger<AuthorizedHttpClient> logger, ITokenStorage tokenStorage, ILoadingService loading)
        {
            _httpClient = httpClient; _logger = logger; _tokenStorage = tokenStorage; _loading = loading;
            _logger.LogDebug("[HTTP] Base Address: {Base}", _httpClient.BaseAddress);
        }

        private async Task AttachTokenAsync(HttpRequestMessage request)
        {
            var token = await _tokenStorage.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim('"').Trim());
            else
                _logger.LogWarning("[AUTH] Missing token | Path={Path}", request.RequestUri?.AbsolutePath);
        }

        private async Task<ApiResponse<T>> SendAsync<T>(Func<HttpRequestMessage> requestFactory, string url, CancellationToken ct, bool showLoading)
        {
            try
            {
                using var loadingScope = showLoading ? _loading.Begin() : null;
                using var request = requestFactory();
                await AttachTokenAsync(request);
                using var response = await _httpClient.SendAsync(request, ct);

                if (response.StatusCode == HttpStatusCode.Unauthorized && !IsAuthRefreshRequest(url))
                {
                    var refreshed = await TryRefreshTokenAsync(ct);
                    if (refreshed)
                    {
                        using var retry = requestFactory();
                        await AttachTokenAsync(retry);
                        using var retryResponse = await _httpClient.SendAsync(retry, ct);
                        return await HandleResponseAsync<T>(retryResponse, url, ct);
                    }
                }

                return await HandleResponseAsync<T>(response, url, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HTTP] Connection error: {Url}", url);
                return ApiResponse<T>.Fail("Không thể kết nối đến server.");
            }
        }

        private static bool IsAuthRefreshRequest(string url) => url.Contains("api/auth/refresh", StringComparison.OrdinalIgnoreCase);

        private async Task<bool> TryRefreshTokenAsync(CancellationToken ct)
        {
            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken)) return false;

            await _refreshLock.WaitAsync(ct);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh")
                {
                    Content = JsonContent.Create(new FVN_REGISTER.Contract.Requests.Auths.RefreshTokenRequestDto { RefreshToken = refreshToken })
                };
                using var response = await _httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) return false;

                var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(cancellationToken: ct);
                if (payload?.IsSuccess != true || payload.Data.ValueKind != JsonValueKind.Object) return false;
                if (!payload.Data.TryGetProperty("token", out var tokenNode) && !payload.Data.TryGetProperty("Token", out tokenNode)) return false;

                var token = tokenNode.GetString();
                if (string.IsNullOrWhiteSpace(token)) return false;
                await _tokenStorage.SetTokenAsync(token);
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AUTH] Refresh token request failed.");
                return false;
            }
            finally { _refreshLock.Release(); }
        }

        private async Task<ApiResponse<T>> HandleResponseAsync<T>(HttpResponseMessage response, string url, CancellationToken ct)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                var isUnauthorized = response.StatusCode == HttpStatusCode.Unauthorized;
                var errorResponse = TryDeserialize<ApiResponse<T>>(content);
                if (errorResponse != null)
                {
                    errorResponse.IsSuccess = false; errorResponse.StatusCode = (int)response.StatusCode; errorResponse.IsUnauthorized = isUnauthorized;
                    if (!string.IsNullOrWhiteSpace(errorResponse.Message)) return errorResponse;
                }
                var fallbackMessage = response.StatusCode switch
                {
                    HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
                    HttpStatusCode.Unauthorized => "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.",
                    HttpStatusCode.NotFound => "Không tìm thấy tài nguyên yêu cầu.",
                    _ => string.IsNullOrWhiteSpace(content) ? "Server request failed." : content
                };

                return new ApiResponse<T>
                {
                    IsSuccess = false,
                    StatusCode = (int)response.StatusCode,
                    Message = fallbackMessage,
                    IsUnauthorized = isUnauthorized
                };
            }

            var trimmed = content.TrimStart();
            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                var apiResult = TryDeserialize<ApiResponse<T>>(content);
                if (apiResult != null) return apiResult;
            }

            var raw = TryDeserialize<T>(content);
            if (raw != null) return ApiResponse<T>.Ok(raw);
            return ApiResponse<T>.Fail("Invalid response format.");
        }

        private static T? TryDeserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default;
            try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
            catch (JsonException) { return default; }
        }

        public Task<ApiResponse<T>> GetAsync<T>(string url, CancellationToken ct = default, bool showLoading = true)
            => SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Get, url), url, ct, showLoading);

        public Task<ApiResponse<T>> PostAsync<T>(string url, object data, CancellationToken ct = default, bool showLoading = true)
            => SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(data) }, url, ct, showLoading);

        public Task<ApiResponse<T>> PutAsync<T>(string url, object data, CancellationToken ct = default, bool showLoading = true)
            => SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(data) }, url, ct, showLoading);

        public Task<ApiResponse<T>> DeleteAsync<T>(string url, CancellationToken ct = default, bool showLoading = true)
            => SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Delete, url), url, ct, showLoading);

        public Task<ApiResponse<T>> PatchAsync<T>(string url, object data, CancellationToken ct = default, bool showLoading = true)
            => SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(data) }, url, ct, showLoading);

        public Task<ApiResponse<PaginationResult<T>>> GetPagedAsync<T>(string url, CancellationToken ct = default, bool showLoading = true)
            => GetAsync<PaginationResult<T>>(url, ct, showLoading);

        public async Task<ApiResponse<byte[]>> PostFileAsync(string url, object data, CancellationToken ct = default, bool showLoading = true)
        {
            try
            {
                var result = await SendFileWithRefreshAsync(
                    () => new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(data) },
                    url, ct, showLoading);

                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HTTP] POST file download error: {Url}", url);
                return ApiResponse<byte[]>.Fail("Download error");
            }
        }

        public async Task<ApiResponse<byte[]>> GetFileAsync(string url, CancellationToken ct = default, bool showLoading = true)
        {
            try
            {
                return await SendFileWithRefreshAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, url),
                    url, ct, showLoading);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HTTP] File download error: {Url}", url);
                return ApiResponse<byte[]>.Fail("Download error");
            }
        }

        private async Task<ApiResponse<byte[]>> SendFileWithRefreshAsync(
            Func<HttpRequestMessage> requestFactory,
            string url,
            CancellationToken ct,
            bool showLoading)
        {
            using var loadingScope = showLoading ? _loading.Begin() : null;
            using var request = requestFactory();
            await AttachTokenAsync(request);
            using var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized && !IsAuthRefreshRequest(url))
            {
                if (await TryRefreshTokenAsync(ct))
                {
                    using var retry = requestFactory();
                    await AttachTokenAsync(retry);
                    using var retryResponse = await _httpClient.SendAsync(retry, ct);
                    return await HandleFileResponseAsync(retryResponse, url, ct);
                }
            }

            return await HandleFileResponseAsync(response, url, ct);
        }

        private async Task<ApiResponse<byte[]>> HandleFileResponseAsync(HttpResponseMessage response, string url, CancellationToken ct)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[HTTP] File download failed: {Url} | Status={Status}", url, (int)response.StatusCode);
                var message = await response.Content.ReadAsStringAsync(ct);
                return ApiResponse<byte[]>.Fail(string.IsNullOrWhiteSpace(message) ? "Download failed" : message);
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            return ApiResponse<byte[]>.Ok(bytes);
        }

        public Task<ApiResponse<T>> PostMultipartAsync<T>(string url, MultipartFormDataContent content, CancellationToken ct = default, bool showLoading = true)
            => SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Post, url) { Content = content }, url, ct, showLoading);
    }
}
