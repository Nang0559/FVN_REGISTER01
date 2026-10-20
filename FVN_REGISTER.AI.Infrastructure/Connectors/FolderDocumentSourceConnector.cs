using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Application.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace FVN_REGISTER.AI.Infrastructure.Connectors;
public sealed class FolderDocumentSourceConnector(IOptions<AIPlatformOptions> options, IDocumentIngestionService ingestion, ILogger<FolderDocumentSourceConnector> logger) : IDocumentSourceConnector
{
    public string ConnectorType => "Folder";
    public async Task<int> IngestAvailableAsync(CancellationToken cancellationToken = default)
    {
        var total = 0;
        foreach (var config in options.Value.FolderConnectors.Where(x => x.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(config.RootPath) || string.IsNullOrWhiteSpace(config.TenantCode) || string.IsNullOrWhiteSpace(config.ActorId)) { logger.LogWarning("Folder connector {Name} is incomplete and was skipped.", config.Name); continue; }
            var root = Path.GetFullPath(config.RootPath);
            if (!Directory.Exists(root)) { logger.LogWarning("Folder connector {Name} root {Root} does not exist.", config.Name, root); continue; }
            var enumeration = new EnumerationOptions { RecurseSubdirectories = config.IncludeSubdirectories, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
            foreach (var path in Directory.EnumerateFiles(root, "*", enumeration))
            {
                cancellationToken.ThrowIfCancellationRequested(); if (!await IsStableAsync(path, cancellationToken)) continue;
                try
                {
                    await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'); var retentionDays = Math.Clamp(config.RetentionDays, 1, 3650);
                    var receipt = await ingestion.IngestAsync(config.TenantCode, config.ActorId, Path.GetFileName(path), stream, cancellationToken, config.ModuleCode, config.SchemaCode, DocumentSourceTypes.Folder, $"{config.Name}:{relative}", DateTimeOffset.UtcNow.AddDays(retentionDays));
                    if (!receipt.Duplicate) total++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException) { logger.LogWarning(ex, "Folder connector {Name} could not ingest {Path}; the file will be retried on the next poll.", config.Name, path); }
            }
        }
        return total;
    }
    private static async Task<bool> IsStableAsync(string path, CancellationToken cancellationToken)
    {
        var first = new FileInfo(path); if (!first.Exists) return false; var length = first.Length; var modified = first.LastWriteTimeUtc;
        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken); var second = new FileInfo(path);
        return second.Exists && second.Length == length && second.LastWriteTimeUtc == modified;
    }
}
