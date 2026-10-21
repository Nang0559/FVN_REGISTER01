using FVN_REGISTER.AI.Infrastructure.Services;

namespace FVN_REGISTER.AI.Tests;

public sealed class DocumentCandidateTextTests
{
    [Fact]
    public void ExtractDateCandidates_ParsesVietnameseWrittenDate()
    {
        var result = DocumentCandidateText.ExtractDateCandidates("Hà Nội, ngày 06 tháng 04 năm 2023");
        Assert.Equal(["2023-04-06"], result);
    }

    [Fact]
    public void ExtractDateCandidates_ParsesVietnameseDateWithoutDiacritics()
    {
        var result = DocumentCandidateText.ExtractDateCandidates("Ha Noi, ngay 6 thang 4 nam 2023");
        Assert.Equal(["2023-04-06"], result);
    }

    [Fact]
    public void ExtractDateCandidates_NormalisesNumericFormatsAndDeduplicates()
    {
        var result = DocumentCandidateText.ExtractDateCandidates(
            "06/04/2023 và 2023-04-06 và ngày 06 tháng 04 năm 2023, 02/10/2026");
        Assert.Equal(["2023-04-06", "2026-10-02"], result);
    }

    [Fact]
    public void ExtractDateCandidates_IgnoresInvalidDatesAndDocumentNumbers()
    {
        var result = DocumentCandidateText.ExtractDateCandidates("Số: FCC-06042023 và 31/02/2023 và ngày 45 tháng 13 năm 2023");
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("aS", true)]
    [InlineData("Sy", true)]
    [InlineData("NN /", true)]
    [InlineData("a 1", true)]
    [InlineData("   ", true)]
    [InlineData("QUYẾT ĐỊNH", false)]
    [InlineData("Sản 9", false)]
    [InlineData("2.1. Trường hợp", false)]
    public void IsNoiseLine_FlagsOnlyJunk(string line, bool expected) =>
        Assert.Equal(expected, DocumentCandidateText.IsNoiseLine(line));
}
