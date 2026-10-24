using FVN_REGISTER.Contract.Help;

namespace FVN_REGISTER.API.Services;

/// <summary>Loads the Markdown user guide as searchable passages. The guide is read from disk, so editing the .md changes the answers without a rebuild.</summary>
public interface IGuideKnowledgeIndex
{
    Task<IReadOnlyList<GuideChunk>> GetChunksAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads <c>Guide/04_USER_GUIDE.md</c> (section-mapped, permission-filtered), <c>05_QUICK_GUIDE.md</c> and <c>06_FAQ.md</c> (general) from the
/// folders the API publishes (see FVN_REGISTER.API.csproj). The parsed index is cached and rebuilt when a file's timestamp or size changes.
/// </summary>
public sealed class GuideKnowledgeIndex : IGuideKnowledgeIndex
{
    private static readonly (string Name, bool General)[] Files =
    {
        (GuideSectionMap.MainGuideFile, false),
        ("05_QUICK_GUIDE.md", true),
        ("06_FAQ.md", true),
    };

    private readonly string[] _roots;
    private readonly ILogger<GuideKnowledgeIndex> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private (string Stamp, IReadOnlyList<GuideChunk> Chunks)? _cache;

    public GuideKnowledgeIndex(IWebHostEnvironment environment, IConfiguration configuration, ILogger<GuideKnowledgeIndex> logger)
    {
        _logger = logger;
        _roots = new[]
        {
            configuration["AIPlatform:Guide:Directory"] ?? string.Empty,
            Path.Combine(environment.ContentRootPath, "Guide"),
            Path.Combine(AppContext.BaseDirectory, "Guide"),
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }

    public async Task<IReadOnlyList<GuideChunk>> GetChunksAsync(CancellationToken cancellationToken = default)
    {
        var found = Files
            .Select(file => (file.Name, file.General, Info: Locate(file.Name)))
            .Where(file => file.Info is not null)
            .ToArray();
        var stamp = string.Join("|", found.Select(f => $"{f.Name}:{f.Info!.LastWriteTimeUtc.Ticks}:{f.Info.Length}"));
        if (_cache is { } cached && cached.Stamp == stamp) return cached.Chunks;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache is { } again && again.Stamp == stamp) return again.Chunks;
            var chunks = new List<GuideChunk>();
            foreach (var file in found)
            {
                var text = await File.ReadAllTextAsync(file.Info!.FullName, cancellationToken);
                chunks.AddRange(GuideChunker.Chunk(file.Name, text, allGeneral: file.General));
            }
            if (found.All(f => f.Name != GuideSectionMap.MainGuideFile))
                _logger.LogWarning("User guide {File} was not found in any of: {Roots}", GuideSectionMap.MainGuideFile, string.Join("; ", _roots));
            _cache = (stamp, chunks);
            return chunks;
        }
        finally { _gate.Release(); }
    }

    private FileInfo? Locate(string fileName)
    {
        foreach (var root in _roots)
        {
            var info = new FileInfo(Path.Combine(root, fileName));
            if (info.Exists) return info;
        }
        return null;
    }
}
