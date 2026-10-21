using System.Security.Cryptography;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.AI.Infrastructure.Storage;

public sealed class FileSystemOriginalDocumentStore : IOriginalDocumentStore
{
    private readonly string root;
    private readonly ILogger<FileSystemOriginalDocumentStore> logger;

    public FileSystemOriginalDocumentStore(
        IOptions<AIPlatformOptions> options,
        ILogger<FileSystemOriginalDocumentStore> logger)
    {
        this.logger = logger;
        var configuredRoot = options.Value.StorageRoot?.Trim();

        if (string.IsNullOrWhiteSpace(configuredRoot))
            throw new InvalidOperationException("AIPlatform:StorageRoot must be configured.");

        if (!Path.IsPathFullyQualified(configuredRoot))
        {
            throw new InvalidOperationException(
                $"AIPlatform:StorageRoot must be an absolute path. Current value: '{configuredRoot}'.");
        }

        root = Path.GetFullPath(configuredRoot);
        Directory.CreateDirectory(root);

        this.logger.LogInformation(
            "AI original-document storage initialized. StorageRoot={StorageRoot}; BaseDirectory={BaseDirectory}; CurrentDirectory={CurrentDirectory}",
            root,
            AppContext.BaseDirectory,
            Environment.CurrentDirectory);
    }

    public async Task<StoredDocument> SaveAsync(Guid id, Stream content, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Document id required.", nameof(id));

        var staging = Path.Combine(root, ".staging");
        Directory.CreateDirectory(staging);

        var temp = Path.Combine(staging, $"{Guid.NewGuid():N}.upload");
        var key = Path.Combine("objects", id.ToString("N")[..2], $"{id:N}.blob").Replace('\\', '/');
        var final = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(final)!);

        try
        {
            long length = 0;
            string hash;

            await using (var output = new FileStream(
                temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                int n;

                while ((n = await content.ReadAsync(buffer.AsMemory(), ct)) > 0)
                {
                    length += n;
                    sha.AppendData(buffer, 0, n);
                    await output.WriteAsync(buffer.AsMemory(0, n), ct);
                }

                await output.FlushAsync(ct);
                hash = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
            }

            File.Move(temp, final, false);
            logger.LogDebug(
                "Stored AI original document. StorageKey={StorageKey}; ResolvedPath={ResolvedPath}; Length={Length}",
                key, final, length);

            return new StoredDocument(key, length, hash);
        }
        catch
        {
            if (File.Exists(temp))
                File.Delete(temp);

            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = Resolve(key);
        if (!File.Exists(path))
        {
            logger.LogError(
                "AI original document blob is missing. StorageKey={StorageKey}; ResolvedPath={ResolvedPath}; StorageRoot={StorageRoot}; BaseDirectory={BaseDirectory}; CurrentDirectory={CurrentDirectory}",
                key, path, root, AppContext.BaseDirectory, Environment.CurrentDirectory);

            throw new FileNotFoundException(
                $"Stored AI document not found. StorageKey='{key}', ResolvedPath='{path}', StorageRoot='{root}'.",
                path);
        }

        Stream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = Resolve(key);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Path.IsPathRooted(key))
            throw new InvalidOperationException("Invalid storage key.");

        var path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage key escaped storage root.");

        return path;
    }
}
