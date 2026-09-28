using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text.Json;
using Microsoft.Win32;
using FVN_REGISTER.Contract.Dtos.Security;

namespace FVN_REGISTER.EndpointAgent;

public sealed class EndpointCollector
{
    public IReadOnlyList<EndpointSoftwareInventoryDto> CollectSoftware()
    {
        var result = new Dictionary<string, EndpointSoftwareInventoryDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            ReadUninstall(RegistryHive.LocalMachine, view, result);
            ReadUninstall(RegistryHive.CurrentUser, view, result);
        }
        return result.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<EndpointServiceInventoryDto> CollectServices()
    {
        return ServiceController.GetServices()
            .Select(service =>
            {
                string? startMode = null;
                string? binaryHash = null;
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services\\{service.ServiceName}");
                    startMode = key?.GetValue("Start") switch
                    {
                        2 => "Automatic",
                        3 => "Manual",
                        4 => "Disabled",
                        _ => "Unknown"
                    };
                    var imagePath = key?.GetValue("ImagePath")?.ToString();
                    if (!string.IsNullOrWhiteSpace(imagePath))
                    {
                        using var sha = SHA256.Create();
                        binaryHash = Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(imagePath))).ToLowerInvariant();
                    }
                }
                catch { }

                string state;
                try { state = service.Status.ToString(); } catch { state = "Unknown"; }
                return new EndpointServiceInventoryDto(service.ServiceName, service.DisplayName, state, startMode, binaryHash);
            })
            .OrderBy(x => x.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<EndpointAntivirusInventoryDto> CollectAntivirus()
    {
        var result = new List<EndpointAntivirusInventoryDto>();
        var defender = ReadDefenderStatus();
        if (defender is not null) result.Add(defender);
        return result;
    }

    private static EndpointAntivirusInventoryDto? ReadDefenderStatus()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-MpComputerStatus | Select-Object AMProductVersion,AMEngineVersion,AntivirusSignatureVersion,AntivirusSignatureLastUpdated,AntivirusEnabled,RealTimeProtectionEnabled,AMRunningMode,ProductStatus | ConvertTo-Json -Compress\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10000);
            if (string.IsNullOrWhiteSpace(output)) return null;
            using var document = JsonDocument.Parse(output.Trim());
            var root = document.RootElement;
            var enabled = GetBool(root, "AntivirusEnabled");
            var realtime = GetBool(root, "RealTimeProtectionEnabled");
            var status = enabled == true && realtime == true ? "Protected" : enabled == false || realtime == false ? "Unprotected" : "Unknown";
            var updated = GetDateTime(root, "AntivirusSignatureLastUpdated");
            return new EndpointAntivirusInventoryDto(
                "Microsoft Defender Antivirus",
                GetString(root, "AMProductVersion"),
                GetString(root, "AMEngineVersion"),
                GetString(root, "AntivirusSignatureVersion"),
                updated?.ToUniversalTime(),
                enabled,
                realtime,
                status,
                GetString(root, "AMRunningMode"),
                "WindowsDefender");
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;

    private static bool? GetBool(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    private static DateTime? GetDateTime(JsonElement root, string name)
    {
        var text = GetString(root, name);
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value) ? value : null;
    }

    private static void ReadUninstall(RegistryHive hive, RegistryView view, IDictionary<string, EndpointSoftwareInventoryDto> result)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
            if (uninstall == null) return;
            foreach (var subName in uninstall.GetSubKeyNames())
            {
                try
                {
                    using var key = uninstall.OpenSubKey(subName);
                    var name = key?.GetValue("DisplayName")?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var dto = new EndpointSoftwareInventoryDto(
                        name,
                        name,
                        key?.GetValue("Publisher")?.ToString(),
                        key?.GetValue("DisplayVersion")?.ToString(),
                        key?.GetValue("Architecture")?.ToString(),
                        ParseInstallDate(key?.GetValue("InstallDate")?.ToString()),
                        key?.GetValue("InstallLocation")?.ToString());
                    result.TryAdd(BuildKey(dto), dto);
                }
                catch { }
            }
        }
        catch { }
    }

    private static string BuildKey(EndpointSoftwareInventoryDto x) => $"{x.Name}|{x.Publisher}|{x.Version}";

    private static DateTime? ParseInstallDate(string? value)
    {
        return DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : null;
    }
}
