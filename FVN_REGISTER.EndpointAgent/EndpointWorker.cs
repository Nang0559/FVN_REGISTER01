using System.Net.Http;
using System.Net.Http.Json;
using FVN_REGISTER.Contract.Dtos.Security;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.EndpointAgent;

public sealed class EndpointWorker : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EndpointWorker> _logger;
    private readonly EndpointAgentOptions _options;
    private readonly EndpointCollector _collector;

    public EndpointWorker(IHttpClientFactory httpClientFactory, IOptions<EndpointAgentOptions> options, EndpointCollector collector, ILogger<EndpointWorker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _collector = collector;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.DeviceKey) || string.IsNullOrWhiteSpace(_options.ApiKeyProtected))
        {
            _logger.LogError("Endpoint Agent is not configured: DeviceKey and ApiKeyProtected are required.");
            return;
        }

        string apiKey;
        try { apiKey = WindowsSecretStore.Unprotect(_options.ApiKeyProtected); }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Endpoint Agent credential cannot be decrypted on this Windows machine.");
            return;
        }

        var delay = TimeSpan.FromMinutes(Math.Clamp(_options.IntervalMinutes, 5, 1440));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SendInventoryAsync(apiKey, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Endpoint inventory synchronization failed."); }
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task SendInventoryAsync(string apiKey, CancellationToken cancellationToken)
    {
        var request = new EndpointInventoryRequestDto(
            _options.DeviceKey.Trim(),
            Environment.MachineName,
            GetSerialNumber(),
            GetHardwareIdentity(),
            GetAgentInstallationId(),
            "Windows",
            Environment.OSVersion.VersionString,
            null,
            null,
            typeof(EndpointWorker).Assembly.GetName().Version?.ToString(),
            _collector.CollectSoftware(),
            _collector.CollectServices(),
            _collector.CollectAntivirus());

        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Add("X-FVN-Device-Api-Key", apiKey);
        using var response = await client.PostAsJsonAsync("api/security/endpoints/inventory", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Endpoint inventory synchronized. Software={SoftwareCount}, Services={ServiceCount}, Antivirus={AntivirusCount}.", request.Software.Count, request.Services.Count, request.Antivirus.Count);
    }

    private static string? GetSerialNumber()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("HARDWARE\\DESCRIPTION\\System\\BIOS");
            return key?.GetValue("SystemSerialNumber")?.ToString();
        }
        catch { return null; }
    }

    private static string? GetHardwareIdentity()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("HARDWARE\\DESCRIPTION\\System\\BIOS");
            var manufacturer = key?.GetValue("SystemManufacturer")?.ToString()?.Trim();
            var product = key?.GetValue("SystemProductName")?.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(manufacturer) || string.IsNullOrWhiteSpace(product) ? null : $"{manufacturer}|{product}";
        }
        catch { return null; }
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
