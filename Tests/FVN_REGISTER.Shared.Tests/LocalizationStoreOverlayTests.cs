using FVN_REGISTER.Shared.Services.Language;
using Xunit;

namespace FVN_REGISTER.Shared.Tests;

/// <summary>
/// The Language Center saves to the catalog files on the API server; clients apply those texts through the runtime overlay.
/// The overlay is static, so these tests share a collection with <see cref="LocalizationCatalogTests"/> (no parallel runs)
/// and always restore the embedded defaults.
/// </summary>
[Collection("LocalizationStore")]
public sealed class LocalizationStoreOverlayTests : IDisposable
{
    public LocalizationStoreOverlayTests() => LocalizationStore.ClearOverrides();

    public void Dispose() => LocalizationStore.ClearOverrides();

    private static Dictionary<string, string> One(string key, string value) => new() { [key] = value };

    [Fact]
    public void Overlay_WinsOverEmbeddedText_AndGetAllStaysEmbedded()
    {
        var embedded = LocalizationStore.Get(LanguageCode.Vi, "common.save");

        LocalizationStore.ApplyOverrides(1, One("common.save", "Ghi lại"), One("common.save", "セーブ"));

        Assert.Equal("Ghi lại", LocalizationStore.Get(LanguageCode.Vi, "common.save"));
        Assert.Equal("セーブ", LocalizationStore.Get(LanguageCode.Ja, "common.save"));
        Assert.Equal("Ghi lại", LocalizationStore.Get(LanguageCode.Vi, "COMMON.SAVE"));
        Assert.Equal(embedded, LocalizationStore.GetAll(LanguageCode.Vi)["common.save"]);
        Assert.Equal(1, LocalizationStore.OverridesVersion);
    }

    [Fact]
    public void ApplyingANewOverlay_ReplacesTheOldOne()
    {
        var embedded = LocalizationStore.Get(LanguageCode.Vi, "common.save");
        LocalizationStore.ApplyOverrides(1, One("common.save", "Ghi lại"), null);

        LocalizationStore.ApplyOverrides(2, One("common.cancel", "Bỏ qua"), null);

        Assert.Equal(embedded, LocalizationStore.Get(LanguageCode.Vi, "common.save"));
        Assert.Equal("Bỏ qua", LocalizationStore.Get(LanguageCode.Vi, "common.cancel"));
    }

    [Fact]
    public void Precedence_IsJaOverlay_JaEmbedded_ViOverlay_ViEmbedded()
    {
        var embeddedJa = LocalizationStore.GetAll(LanguageCode.Ja)["common.cancel"];

        LocalizationStore.ApplyOverrides(1, new Dictionary<string, string> { ["common.cancel"] = "Bỏ qua", ["only.vi"] = "Chỉ tiếng Việt" }, null);

        Assert.Equal(embeddedJa, LocalizationStore.Get(LanguageCode.Ja, "common.cancel"));   // embedded ja beats a vi overlay
        Assert.Equal("Chỉ tiếng Việt", LocalizationStore.Get(LanguageCode.Ja, "only.vi"));     // nothing in ja -> vi overlay
    }

    [Fact]
    public void EmptyValues_AreIgnored_SoATextCanNeverBecomeBlank()
    {
        LocalizationStore.ApplyOverrides(1, One("common.save", ""), One("common.save", "   ".Trim()));

        Assert.Equal(LocalizationStore.GetAll(LanguageCode.Vi)["common.save"], LocalizationStore.Get(LanguageCode.Vi, "common.save"));
        Assert.Equal(LocalizationStore.GetAll(LanguageCode.Ja)["common.save"], LocalizationStore.Get(LanguageCode.Ja, "common.save"));
    }

    [Fact]
    public void ClearOverrides_RestoresDefaults()
    {
        var embedded = LocalizationStore.Get(LanguageCode.Vi, "common.save");
        LocalizationStore.ApplyOverrides(5, One("common.save", "X"), null);

        LocalizationStore.ClearOverrides();

        Assert.Equal(embedded, LocalizationStore.Get(LanguageCode.Vi, "common.save"));
        Assert.Equal(-1, LocalizationStore.OverridesVersion);
    }

    [Fact]
    public async Task ConcurrentSwaps_NeverTearAReader()
    {
        var bad = 0;
        using var stop = new CancellationTokenSource();
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                var value = LocalizationStore.Get(LanguageCode.Vi, "perf.key");
                if (value != "perf.key" && !value.StartsWith('V')) Interlocked.Increment(ref bad);
            }
        })).ToArray();

        for (var i = 0; i < 5000; i++)
            LocalizationStore.ApplyOverrides(i, One("perf.key", "V" + i), null);

        stop.Cancel();
        await Task.WhenAll(readers);
        Assert.Equal(0, bad);
    }
}
