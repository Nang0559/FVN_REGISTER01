using FVN_REGISTER.Contract.Dtos.Notifications;
using FVN_REGISTER.Shared.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FVN_REGISTER.Shared.Services.Notifications
{
    public interface IPushClientService
    {
        /// <summary>"unsupported" | "denied" | "granted" | "default"</summary>
        Task<string> GetStateAsync();

        /// <summary>Request permission (if needed), subscribe this browser and bind it to the signed-in user.</summary>
        Task<bool> EnableAsync(CancellationToken ct = default);

        /// <summary>Unbind this device on the server, unsubscribe locally and clear the badge. Call before logout.</summary>
        Task DisableAsync(CancellationToken ct = default);

        /// <summary>Set (or clear when 0) the app-icon badge while the app is open.</summary>
        Task SetBadgeAsync(int count);
    }

    public sealed class PushClientService : IPushClientService
    {
        private readonly IJSRuntime _js;
        private readonly IHttpClientWithAuth _http;
        private readonly ILogger<PushClientService> _logger;

        public PushClientService(IJSRuntime js, IHttpClientWithAuth http, ILogger<PushClientService> logger)
        {
            _js = js;
            _http = http;
            _logger = logger;
        }

        public async Task<string> GetStateAsync()
        {
            try
            {
                if (!await _js.InvokeAsync<bool>("fvnPush.isSupported")) return "unsupported";
                return await _js.InvokeAsync<string>("fvnPush.permission");
            }
            catch { return "unsupported"; }
        }

        public async Task<bool> EnableAsync(CancellationToken ct = default)
        {
            try
            {
                var key = await _http.GetAsync<PushPublicKeyDto>("api/push/public-key", ct);
                if (!key.IsSuccess || string.IsNullOrWhiteSpace(key.Data?.PublicKey)) return false;

                var sub = await _js.InvokeAsync<PushSubscribeRequestDto?>("fvnPush.subscribe", ct, key.Data.PublicKey);
                if (sub == null) return false;

                var result = await _http.PostAsync<object>("api/push/subscribe", sub, ct);
                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PUSH-CLIENT] EnableAsync failed");
                return false;
            }
        }

        public async Task DisableAsync(CancellationToken ct = default)
        {
            try
            {
                var endpoint = await _js.InvokeAsync<string?>("fvnPush.currentEndpoint");
                if (!string.IsNullOrWhiteSpace(endpoint))
                    await _http.PostAsync<object>("api/push/unsubscribe", new PushUnsubscribeRequestDto { Endpoint = endpoint }, ct);

                await _js.InvokeVoidAsync("fvnPush.unsubscribe");
                await _js.InvokeVoidAsync("fvnPush.setBadge", 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PUSH-CLIENT] DisableAsync failed (ignored)");
            }
        }

        public async Task SetBadgeAsync(int count)
        {
            try { await _js.InvokeVoidAsync("fvnPush.setBadge", count); }
            catch { /* badge is cosmetic */ }
        }
    }
}
