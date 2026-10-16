using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Notifications;
using FVN_REGISTER.Contract.Dtos.Notifications;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;

namespace FVN_REGISTER.Infrastructure.Services.Notifications;

public sealed class WebPushService : IWebPushService
{
    // The server POSTs to the endpoint the client supplies, so only well-known
    // browser push services are accepted (prevents SSRF to internal hosts).
    private static readonly string[] AllowedHostSuffixes =
    {
        "fcm.googleapis.com",          // Chrome / Edge / Android
        "push.services.mozilla.com",   // Firefox
        "push.apple.com",              // Safari / iOS (web.push.apple.com)
        "notify.windows.com"           // legacy Edge (WNS)
    };

    private static readonly WebPushClient Client = new();

    private readonly FVNWEBAPPContext _db;
    private readonly IOptionsMonitor<WebPushOptions> _options;
    private readonly ILogger<WebPushService> _logger;

    public WebPushService(
        FVNWEBAPPContext db,
        IOptionsMonitor<WebPushOptions> options,
        ILogger<WebPushService> logger)
    {
        _db = db;
        _options = options;
        _logger = logger;
    }

    public string? PublicKey =>
        _options.CurrentValue.IsConfigured ? _options.CurrentValue.PublicKey : null;

    public async Task<ServiceResult> SubscribeAsync(int userId, PushSubscribeRequestDto dto, CancellationToken ct = default)
    {
        if (!_options.CurrentValue.IsConfigured)
            return ServiceResult.Fail("Web Push chưa được cấu hình.");

        if (dto == null
            || string.IsNullOrWhiteSpace(dto.Endpoint) || dto.Endpoint.Length > 1000
            || string.IsNullOrWhiteSpace(dto.P256dh) || dto.P256dh.Length > 200
            || string.IsNullOrWhiteSpace(dto.Auth) || dto.Auth.Length > 100)
            return ServiceResult.Fail("Thông tin đăng ký thiết bị không hợp lệ.");

        if (!IsAllowedEndpoint(dto.Endpoint))
            return ServiceResult.Fail("Dịch vụ push của trình duyệt này không được hỗ trợ.");

        var hash = Hash(dto.Endpoint);
        var userAgent = dto.UserAgent is { Length: > 300 } ? dto.UserAgent[..300] : dto.UserAgent;
        var now = DateTime.Now;

        var row = await _db.PushSubscriptions.FirstOrDefaultAsync(x => x.EndpointHash == hash, ct);
        if (row == null)
        {
            _db.PushSubscriptions.Add(new F03PushSubscription
            {
                UserId = userId,
                EndpointHash = hash,
                Endpoint = dto.Endpoint,
                P256dh = dto.P256dh,
                Auth = dto.Auth,
                UserAgent = userAgent,
                LastUsedAt = now,
                CreatedBy = userId
            });
        }
        else
        {
            // Same device, possibly a different user: always bind to the latest sign-in.
            row.UserId = userId;
            row.Endpoint = dto.Endpoint;
            row.P256dh = dto.P256dh;
            row.Auth = dto.Auth;
            row.UserAgent = userAgent;
            row.LastUsedAt = now;
            row.IsActive = true;
            row.ModifiedBy = userId;
            row.ModifiedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UnsubscribeAsync(int userId, string endpoint, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return ServiceResult.Ok();

        var hash = Hash(endpoint);
        var rows = await _db.PushSubscriptions
            .Where(x => x.EndpointHash == hash && x.UserId == userId)
            .ToListAsync(ct);

        if (rows.Count > 0)
        {
            _db.PushSubscriptions.RemoveRange(rows);
            await _db.SaveChangesAsync(ct);
        }

        return ServiceResult.Ok();
    }

    public async Task NotifyNewAsync(int userId, int unreadCount, string? title, string? body, string? url, CancellationToken ct = default)
    {
        var opt = _options.CurrentValue;
        if (!opt.IsConfigured) return;

        var subs = await _db.PushSubscriptions
            .Where(x => x.UserId == userId && x.IsActive != false)
            .ToListAsync(ct);
        if (subs.Count == 0) return;

        // Default is privacy-friendly: the service worker renders "N unread" in the
        // device language. Title/body are only sent when ShowContent is enabled.
        var payload = JsonSerializer.Serialize(new
        {
            title = opt.ShowContent ? title : null,
            body = opt.ShowContent ? body : null,
            url = SafeRelativeUrl(url),
            badge = Math.Max(0, unreadCount)
        });

        var vapid = new VapidDetails(opt.Subject, opt.PublicKey, opt.PrivateKey);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(2, opt.TimeoutSeconds)));

        var dead = new List<F03PushSubscription>();
        await Task.WhenAll(subs.Select(async s =>
        {
            try
            {
                await Client.SendNotificationAsync(
                    new PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, vapid, cts.Token);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                lock (dead) dead.Add(s);   // browser revoked / uninstalled the PWA
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Web Push failed. UserId={UserId}, SubscriptionId={Id}", userId, s.Id);
            }
        }));

        if (dead.Count > 0)
        {
            _db.PushSubscriptions.RemoveRange(dead);
            await _db.SaveChangesAsync(ct);
        }
    }

    internal static bool IsAllowedEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var host = uri.Host.ToLowerInvariant();
        return AllowedHostSuffixes.Any(s => host == s || host.EndsWith("." + s, StringComparison.Ordinal));
    }

    private static string SafeRelativeUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//") ? url : "/";

    private static string Hash(string endpoint) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint))).ToLowerInvariant();
}
