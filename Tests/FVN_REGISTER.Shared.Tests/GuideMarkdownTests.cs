using FVN_REGISTER.Shared.Services.Help;
using Xunit;

namespace FVN_REGISTER.Shared.Tests;

/// <summary>Guards the link between the screens' help (HelpRegistry) and the Markdown user guide rendered by /huong-dan.</summary>
public sealed class GuideMarkdownTests
{
    private static string GuidePath() => Path.Combine(FindRepositoryRoot(),
        "MÔ HÌNH", "03_TAI_LIEU_HE_THONG", "02_HUONG_DAN_SU_DUNG", HelpRegistry.GuideFile);

    private static string Guide() => File.ReadAllText(GuidePath());

    [Fact]
    public void EveryMappedSection_ExistsInTheGuide()
    {
        var markdown = Guide();
        var problems = new List<string>();
        foreach (var (code, ids) in HelpRegistry.GuideSections)
            foreach (var id in ids)
                if (GuideMarkdown.ExtractSection(markdown, id).Length == 0)
                    problems.Add($"{code}: section '{id}' not found in {HelpRegistry.GuideFile}");
        Assert.Empty(problems);
    }

    [Fact]
    public void EveryMappedCode_IsARealFeatureCode()
    {
        var known = HelpRegistry.AllFeatureCodes.ToHashSet(StringComparer.Ordinal);
        var unknown = HelpRegistry.GuideSections.Keys.Where(code => !known.Contains(code)).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Empty(unknown);
    }

    [Fact]
    public void Section_StopsAtTheNextHeadingOfTheSameOrHigherLevel()
    {
        var section = GuideMarkdown.ExtractSection(Guide(), "7.3");
        Assert.StartsWith("### 7.3.", section);
        Assert.Contains("/hrm/attendance-calculation", section);
        Assert.DoesNotContain("### 7.4.", section);
        Assert.DoesNotContain("Background Jobs", section);
    }

    [Fact]
    public void ChapterId_IncludesItsSubSections()
    {
        var chapter = GuideMarkdown.ExtractSection(Guide(), "7");
        Assert.Contains("### 7.1.", chapter);
        Assert.Contains("### 7.4.", chapter);
    }

    [Fact]
    public void HeadingTextId_SelectsTheAppendixBlock()
    {
        var block = GuideMarkdown.ExtractSection(Guide(), "~Nghiệp vụ, chấm công và báo cáo");
        Assert.Contains("/payroll/table", block);
    }

    [Fact]
    public void HashLinesInsideCodeFences_AreNotHeadings()
    {
        const string md = "## 1. A\ntext\n```\n# not a heading\n## 2. fake\n```\nmore\n## 2. B\nother";
        var section = GuideMarkdown.ExtractSection(md, "1");
        Assert.Contains("more", section);
        Assert.Contains("## 2. fake", section);
        Assert.DoesNotContain("other", section);
    }

    [Fact]
    public void UnknownSection_IsEmpty() =>
        Assert.Equal(string.Empty, GuideMarkdown.ExtractSection(Guide(), "99.9"));

    [Fact]
    public void ToHtml_PointsScreenshotsAtTheGuideAssetsUrl()
    {
        var html = GuideMarkdown.ToHtml("![x](../../screenshots/a.png)");
        Assert.Contains("/guide-assets/screenshots/a.png", html);
        Assert.DoesNotContain("../../screenshots/", html);
    }

    [Fact]
    public void ToHtml_DoesNotRenderRawHtml()
    {
        var html = GuideMarkdown.ToHtml("<script>alert(1)</script>");
        Assert.DoesNotContain("<script>", html);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FVN_REGISTER.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("FVN_REGISTER.sln not found above " + AppContext.BaseDirectory);
    }
}
