using System.Net;
using System.Management;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FVN_REGISTER.Contract.Dtos.Security;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.EndpointAgent;

public sealed class EndpointWorker : BackgroundService
{
    private sealed record RotationResult(string DeviceKey, string ApiKey, DateTimeOffset ExpiresAtUtc);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EndpointWorker> _logger;
    private readonly EndpointAgentOptions _options;
    private readonly EndpointCollector _collector;
    private readonly IHostApplicationLifetime _lifetime;

    public EndpointWorker(IHttpClientFactory httpClientFactory, IOptions<EndpointAgentOptions> options, EndpointCollector collector, ILogger<EndpointWorker> logger, IHostApplicationLifetime lifetime)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _collector = collector;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Uri.TryCreate(_options.ApiBaseUrl?.Trim(), UriKind.Absolute, out var apiUri) || !string.Equals(apiUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            FailConfiguration("ApiBaseUrl is required and must use HTTPS.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.DeviceKey) || string.IsNullOrWhiteSpace(_options.ApiKeyProtected))
        {
            FailConfiguration("DeviceKey and ApiKeyProtected are required.");
            return;
        }

        string apiKey;
        try { apiKey = WindowsSecretStore.Unprotect(_options.ApiKeyProtected); }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Endpoint Agent credential cannot be decrypted on this Windows machine.");
            FailConfiguration("ApiKeyProtected cannot be decrypted on this Windows machine.");
            return;
        }

        var delay = TimeSpan.FromMinutes(Math.Clamp(_options.IntervalMinutes, 5, 1440));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { apiKey = await SendInventoryAsync(apiKey, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Endpoint inventory synchronization failed."); }
            await Task.Delay(delay, stoppingToken);
        }
    }

    private void FailConfiguration(string message)
    {
        _logger.LogCritical("Endpoint Agent configuration invalid: {Message}", message);
        Environment.ExitCode = 2;
        _lifetime.StopApplication();
    }

    private async Task<string> SendInventoryAsync(string apiKey, CancellationToken cancellationToken)
    {
        var request = new EndpointInventoryRequestDto(
            _options.DeviceKey.Trim(), Environment.MachineName, GetSerialNumber(), GetHardwareIdentity(), GetAgentInstallationId(),
            "Windows", Environment.OSVersion.VersionString, null, null, typeof(EndpointWorker).Assembly.GetName().Version?.ToString(),
            _collector.CollectSoftware(), _collector.CollectServices(), _collector.CollectAntivirus());

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Remove("X-FVN-Device-Api-Key");
            client.DefaultRequestHeaders.Add("X-FVN-Device-Api-Key", apiKey);
            using var response = await client.PostAsJsonAsync("api/security/endpoints/inventory", request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                var rotated = await RotateCredentialAsync(apiKey, cancellationToken);
                if (rotated == null)
                {
                    _logger.LogCritical("Endpoint Agent received HTTP 401 and credential rotation failed. Provision a new credential from Security Center.");
                    response.EnsureSuccessStatusCode();
                }
                apiKey = rotated!.ApiKey;
                PersistProtectedSecret(apiKey);
                _logger.LogWarning("Endpoint Agent credential was rotated after HTTP 401.");
                continue;
            }

            response.EnsureSuccessStatusCode();
            if (TryGetExpiry(response, out var expiresAtUtc) && expiresAtUtc <= DateTimeOffset.UtcNow.AddDays(Math.Max(1, _options.CredentialRotationLeadDays)))
            {
                var rotated = await RotateCredentialAsync(apiKey, cancellationToken);
                if (rotated != null)
                {
                    apiKey = rotated.ApiKey;
                    PersistProtectedSecret(apiKey);
                    _logger.LogInformation("Endpoint Agent credential rotated proactively; new expiry is {ExpiresAtUtc}.", rotated.ExpiresAtUtc);
                }
                else _logger.LogWarning("Endpoint Agent credential is close to expiry but proactive rotation was rejected.");
            }

            _logger.LogInformation("Endpoint inventory synchronized. Software={SoftwareCount}, Services={ServiceCount}, Antivirus={AntivirusCount}.", request.Software.Count, request.Services.Count, request.Antivirus.Count);
            return apiKey;
        }
        return apiKey;
    }

    private async Task<RotationResult?> RotateCredentialAsync(string currentApiKey, CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Remove("X-FVN-Device-Api-Key");
            client.DefaultRequestHeaders.Add("X-FVN-Device-Api-Key", currentApiKey);
            using var response = await client.PostAsync("api/security/endpoints/credentials/agent-rotate", content: null, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!document.RootElement.TryGetProperty("data", out var data)) return null;
            var apiKey = data.TryGetProperty("apiKey", out var key) ? key.GetString() : null;
            var deviceKey = data.TryGetProperty("deviceKey", out var device) ? device.GetString() : null;
            var expires = data.TryGetProperty("expiresAtUtc", out var expiry) && expiry.TryGetDateTimeOffset(out var expiresAtUtc) ? expiresAtUtc : (DateTimeOffset?)null;
            return string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deviceKey) || !expires.HasValue ? null : new RotationResult(deviceKey, apiKey, expires.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Endpoint Agent credential rotation request failed.");
            return null;
        }
    }

    private static bool TryGetExpiry(HttpResponseMessage response, out DateTimeOffset expiresAtUtc)
    {
        if (response.Headers.TryGetValues("X-FVN-ApiKey-Expires-Utc", out var values) && DateTimeOffset.TryParse(values.FirstOrDefault(), out expiresAtUtc)) return true;
        expiresAtUtc = default;
        return false;
    }

    private static void PersistProtectedSecret(string apiKey)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return;
        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? new JsonObject();
        var section = root["FVNEndpointAgent"]?.AsObject() ?? new JsonObject();
        section["ApiKeyProtected"] = WindowsSecretStore.Protect(apiKey);
        root["FVNEndpointAgent"] = section;
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string? GetSerialNumber() => ReadWmiValue("Win32_BIOS", "SerialNumber");

    private static string? GetHardwareIdentity()
    {
        var uuid = ReadWmiValue("Win32_ComputerSystemProduct", "UUID");
        if (string.IsNullOrWhiteSpace(uuid)) return null;
        var normalized = uuid.Trim();
        if (normalized.All(c => c == '0' || c == '-' || c == ' ')) return null;
        return normalized;
    }

    private static string? ReadWmiValue(string className, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
            foreach (ManagementObject item in searcher.Get())
            {
                var value = item[propertyName]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        catch { }
        return null;
    }

    private static string GetAgentInstallationId()
    {
        const string path = @"SOFTWARE\FVN_REGISTER\EndpointAgent";
        const string valueName = "InstallationId";
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(path, writable: true);
            var existing = key?.GetValue(valueName)?.ToString();
            if (!string.IsNullOrWhiteSpace(existing)) return existing;
            var id = Guid.NewGuid().ToString("N");
            key?.SetValue(valueName, id, Microsoft.Win32.RegistryValueKind.String);
            return id;
        }
        catch { return Environment.MachineName; }
    }
}
