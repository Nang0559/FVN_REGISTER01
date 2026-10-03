using System.Text.RegularExpressions;
using FVN_REGISTER.Shared.Services.Language;
using Xunit;

namespace FVN_REGISTER.Shared.Tests;

/// <summary>
/// Guards the UI text catalog (FVN_REGISTER.Shared/Localization/{lang}.{module}.json).
/// Duplicate keys inside one file are rejected by <see cref="LocalizationStore"/> itself (see the first test).
/// </summary>
[Collection("LocalizationStore")]
public sealed class LocalizationCatalogTests
{
    private static readonly Regex Placeholder = new(@"\{(\d+)(?:[^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex LiteralKeyCall = new(@"\bT\(\s*""([A-Za-z0-9_.]+)""", RegexOptions.Compiled);

    private static IReadOnlyList<LocalizationFile> Files() =>
        LocalizationStore.ReadFiles(typeof(LocalizationStore).Assembly);

    [Fact]
    public void Catalog_Loads_WithoutDuplicateKeys()
    {
        // ReadFiles throws on a duplicate key inside one file; GetAll throws on a key repeated across files.
        var files = Files();
        Assert.NotEmpty(files);
        Assert.NotEmpty(LocalizationStore.GetAll(LanguageCode.Vi));
        Assert.NotEmpty(LocalizationStore.GetAll(LanguageCode.Ja));
    }

    [Fact]
    public void EveryKey_LivesInTheFileOfItsModule()
    {
        var problems = new List<string>();
        foreach (var file in Files())
            foreach (var key in file.Entries.Keys)
                if (!key.StartsWith(file.Module + ".", StringComparison.OrdinalIgnoreCase))
                    problems.Add($"{file.Language}.{file.Module}.json contains '{key}' (must start with '{file.Module}.')");
        Assert.Empty(problems);
    }

    [Fact]
    public void Vietnamese_And_Japanese_HaveExactlyTheSameKeys()
    {
        var vi = LocalizationStore.GetAll(LanguageCode.Vi).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ja = LocalizationStore.GetAll(LanguageCode.Ja).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var problems = vi.Except(ja, StringComparer.OrdinalIgnoreCase).Select(k => $"missing in ja: {k}")
            .Concat(ja.Except(vi, StringComparer.OrdinalIgnoreCase).Select(k => $"missing in vi: {k}"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        Assert.Empty(problems);
    }

    [Fact]
    public void NoValue_IsEmpty()
    {
        var problems = new List<string>();
        foreach (var lang in new[] { LanguageCode.Vi, LanguageCode.Ja })
            foreach (var (key, value) in LocalizationStore.GetAll(lang))
                if (string.IsNullOrWhiteSpace(value))
                    problems.Add($"{lang}: '{key}' is empty");
        Assert.Empty(problems);
    }

    [Fact]
    public void Vietnamese_And_Japanese_UseTheSamePlaceholders()
    {
        var vi = LocalizationStore.GetAll(LanguageCode.Vi);
        var ja = LocalizationStore.GetAll(LanguageCode.Ja);
        var problems = new List<string>();
        foreach (var (key, viText) in vi)
        {
            if (!ja.TryGetValue(key, out var jaText)) continue;
            var a = Indexes(viText);
            var b = Indexes(jaText);
            if (!a.SetEquals(b))
                problems.Add($"'{key}': vi uses {{{string.Join(",", a.OrderBy(x => x))}}} but ja uses {{{string.Join(",", b.OrderBy(x => x))}}}");
        }
        Assert.Empty(problems);

        static HashSet<int> Indexes(string text) =>
            Placeholder.Matches(text).Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();
    }

    [Fact]
    public void EveryLiteralKey_UsedInSource_Exists()
    {
        var root = FindRepositoryRoot();
        var shared = Path.Combine(root, "FVN_REGISTER.Shared");
        var known = LocalizationStore.GetAll(LanguageCode.Vi);

        var problems = new List<string>();
        var sources = Directory.EnumerateFiles(shared, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        foreach (var file in sources)
        {
            var text = File.ReadAllText(file);
            foreach (Match m in LiteralKeyCall.Matches(text))
            {
                var key = m.Groups[1].Value;
                if (!known.ContainsKey(key))
                    problems.Add($"{Path.GetRelativePath(root, file)}: T(\"{key}\") has no entry in Localization/*.json");
            }
        }
        Assert.Empty(problems.Distinct().OrderBy(x => x, StringComparer.Ordinal));
    }

    // ---- regressions for the keys that used to collide -------------------------------------------------

    [Fact]
    public void DashboardPending_And_PendingMine_AreDifferentTexts()
    {
        // "dashboard.pending" used to be declared twice; the second declaration silently replaced the first.
        Assert.Equal("Chờ duyệt", LocalizationStore.Get(LanguageCode.Vi, "dashboard.pending"));
        Assert.Equal("Đơn đang chờ tôi xử lý", LocalizationStore.Get(LanguageCode.Vi, "dashboard.pendingMine"));
    }

    [Fact]
    public void ApprovalLevel_Label_And_Formatted_AreSeparateKeys()
    {
        Assert.Equal("Cấp duyệt", LocalizationStore.Get(LanguageCode.Vi, "approval.level"));
        Assert.Equal("Cấp 2", string.Format(System.Globalization.CultureInfo.InvariantCulture,
            LocalizationStore.Get(LanguageCode.Vi, "approval.levelN"), 2));
        Assert.Equal("第2承認", string.Format(System.Globalization.CultureInfo.InvariantCulture,
            LocalizationStore.Get(LanguageCode.Ja, "approval.levelN"), 2));
    }

    [Fact]
    public void MissingKey_ReturnsTheKeyItself()
    {
        Assert.Equal("no.such.key", LocalizationStore.Get(LanguageCode.Vi, "no.such.key"));
        Assert.Equal("no.such.key", LocalizationStore.Get(LanguageCode.Ja, "no.such.key"));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FVN_REGISTER.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("FVN_REGISTER.sln not found above " + AppContext.BaseDirectory);
    }
}
