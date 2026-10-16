using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FVN_REGISTER.Contract.Dtos.Language;

namespace FVN_REGISTER.Infrastructure.Services.Language;

/// <summary>
/// Builds the snapshot of the UI text that the clients load at runtime, from the <c>{lang}.{module}.json</c> files that the
/// Language Center edits. The version only changes when a file changes, so a client that already holds it downloads nothing.
///
/// The version is derived from file names, sizes and write times (no file is opened to compute it); the content is only parsed
/// again when that stamp changes. Files are replaced atomically by the Language Center, so a reader never sees half a file.
/// </summary>
public sealed class LocalizationRuntimeCatalog
{
    private readonly object _gate = new();
    private string _stamp = string.Empty;
    private string _root = string.Empty;
    private LocalizationRuntimeSnapshotDto _snapshot = new();

    public LocalizationRuntimeSnapshotDto Get(string root, long? sinceVersion)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return new LocalizationRuntimeSnapshotDto { Version = 0, Unchanged = sinceVersion == 0 };

        var files = Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            .Where(IsCatalogFile)
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

        var stamp = BuildStamp(files);
        lock (_gate)
        {
            if (!string.Equals(_stamp, stamp, StringComparison.Ordinal) || !string.Equals(_root, root, StringComparison.Ordinal))
            {
                _snapshot = Build(files, VersionOf(stamp));
                _stamp = stamp;
                _root = root;
            }

            var current = _snapshot;
            if (sinceVersion.HasValue && sinceVersion.Value == current.Version)
                return new LocalizationRuntimeSnapshotDto { Version = current.Version, Unchanged = true };

            return current;
        }
    }

    private static bool IsCatalogFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith("vi.", StringComparison.OrdinalIgnoreCase) || name.StartsWith("ja.", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildStamp(IEnumerable<FileInfo> files)
    {
        var sb = new StringBuilder();
        foreach (var f in files)
            sb.Append(f.Name).Append('|').Append(f.Length).Append('|').Append(f.LastWriteTimeUtc.Ticks).Append(';');
        return sb.ToString();
    }

    /// <summary>A positive 56-bit number derived from the stamp (fits JSON numbers and a long).</summary>
    private static long VersionOf(string stamp)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stamp));
        long value = 0;
        for (var i = 0; i < 7; i++) value = (value << 8) | hash[i];
        return value == 0 ? 1 : value;   // 0 is reserved for "no catalog folder"
    }

    private static LocalizationRuntimeSnapshotDto Build(IEnumerable<FileInfo> files, long version)
    {
        var snapshot = new LocalizationRuntimeSnapshotDto { Version = version };
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file.Name);
            var target = name.StartsWith("ja.", StringComparison.OrdinalIgnoreCase) ? snapshot.Ja : snapshot.Vi;

            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String) continue;
                var value = property.Value.GetString();
                if (!string.IsNullOrEmpty(value))
                    target[property.Name] = value;
            }
        }
        return snapshot;
    }
}
