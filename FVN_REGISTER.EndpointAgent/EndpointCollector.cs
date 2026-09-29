using System.Diagnostics;
using System.ServiceProcess;
using System.Text.Json;

namespace FVN_REGISTER.EndpointAgent;

// ...

private static IReadOnlyList<EndpointAntivirusInventoryDto> ReadSecurityCenterProducts()
{
    try
    {
        const string command = "Get-CimInstance -Namespace root/SecurityCenter2 -ClassName AntiVirusProduct | Select-Object displayName,productState,pathToSignedProductExe | ConvertTo-Json -Compress";
        var output = RunPowerShell(command, TimeSpan.FromSeconds(10));
        if (string.IsNullOrWhiteSpace(output)) return [];
        using var document = JsonDocument.Parse(output.Trim());
        IEnumerable<JsonElement> elements = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray()
            : [document.RootElement];
        return elements
            .Select(x =>
            {
                var name = GetString(x, "displayName");
                if (string.IsNullOrWhiteSpace(name) || name.Contains("Microsoft Defender", StringComparison.OrdinalIgnoreCase)) return null;
                var state = GetString(x, "productState");
                return new EndpointAntivirusInventoryDto(name, null, null, null, null, null, null, string.IsNullOrWhiteSpace(state) ? "Detected" : "Detected", null, "WindowsSecurityCenter");
            })
            .Where(x => x is not null)
            .Cast<EndpointAntivirusInventoryDto>()
            .ToArray();
    }
    catch { return []; }
}

private static string RunPowerShell(string command, TimeSpan timeout)
{
    using var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command.Replace("\\\"", "\\\\\"")}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }
    };
    process.Start();
    var outputTask = process.StandardOutput.ReadToEndAsync();
    try
    {
        outputTask.WaitAsync(timeout).GetAwaiter().GetResult();
        process.WaitForExit((int)timeout.TotalMilliseconds);
        return outputTask.GetAwaiter().GetResult();
    }
    catch (global::System.TimeoutException)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        return string.Empty;
    }
}

private static string? GetString(JsonElement root, string name)
    => root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;

private static bool? GetBool(JsonElement root, string name)
    => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;