using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.SystemUpdate;
using FVN_REGISTER.Contract.Dtos.SystemUpdate;
using FVN_REGISTER.Contract.Utils;
using Microsoft.Extensions.Logging;

namespace FVN_REGISTER.Infrastructure.Services.SystemUpdate;

public sealed class SystemUpdateService : ISystemUpdateService
{
    private const int MaxManifestBytes = 5 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SystemUpdateOptions _options;
    private readonly ILogger<SystemUpdateService> _logger;

    public SystemUpdateService(SystemUpdateOptions options, ILogger<SystemUpdateService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<ServiceResult<SystemUpdateOverviewDto>> GetOverviewAsync(CancellationToken ct = default)
    {
        var opt = _options;
        var dto = new SystemUpdateOverviewDto { Enabled = opt.IsConfigured, MaxPackageBytes = (long)Math.Max(1, opt.MaxPackageMb) * 1024 * 1024 };
        if (!opt.IsConfigured) return ServiceResult<SystemUpdateOverviewDto>.Ok(dto);
        dto.Current = await ReadJsonAsync<PatchStatusDto>(Path.Combine(opt.StatusPath, "status.json"), ct);
        var history = await ReadJsonAsync<List<PatchStatusDto>>(Path.Combine(opt.StatusPath, "history.json"), ct);
        if (history != null) dto.History = history.OrderByDescending(x => x.StartedAt).Take(20).ToList();
        if (Directory.Exists(opt.InboxPath)) dto.PendingPackages = Directory.EnumerateFiles(opt.InboxPath, "patch-*.zip").Select(Path.GetFileName).Where(x => x != null).Select(x => x!).OrderBy(x => x).ToList();
        return ServiceResult<SystemUpdateOverviewDto>.Ok(dto);
    }

    public async Task<ServiceResult<PatchUploadResultDto>> SaveUploadAsync(Stream package, string originalFileName, int userId, string userName, CancellationToken ct = default)
    {
        var opt = _options;
        if (!opt.IsConfigured) return ServiceResult<PatchUploadResultDto>.Fail("Tính năng cập nhật hệ thống chưa được bật.");
        Directory.CreateDirectory(opt.InboxPath);
        Directory.CreateDirectory(opt.StatusPath);
        if (Directory.EnumerateFiles(opt.InboxPath, "patch-*.zip").Any() || Directory.EnumerateFiles(opt.InboxPath, "patch-*.part").Any()) return ServiceResult<PatchUploadResultDto>.Fail("Đang có gói vá chờ xử lý. Hãy đợi cập nhật hiện tại hoàn tất.");
        var current = await ReadJsonAsync<PatchStatusDto>(Path.Combine(opt.StatusPath, "status.json"), ct);
        if (string.Equals(current?.State, "Running", StringComparison.OrdinalIgnoreCase)) return ServiceResult<PatchUploadResultDto>.Fail("Hệ thống đang được cập nhật.");

        var id = $"patch-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..29];
        var partPath = Path.Combine(opt.InboxPath, id + ".part");
        var zipPath = Path.Combine(opt.InboxPath, id + ".zip");
        var metaPath = Path.Combine(opt.InboxPath, id + ".meta.json");
        var maxBytes = (long)Math.Max(1, opt.MaxPackageMb) * 1024 * 1024;
        try
        {
            long total = 0;
            await using (var output = new FileStream(partPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await package.ReadAsync(buffer, ct)) > 0)
                {
                    total += read;
                    if (total > maxBytes) return ServiceResult<PatchUploadResultDto>.Fail($"Gói vá vượt quá giới hạn {opt.MaxPackageMb} MB.");
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }
            var verify = VerifyPackage(partPath, opt.PublicKeyPath, out var version);
            if (!verify.IsSuccess)
            {
                _logger.LogWarning("[SYSTEM-UPDATE] Rejected package from UserId={UserId}: {Reason}", userId, verify.Message);
                return ServiceResult<PatchUploadResultDto>.Fail(verify.Message ?? "Gói vá không hợp lệ.");
            }
            await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(new { uploadedBy = $"{userName} (#{userId})", uploadedAt = DateTime.Now.ToString("o"), originalName = Path.GetFileName(originalFileName), version }), ct);
            File.Move(partPath, zipPath);
            return ServiceResult<PatchUploadResultDto>.Ok(new PatchUploadResultDto { FileName = id + ".zip", Version = version });
        }
        finally
        {
            TryDelete(partPath);
            if (!File.Exists(zipPath)) TryDelete(metaPath);
        }
    }

    private static ServiceResult VerifyPackage(string zipPath, string publicKeyPath, out string version)
    {
        version = string.Empty;
        try
        {
            if (!File.Exists(publicKeyPath)) return ServiceResult.Fail("Chưa cấu hình khóa công khai để xác thực gói vá.");
            using var zip = ZipFile.OpenRead(zipPath);
            var manifestEntry = zip.GetEntry("manifest.json");
            var sigEntry = zip.GetEntry("manifest.sig");
            if (manifestEntry == null || sigEntry == null) return ServiceResult.Fail("Gói vá thiếu manifest.json hoặc manifest.sig.");
            if (manifestEntry.Length > MaxManifestBytes || sigEntry.Length > 4096) return ServiceResult.Fail("Manifest không hợp lệ (quá lớn).");
            byte[] manifest;
            using (var ms = new MemoryStream()) { using var s = manifestEntry.Open(); s.CopyTo(ms); manifest = ms.ToArray(); }
            using var reader = new StreamReader(sigEntry.Open());
            var sigText = reader.ReadToEnd().Trim();
            using var rsa = LoadPublicKey(File.ReadAllText(publicKeyPath));
            if (!rsa.VerifyData(manifest, Convert.FromBase64String(sigText), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) return ServiceResult.Fail("Chữ ký số của gói vá không hợp lệ.");
            using var doc = JsonDocument.Parse(manifest);
            version = doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() ?? string.Empty : string.Empty;
            return string.IsNullOrWhiteSpace(version) ? ServiceResult.Fail("Manifest không có version.") : ServiceResult.Ok();
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or JsonException or CryptographicException or XmlException) { return ServiceResult.Fail("Gói vá không đọc được hoặc bị hỏng."); }
    }

    private static RSA LoadPublicKey(string xml)
    {
        var root = XElement.Parse(xml, LoadOptions.PreserveWhitespace);
        static byte[] ReadBase64(XElement root, string name)
        {
            var value = root.Descendants().FirstOrDefault(x => x.Name.LocalName == name)?.Value;
            if (string.IsNullOrWhiteSpace(value)) throw new CryptographicException($"RSA public key missing {name}.");
            return Convert.FromBase64String(value);
        }
        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { Modulus = ReadBase64(root, "Modulus"), Exponent = ReadBase64(root, "Exponent") });
        return rsa;
    }

    private static async Task<T?> ReadJsonAsync<T>(string path, CancellationToken ct) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            var text = await File.ReadAllTextAsync(path, ct);
            return string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<T>(text, Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}