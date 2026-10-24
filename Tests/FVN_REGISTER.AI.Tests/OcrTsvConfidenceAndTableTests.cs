using FVN_REGISTER.AI.Infrastructure.Extraction;

namespace FVN_REGISTER.AI.Tests;

public sealed class OcrTsvConfidenceAndTableTests
{
    private const string Header = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext";

    private static string Word(int line, int word, int left, int top, int width, int height, string conf, string text) =>
        $"5\t1\t1\t1\t{line}\t{word}\t{left}\t{top}\t{width}\t{height}\t{conf}\t{text}";

    [Fact]
    public void FormatPage_UsesRealWordConfidenceForParagraphLines()
    {
        var tsv = string.Join('\n', Header,
            "1\t1\t0\t0\t0\t0\t0\t0\t800\t600\t-1\t",
            Word(1, 1, 10, 10, 60, 20, "96", "Quy"),
            Word(1, 2, 80, 10, 60, 20, "92", "định"));

        var text = TesseractTsvFormatter.FormatPage(TesseractTsvFormatter.ParseWords(tsv));

        Assert.Equal("[LINE conf=0.94] Quy định\n", text.Replace("\r\n", "\n"));
    }

    [Fact]
    public void ParseWords_IgnoresStructureRowsEmptyWordsAndNegativeConfidence()
    {
        var tsv = string.Join('\n', Header,
            "2\t1\t1\t0\t0\t0\t0\t0\t100\t50\t-1\t",
            Word(1, 1, 10, 10, 50, 20, "-1", "noise"),
            Word(1, 2, 70, 10, 50, 20, "88.5", "   "),
            Word(1, 3, 130, 10, 50, 20, "88.5", "ok"));

        var words = TesseractTsvFormatter.ParseWords(tsv);

        var word = Assert.Single(words);
        Assert.Equal("ok", word.Text);
        Assert.Equal(0.885, word.Confidence, 3);
    }

    [Fact]
    public void FormatPage_DetectsTableRowsFromLargeHorizontalGaps()
    {
        var tsv = string.Join('\n', Header,
            Word(1, 1, 10, 10, 80, 20, "90", "Họ"), Word(1, 2, 300, 10, 80, 20, "80", "Ngày"), Word(1, 3, 600, 10, 80, 20, "70", "Số"),
            Word(2, 1, 10, 50, 80, 20, "100", "An"), Word(2, 2, 300, 50, 80, 20, "90", "01/02"), Word(2, 3, 600, 50, 80, 20, "80", "3"));

        var text = TesseractTsvFormatter.FormatPage(TesseractTsvFormatter.ParseWords(tsv)).Replace("\r\n", "\n");

        Assert.Equal(
            "[ROW 1 conf=0.8 cc=0.9,0.8,0.7] Họ\tNgày\tSố\n" +
            "[ROW 2 conf=0.9 cc=1,0.9,0.8] An\t01/02\t3\n", text);
    }

    [Fact]
    public void FormatPage_SingleMultiColumnLineIsNotATable()
    {
        var tsv = string.Join('\n', Header,
            Word(1, 1, 10, 10, 80, 20, "90", "Mục"), Word(1, 2, 400, 10, 80, 20, "90", "Trang 1"));

        var text = TesseractTsvFormatter.FormatPage(TesseractTsvFormatter.ParseWords(tsv));

        Assert.StartsWith("[LINE conf=0.9] Mục Trang 1", text);
        Assert.DoesNotContain("[ROW", text);
    }

    [Fact]
    public async Task Representation_ReadsConfidenceMarkersIntoParagraphsRowsAndCells()
    {
        const string text = "[OCR]\n[PAGE 1]\n[LINE conf=0.5] Điều 1\n[ROW 1 conf=0.9 cc=0.9,0.8] a\tb\n[ROW 2 conf=0.7 cc=0.7,0.6] c\td\n";

        var representation = await new DocumentRepresentationExtractor().ExtractAsync(text, "image/png");

        Assert.True(representation.IsOcr);
        var page = Assert.Single(representation.Pages);
        Assert.Equal(0.5, Assert.Single(page.Paragraphs).Confidence, 3);
        Assert.Equal(2, page.Rows.Count);
        Assert.Equal(0.8, page.Rows[0].Cells[1].Confidence, 3);
        var table = Assert.Single(representation.Tables);
        Assert.Equal(0.8, table.Confidence, 3);
    }

    [Fact]
    public async Task Representation_WithoutMarkersKeepsLegacyOcrConfidence()
    {
        var representation = await new DocumentRepresentationExtractor().ExtractAsync("[OCR]\n[PAGE 1]\nplain line\n", "image/png");

        Assert.Equal(0.85, representation.Pages[0].Paragraphs[0].Confidence, 3);
    }

    [Theory]
    [InlineData("[LINE conf=0.93] Quy định nghỉ phép", "Quy định nghỉ phép")]
    [InlineData("[ROW 3 conf=0.91 cc=0.9,0.8] a\tb", "[ROW 3] a\tb")]
    [InlineData("[ROW 3] a\tb", "[ROW 3] a\tb")]
    [InlineData("[PAGE 2]", "[PAGE 2]")]
    [InlineData("plain text", "plain text")]
    public void StripLine_RemovesOnlyConfidenceAttributes(string input, string expected) =>
        Assert.Equal(expected, OcrTextMarkers.StripLine(input));
}
