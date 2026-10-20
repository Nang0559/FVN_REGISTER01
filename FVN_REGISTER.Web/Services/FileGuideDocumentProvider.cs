using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using FVN_REGISTER.Shared.Services.Help;

namespace FVN_REGISTER.Web.Services;

/// <summary>
/// Reads Markdown from the published Guide folder. Supports both published execution (AppContext.BaseDirectory)
/// and dotnet run, where ContentRootPath may be the project directory while content files live under bin/Debug.
/// </summary>
public sealed class FileGuideDocumentProvider : IGuideDocumentProvider
{
    private static readonly Regex SafeFileName = new(@"^[A-Za-z0-9_\-]+\.md$", RegexOptions.Compiled);

    private readonly string[] _roots;
    private readonly ConcurrentDictionary<string, (DateTime LastWriteUtc, string Text)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FileGuideDocumentProvider(IWebHostEnvironment environment)
    {
        _roots = new[]
        {
            Path.Combine(environment.ContentRootPath, "Guide"),
            Path.Combine(AppContext.BaseDirectory, "Guide"),
            Path.Combine(environment.WebRootPath ?? string.Empty, "Guide")
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }

    public async Task<string?> GetMarkdownAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !SafeFileName.IsMatch(fileName))
            return null;

        foreach (var root in _roots)
        {
            var path = Path.Combine(root, fileName);
            var info = new FileInfo(path);
            if (!info.Exists) continue;

            if (_cache.TryGetValue(path, out var cached) && cached.LastWriteUtc == info.LastWriteTimeUtc)
                return cached.Text;

            var text = await File.ReadAllTextAsync(path, cancellationToken);
            _cache[path] = (info.LastWriteTimeUtc, text);
            return text;
        }

        return null;
    }
}
