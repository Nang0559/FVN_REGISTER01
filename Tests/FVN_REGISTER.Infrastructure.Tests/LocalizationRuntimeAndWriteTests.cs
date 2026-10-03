using FVN_REGISTER.Contract.Dtos.Language;
using FVN_REGISTER.Infrastructure.Services.Language;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

/// <summary>Language Center: files are edited by several administrators at once and clients pick the result up at runtime.</summary>
public sealed class LocalizationRuntimeAndWriteTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fvn-loc-" + Guid.NewGuid().ToString("N"));
    private readonly string _catalog;

    public LocalizationRuntimeAndWriteTests()
    {
        _catalog = Path.Combine(_root, "Localization");
        Directory.CreateDirectory(_catalog);
        File.WriteAllText(Path.Combine(_catalog, "vi.common.json"), "{\n  \"common.save\": \"Lưu\"\n}\n");
        File.WriteAllText(Path.Combine(_catalog, "ja.common.json"), "{\n  \"common.save\": \"保存\"\n}\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* temp folder */ }
    }

    private LocalizationManagementService Service() =>
        new(new TestEnvironment(_root), new ConfigurationBuilder().Build());

    [Fact]
    public async Task SimultaneousSaves_LoseNothingAndLeaveNoTempFiles()
    {
        var service = Service();
        const int threads = 16, perThread = 8;
        using var barrier = new Barrier(threads);

        var workers = Enumerable.Range(0, threads).Select(t => Task.Factory.StartNew(() =>
        {
            barrier.SignalAndWait();
            for (var j = 0; j < perThread; j++)
                service.UpsertAsync(new LocalizationUpsertRequest { Key = $"common.k{t}_{j}", Module = "common", Vi = "vi", Ja = "ja" }).Wait();
        }, TaskCreationOptions.LongRunning)).ToArray();
        await Task.WhenAll(workers);

        var catalog = await service.GetCatalogAsync();
        Assert.Equal(threads * perThread, catalog.Entries.Count(e => e.Key.Contains('_')));
        Assert.Empty(Directory.EnumerateFiles(_catalog, "*.tmp"));
    }

    [Fact]
    public async Task SavedText_ShowsUpInTheRuntimeSnapshot_WithANewVersion()
    {
        var service = Service();
        var first = await service.GetRuntimeSnapshotAsync(null);
        Assert.True(first.Version > 0);
        Assert.Equal("Lưu", first.Vi["common.save"]);
        Assert.True((await service.GetRuntimeSnapshotAsync(first.Version)).Unchanged);

        await service.UpsertAsync(new LocalizationUpsertRequest { Key = "common.save", Module = "common", Vi = "Ghi lại", Ja = "保存する" });

        var next = await service.GetRuntimeSnapshotAsync(first.Version);
        Assert.False(next.Unchanged);
        Assert.NotEqual(first.Version, next.Version);
        Assert.Equal("Ghi lại", next.Vi["common.save"]);
        Assert.Equal("保存する", next.Ja["common.save"]);
    }

    [Fact]
    public async Task MovingAKeyToAnotherModule_LeavesExactlyOneEntry()
    {
        var service = Service();
        await service.UpsertAsync(new LocalizationUpsertRequest { Key = "common.k1", Module = "common", Vi = "a", Ja = "b" });
        await service.UpsertAsync(new LocalizationUpsertRequest { Key = "common.k10", Module = "common", Vi = "c", Ja = "d" });

        await service.UpsertAsync(new LocalizationUpsertRequest { Key = "common.k1", Module = "leave", Vi = "a", Ja = "b" });

        var catalog = await service.GetCatalogAsync();
        Assert.Equal("leave", Assert.Single(catalog.Entries, e => e.Key == "common.k1").Module);
        Assert.Contains(catalog.Entries, e => e.Key == "common.k10" && e.Module == "common");
    }

    [Fact]
    public async Task MissingCatalogFolder_GivesAnEmptySnapshotInsteadOfAnError()
    {
        var elsewhere = new LocalizationManagementService(new TestEnvironment(Path.Combine(_root, "nowhere")), new ConfigurationBuilder().Build());
        var snapshot = await elsewhere.GetRuntimeSnapshotAsync(null);
        Assert.Equal(0, snapshot.Version);
        Assert.Empty(snapshot.Vi);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public TestEnvironment(string contentRoot) => ContentRootPath = contentRoot;
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "FVN_REGISTER.Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
