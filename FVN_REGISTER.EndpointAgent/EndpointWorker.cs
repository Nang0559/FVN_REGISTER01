using System.Net;
using System.Management;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Security;
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

        string apiKey;
        if (string.IsNullOrWhiteSpace(_options.DeviceKey) || string.IsNullOrWhiteSpace(_options.ApiKeyProtected))
        {
            var bootstrapPath = Path.IsPathRooted(_options.BootstrapPath) ? _options.BootstrapPath : Path.Combine(AppContext.BaseDirectory, _options.BootstrapPath ?? string.Empty);
            if (!File.Exists(bootstrapPath)) { FailConfiguration("Bootstrap file or existing DeviceKey/ApiKeyProtected is required."); return; }
            try
            {
                var bootstrap = JsonSerializer.Deserialize<LanscopeBootstrap>(await File.ReadAllTextAsync(bootstrapPath, stoppingToken))
                    ?? throw new InvalidOperationException("Bootstrap file không hợp lệ.");
                if (!Uri.TryCreate(bootstrap.ApiBaseUrl, UriKind.Absolute, out var bootstrapUri) || bootstrapUri.Scheme != Uri.UriSchemeHttps)
                    throw new InvalidOperationException("Bootstrap ApiBaseUrl phải dùng HTTPS.");
                _options.LanscopeOrganizationalUnit = bootstrap.OrganizationalUnit;
                _options.LanscopeGroup = bootstrap.Group;
                var enrolled = await EnrollWithRetryAsync(bootstrap, stoppingToken);
                _options.DeviceKey = enrolled.DeviceKey;
                apiKey = enrolled.ApiKey;
                PersistConfiguration(enrolled.DeviceKey, enrolled.ApiKey, bootstrap.ApiBaseUrl, bootstrap.ClientId);
                SecureDeleteFile(bootstrapPath);
                _logger.LogInformation("Endpoint Agent enrolled successfully through LANSCOPE bootstrap.");
            }
            catch (Exception ex) { _logger.LogCritical(ex, "LANSCOPE bootstrap enrollment failed."); FailConfiguration("Bootstrap enrollment failed."); return; }
        }
        else
        {
            try { apiKey = WindowsSecretStore.Unprotect(_options.ApiKeyProtected); }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Endpoint Agent credential cannot be decrypted on this Windows machine.");
            FailConfiguration("ApiKeyProtected cannot be decrypted on this Windows machine.");
            return;
        }
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
            _collector.CollectSoftware(), _collector.CollectServices(), _collector.CollectAntivirus(),
            GetLanscopeClientId(), GetLocalIp(), GetMacAddress(), GetLoggedOnUser(), GetLoggedOnDomain(),
            GetOrganizationalUnit(), GetLanscopeGroup(), GetManufacturer(), GetModel());

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
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    private sealed record LanscopeBootstrap(int DeploymentId, int TargetId, string EnrollmentToken, string ApiBaseUrl, string? ClientId, string? OrganizationalUnit, string? Group);
    private sealed record EnrollmentEnvelope<T>(bool Success, T? Data, string? Message);

    private async Task<LanscopeEnrollmentResponseDto> EnrollAsync(LanscopeBootstrap bootstrap, CancellationToken ct)
    {
        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(bootstrap.ApiBaseUrl.TrimEnd('/') + "/");
        var request = new LanscopeEnrollmentRequestDto(
            bootstrap.DeploymentId, bootstrap.TargetId, bootstrap.EnrollmentToken, bootstrap.ClientId,
            Environment.MachineName, GetLocalIp(), GetMacAddress(), GetSerialNumber(), GetLoggedOnUser(),
            GetLoggedOnDomain(), GetOrganizationalUnit(), GetLanscopeGroup(), Environment.OSVersion.VersionString,
            GetManufacturer(), GetModel(), GetHardwareIdentity(), GetAgentInstallationId(),
            typeof(EndpointWorker).Assembly.GetName().Version?.ToString());
        using var response = await client.PostAsJsonAsync("api/security/endpoints/lanscope/enroll", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new EnrollmentHttpException(response.StatusCode, body);
        }
        var payload = await response.Content.ReadFromJsonAsync<EnrollmentEnvelope<LanscopeEnrollmentResponseDto>>(cancellationToken: ct);
        return payload?.Data ?? throw new InvalidOperationException("Enrollment API trả về response không hợp lệ.");
    }

    private static void PersistConfiguration(string deviceKey, string apiKey, string apiBaseUrl, string? lanscopeClientId)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? new JsonObject();
        var section = root["FVNEndpointAgent"]?.AsObject() ?? new JsonObject();
        section["ApiBaseUrl"] = apiBaseUrl;
        section["DeviceKey"] = deviceKey;
        section["ApiKeyProtected"] = WindowsSecretStore.Protect(apiKey);
        section["BootstrapPath"] = string.Empty;
        section["LanscopeClientId"] = lanscopeClientId ?? string.Empty;
        root["FVNEndpointAgent"] = section;
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, json, new System.Text.UTF8Encoding(false)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) try { File.Delete(temp); } catch { } }
    }

    private static void SecureDeleteFile(string path)
    {
        if (!File.Exists(path)) return;
        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
    }

    private static string? GetManufacturer() => ReadWmiValue("Win32_ComputerSystem", "Manufacturer");
    private static string? GetLoggedOnUser() => ReadWmiValue("Win32_ComputerSystem", "UserName");
    private static string? GetLoggedOnDomain()
    {
        var user = GetLoggedOnUser();
        if (string.IsNullOrWhiteSpace(user)) return null;
        var slash = user.IndexOf('\\');
        return slash > 0 ? user[..slash] : null;
    }
    private static string? GetModel() => ReadWmiValue("Win32_ComputerSystem", "Model");
    private string? GetOrganizationalUnit() => _options.LanscopeOrganizationalUnit;
    private string? GetLanscopeGroup() => _options.LanscopeGroup;
    private string? GetLanscopeClientId() => !string.IsNullOrWhiteSpace(_options.LanscopeClientId) ? _options.LanscopeClientId.Trim() : Environment.GetEnvironmentVariable("FVN_LANSCOPE_CLIENT_ID");
    private static string? GetLocalIp() { try { using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,System.Net.Sockets.SocketType.Dgram,System.Net.Sockets.ProtocolType.Udp); socket.Connect("1.1.1.1",53); return (socket.LocalEndPoint as System.Net.IPEndPoint)?.Address.ToString(); } catch { return null; } }
    private static string? GetMacAddress() { try { return System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Where(x=>x.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up&&x.NetworkInterfaceType!=System.Net.NetworkInformation.NetworkInterfaceType.Loopback).OrderByDescending(x=>x.Speed).FirstOrDefault()?.GetPhysicalAddress().ToString(); } catch { return null; } }

    private static string? GetSerialNumber()
    {
        var serial = ReadWmiValue("Win32_BIOS", "SerialNumber");
        return LanscopeSecurityPolicy.IsMeaningfulSerial(serial) ? serial : null;
    }

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

    private async Task<LanscopeEnrollmentResponseDto> EnrollWithRetryAsync(LanscopeBootstrap bootstrap, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { return await EnrollAsync(bootstrap, ct); }
            catch (EnrollmentHttpException ex) when ((int)ex.StatusCode >= 400 && (int)ex.StatusCode < 500)
            {
                _logger.LogError("LANSCOPE enrollment stopped on HTTP {StatusCode}. Response={Response}", (int)ex.StatusCode, ex.ResponseBody);
                throw;
            }
            catch (EnrollmentHttpException ex) when ((int)ex.StatusCode >= 500)
            {
                _logger.LogWarning("LANSCOPE enrollment server failure HTTP {StatusCode} on attempt {Attempt}; retrying in 60 seconds.", (int)ex.StatusCode, attempt);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "LANSCOPE enrollment network failure on attempt {Attempt}; retrying in 60 seconds.", attempt);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("LANSCOPE enrollment request timed out on attempt {Attempt}; retrying in 60 seconds.", attempt);
            }
            await Task.Delay(TimeSpan.FromSeconds(60), ct);
        }
    }

    private sealed class EnrollmentHttpException : Exception
    {
        public EnrollmentHttpException(HttpStatusCode statusCode, string responseBody) : base($"Enrollment API returned {(int)statusCode}.")
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
        public HttpStatusCode StatusCode { get; }
        public string ResponseBody { get; }
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
