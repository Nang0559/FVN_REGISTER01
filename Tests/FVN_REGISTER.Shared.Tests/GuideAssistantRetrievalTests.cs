using FVN_REGISTER.Contract.Dtos.Security;
using FVN_REGISTER.Contract.Help;
using FVN_REGISTER.Core.Constants;
using Xunit;

namespace FVN_REGISTER.Shared.Tests;

/// <summary>Guards the guide assistant's retrieval: what it can find in the real Markdown guide and who may see which passage.</summary>
public sealed class GuideAssistantRetrievalTests
{
    private static string GuideDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "MÔ HÌNH")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "MÔ HÌNH", "03_TAI_LIEU_HE_THONG", "02_HUONG_DAN_SU_DUNG");
    }

    private static IReadOnlyList<GuideChunk> AllChunks()
    {
        var dir = GuideDirectory();
        var chunks = new List<GuideChunk>();
        chunks.AddRange(GuideChunker.Chunk(GuideSectionMap.MainGuideFile, File.ReadAllText(Path.Combine(dir, GuideSectionMap.MainGuideFile)), allGeneral: false));
        chunks.AddRange(GuideChunker.Chunk("05_QUICK_GUIDE.md", File.ReadAllText(Path.Combine(dir, "05_QUICK_GUIDE.md")), allGeneral: true));
        chunks.AddRange(GuideChunker.Chunk("06_FAQ.md", File.ReadAllText(Path.Combine(dir, "06_FAQ.md")), allGeneral: true));
        return chunks;
    }

    private static PermissionSnapshotDto Snapshot(params int[] functionCodes) => new()
    {
        UserId = 1,
        RoleCodes = new List<int> { 1 },
        FunctionCodes = functionCodes.ToHashSet()
    };

    private static Func<GuideChunk, bool> VisibleTo(PermissionSnapshotDto snapshot) =>
        chunk => chunk.General || chunk.FeatureCodes.Any(code => HelpAccessPolicy.CanView(snapshot, code));

    [Fact]
    public void Chunker_ProducesBoundedPassagesWithoutImagesOrAppendix()
    {
        var chunks = AllChunks();
        Assert.True(chunks.Count > 30);
        Assert.All(chunks, c => Assert.True(c.Text.Length <= 1600, $"{c.Id} {c.Title} is {c.Text.Length} chars"));
        Assert.DoesNotContain(chunks, c => c.Text.Contains("![", StringComparison.Ordinal));
        Assert.DoesNotContain(chunks, c => c.Title.StartsWith("Phụ lục", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Chunker_LinksPassagesToTheFeaturesThatDocumentThem()
    {
        var chunks = AllChunks();
        Assert.Contains(chunks, c => c.SectionId == "2.2" && c.FeatureCodes.Contains("OT"));
        Assert.Contains(chunks, c => c.SectionId.StartsWith("7.3", StringComparison.Ordinal) && c.FeatureCodes.Contains("ADMIN_POLICIES"));
        // Chapter 8 is selected in the map by its heading text, not by number.
        Assert.Contains(chunks, c => c.FeatureCodes.Contains("REPORTS"));
    }

    [Fact]
    public void Search_FindsOtRegistrationWithoutDiacritics()
    {
        var chunks = AllChunks();
        var all = VisibleTo(Snapshot(SecurityFunctionCodes.OTView, SecurityFunctionCodes.OTCreate));

        var hits = GuideSearch.Search(chunks, "cach dang ky OT cho nhieu nguoi", all, "OT", 4, 3600);

        Assert.NotEmpty(hits);
        Assert.Contains(hits, h => h.Chunk.SectionId == "2.2");
        Assert.True(hits.Sum(h => h.Chunk.Text.Length) <= 3600);
    }

    [Fact]
    public void Search_DoesNotExposeAdminSectionsToAnOrdinaryUser()
    {
        var chunks = AllChunks();
        var staff = VisibleTo(Snapshot(SecurityFunctionCodes.OTView, SecurityFunctionCodes.OTCreate));
        var admin = VisibleTo(Snapshot(SecurityFunctionCodes.OTLimitManage, SecurityFunctionCodes.AttendanceCalculate));

        const string question = "cấu hình hạn mức OT và ký hiệu chấm công";
        var staffHits = GuideSearch.Search(chunks, question, staff, null, 6, 12_000);
        var adminHits = GuideSearch.Search(chunks, question, admin, null, 6, 12_000);

        Assert.DoesNotContain(staffHits, h => h.Chunk.SectionId.StartsWith("7.", StringComparison.Ordinal));
        Assert.Contains(adminHits, h => h.Chunk.SectionId.StartsWith("7.3", StringComparison.Ordinal));
    }

    [Fact]
    public void Search_GeneralFaqIsVisibleToEveryone()
    {
        var chunks = AllChunks();
        var nobodySpecial = VisibleTo(Snapshot());

        var hits = GuideSearch.Search(chunks, "Escalated có phải là Rejected không", nobodySpecial, null, 4, 3600);

        Assert.Contains(hits, h => h.Chunk.SourceFile == "06_FAQ.md");
    }

    [Fact]
    public void Search_ReturnsNothingWhenNoWordMatches()
    {
        var chunks = AllChunks();
        var hits = GuideSearch.Search(chunks, "thời tiết hôm nay ở Hà Nội", _ => true, null, 4, 3600);
        Assert.Empty(hits);
    }

    [Fact]
    public void Search_BoostsThePassagesOfTheCurrentScreen()
    {
        var chunks = AllChunks();
        var allowEverything = new Func<GuideChunk, bool>(_ => true);

        var withBoost = GuideSearch.Search(chunks, "đăng ký", allowEverything, "TRIP", 1, 3600);
        Assert.Single(withBoost);
        Assert.Contains("TRIP", withBoost[0].Chunk.FeatureCodes);
    }

    [Fact]
    public void Fold_RemovesVietnameseDiacritics()
    {
        Assert.Equal("dang ky nghi phep", GuideSearch.Fold("Đăng ký nghỉ phép"));
    }

    [Fact]
    public void SectionMap_IsInvertible()
    {
        Assert.Contains("OT", GuideSectionMap.FeaturesFor("2.2"));
        Assert.Contains("ADMIN_POLICIES", GuideSectionMap.FeaturesFor("7.3"));
        Assert.Empty(GuideSectionMap.FeaturesFor("99.9"));
        Assert.Equal(new[] { "2.2" }, GuideSectionMap.SectionsFor("OT"));
    }
}
