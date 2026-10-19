using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using FVN_REGISTER.Shared.Services.Help;

namespace FVN_REGISTER.Web.Services;

/// <summary>
/// Reads the Markdown user guide from the <c>Guide</c> folder next to the application (filled from
/// <c>MÔ HÌNH/03_TAI_LIEU_HE_THONG/02_HUONG_DAN_SU_DUNG</c> by FVN_REGISTER.Web.csproj on build and publish).
/// The text is cached per file and reloaded when the file's timestamp changes, so replacing the .md in the published
/// folder is enough: no rebuild and no restart.
/// </summary>
public sealed class FileGuideDocumentProvider : IGuideDocumentProvider
{
    private static readonly Regex SafeFileName = new(@"^[A-Za-z0-9_\-]+\.md$", RegexOptions.Compiled);

    private readonly string _root;
    private readonly ConcurrentDictionary<string, (DateTime LastWriteUtc, string Text)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FileGuideDocumentProvider(IWebHostEnvironment environment) =>
        _root = Path.Combine(environment.ContentRootPath, "Guide");

    public async Task<string?> GetMarkdownAsync(string fileName, CancellationToken cancellationToken = default)
    {
        // Bare file names only: no folders, no "..", so the caller can never read outside the Guide folder.
        if (!SafeFileName.IsMatch(fileName)) return null;

        var path = Path.Combine(_root, fileName);
        var info = new FileInfo(path);
        if (!info.Exists) return null;

        if (_cache.TryGetValue(fileName, out var cached) && cached.LastWriteUtc == info.LastWriteTimeUtc)
            return cached.Text;

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        _cache[fileName] = (info.LastWriteTimeUtc, text);
        return text;
    }
}
