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

    [Fact]
    public void ExtractDocumentNumberCandidates_ReadsNumberAfterSo()
    {
        var result = DocumentCandidateText.ExtractDocumentNumberCandidates("Số: FCC-06042023 cerer 000 =r-ee=");
        Assert.Equal(["FCC-06042023"], result);
    }

    [Fact]
    public void ExtractFormCodeCandidates_FindsInternalFormCodes()
    {
        var result = DocumentCandidateText.ExtractFormCodeCandidates("viết vào “Don xin nghỉ” (HC-NS-05) và FCC-06042023");
        Assert.Equal(["HC-NS-05"], result);
    }

    [Fact]
    public void ExtractArticleCandidates_NormalisesArticleAndChapter()
    {
        var result = DocumentCandidateText.ExtractArticleCandidates(
            "Điều 15- Chương 3 Nghỉ phép năm\nĐiều 18 - Chương 3 Thủ tục xin nghỉ\nĐiều 2. Quyết định này\nĐiều 15- Chương 3");
        Assert.Equal(["Điều 15 - Chương 3", "Điều 18 - Chương 3", "Điều 2"], result);
    }

    [Fact]
    public void ExtractNumericCandidates_DropsDatesNumbersCodesAndOutlineNumbering()
    {
        const string text = "[PAGE 1]\nSố: FCC-06042023\nHà Nội, ngày 06 tháng 04 năm 2023\nĐiều 15- Chương 3\n" +
                            "2.1. Trường hợp sử dụng 3 ngày và 15.8 giờ\nmẫu HC-NS-05\n[PAGE 2]";
        Assert.Equal(["3", "15.8"], DocumentCandidateText.ExtractNumericCandidates(text));
    }

    [Fact]
    public void ExtractNumericCandidates_KeepsDecimalAtLineStart()
    {
        Assert.Equal(["15.8"], DocumentCandidateText.ExtractNumericCandidates("15.8 giờ"));
    }

    [Theory]
    [InlineData("~ z G x I 2 ng 7 {", true)]
    [InlineData("7 4ÙT H@ÔONG DOAN TONG GEAM DOC", true)]
    [InlineData("05 * Sy, \\2 4 AN }", true)]
    [InlineData("Q việt NAIR i œ 4 Ea \\", true)]
    [InlineData("Zz RACH NIỆM HOU lề / kỷ h #À", true)]
    [InlineData("Số: FCC-06042023 creeee OỦO ==~--==", false)]
    [InlineData("Điều 15- Chương 3 Nghỉ phép năm có hưởng lương (muc 15.6&15.7&15.8)", false)]
    [InlineData("viết vào “Đơn xin nghỉ” (HC-NS-05) trong các trường hợp sau:", false)]
    [InlineData("Ngày đầu tiên đi làm trở lại : TAYE SENS NBN PMP kg ° NON việt", false)]
    [InlineData("1 2 3 4 5 6 7", false)]
    [InlineData("- Như Điều 3.", false)]
    public void IsLowQualityLine_FlagsGarbageButKeepsRealLines(string line, bool expected) =>
        Assert.Equal(expected, DocumentCandidateText.IsLowQualityLine(line));

    [Fact]
    public void ExtractNumericCandidates_FromCleanText_IgnoresDigitsInGarbageLines()
    {
        const string text = "Điều 15- Chương 3 Nghỉ phép năm (muc 15.6&15.7&15.8)\n~ z G x I 2 ng 7 {\n05 * Sy, \\2 4 AN }";
        var clean = DocumentCandidateText.CleanForCandidates(text);
        Assert.Equal(["15.6", "15.7", "15.8"], DocumentCandidateText.ExtractNumericCandidates(clean));
    }

    [Fact]
    public void ExtractReferencedArticleCandidates_SkipsOwnHeadings()
    {
        const string text = "Điều 1. Sửa đổi các nội dung\nĐiều 15- Chương 3 Nghỉ phép năm\nĐiều 18 - Chương 3 Thủ tục\nĐiều 2. Quyết định này có hiệu lực\n- Như Điều 3.";
        Assert.Equal(["Điều 15 - Chương 3", "Điều 18 - Chương 3", "Điều 3"],
            DocumentCandidateText.ExtractReferencedArticleCandidates(text));
    }

    [Theory]
    [InlineData("[PAGE 1]\nHà Nội, ngày 06 tháng 04 năm 2023\nQUYÉT ĐỊNH\n(V/v: Sửa đổi nội quy)", true)]
    [InlineData("QUYẾT ĐỊNH", true)]
    [InlineData("Căn cứ quyết định số 12 của Giám đốc", false)]
    [InlineData("Quy trình xin nghỉ phép", false)]
    public void LooksLikeDecision_RequiresATitleLine(string text, bool expected) =>
        Assert.Equal(expected, DocumentCandidateText.LooksLikeDecision(text));
}
