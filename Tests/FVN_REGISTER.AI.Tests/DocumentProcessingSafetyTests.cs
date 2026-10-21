using FVN_REGISTER.AI.Application.Abstractions;
using System.IO.Compression;
using System.Text;
using FVN_REGISTER.AI.Infrastructure.Extraction;
using FVN_REGISTER.AI.Infrastructure.Security;

namespace FVN_REGISTER.AI.Tests;

public sealed class LocalDocumentTextExtractorTests
{
    [Fact]
    public async Task ExtractTextAsync_ReadsUtf8TextAndRestoresStreamPosition()
    {
        var bytes = Encoding.UTF8.GetBytes("FCC Việt Nam\nAI platform");
        await using var stream = new MemoryStream(bytes);
        stream.Position = 2;
        var result = await new LocalDocumentTextExtractor(new TestOcrTextExtractor()).ExtractTextAsync(stream, "text/plain");
        Assert.Contains("FCC Việt Nam", result);
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public async Task ExtractTextAsync_RejectsMalformedPdfAndRestoresStreamPosition()
    {
        await using var stream = new MemoryStream("%PDF-test"u8.ToArray());
        await Assert.ThrowsAnyAsync<Exception>(() =>
            new LocalDocumentTextExtractor(new TestOcrTextExtractor()).ExtractTextAsync(stream, "application/pdf"));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task ExtractTextAsync_ReadsBasicXlsxSharedStrings()
    {
        await using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var shared = zip.CreateEntry("xl/sharedStrings.xml");
            await using (var writer = new StreamWriter(shared.Open()))
                await writer.WriteAsync("<sst><si><t>EmployeeCode</t></si><si><t>E0001</t></si></sst>");
            var sheet = zip.CreateEntry("xl/worksheets/sheet1.xml");
            await using (var writer = new StreamWriter(sheet.Open()))
                await writer.WriteAsync("<worksheet><sheetData><row><c t='s'><v>0</v></c><c t='s'><v>1</v></c></row></sheetData></worksheet>");
        }
        stream.Position = 0;
        var result = await new LocalDocumentTextExtractor(new TestOcrTextExtractor()).ExtractTextAsync(stream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        Assert.Contains("EmployeeCode", result);
        Assert.Contains("E0001", result);
    }
    [Fact]
    public async Task ExtractTextAsync_FlattensJsonWithPropertyPaths()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{"employee":{"code":"E0001","active":true}}"""));
        var result = await new LocalDocumentTextExtractor(new TestOcrTextExtractor()).ExtractTextAsync(stream, "application/json");
        Assert.Contains("$.employee.code = E0001", result);
        Assert.Contains("$.employee.active = True", result);
    }

    [Fact]
    public async Task ExtractTextAsync_ExtractsXmlElementAndAttributeValues()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<request id='R1'><employee>E0001</employee></request>"));
        var result = await new LocalDocumentTextExtractor(new TestOcrTextExtractor()).ExtractTextAsync(stream, "application/xml");
        Assert.Contains("/request/@id = R1", result);
        Assert.Contains("/request/employee = E0001", result);
    }

    [Fact]
    public async Task ExtractTextAsync_ReadsDocxParagraphs()
    {
        await using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var document = zip.CreateEntry("word/document.xml");
            await using var writer = new StreamWriter(document.Open());
            await writer.WriteAsync("<document><body><p><r><t>Equipment request</t></r></p><p><r><t>Quantity 5</t></r></p></body></document>");
        }
        stream.Position = 0;
        var result = await new LocalDocumentTextExtractor(new TestOcrTextExtractor()).ExtractTextAsync(stream,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        Assert.Contains("Equipment request", result);
        Assert.Contains("Quantity 5", result);
    }

    private sealed class TestOcrTextExtractor : IOcrTextExtractor
    {
        public Task<string> ExtractTextAsync(Stream content, string mediaType, CancellationToken cancellationToken = default) =>
            Task.FromResult("OCR test text");
    }

}

public sealed class WindowsDefenderMalwareScannerTests
{
    [Fact]
    public async Task ScanAsync_WhenExecutableMissing_ReturnsUnknownAndNeverClean()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new FVN_REGISTER.AI.Application.Configuration.MalwareScannerOptions
        {
            Provider = "WindowsDefender",
            DefenderExecutable = @"Z:\does-not-exist\MpCmdRun.exe"
        });
        var result = await new WindowsDefenderMalwareScanner(options).ScanAsync(new MemoryStream([1, 2, 3]));
        Assert.Equal(MalwareScanVerdict.Unknown, result.Verdict);
        Assert.Contains(result.FailureCode, new[] { "DEFENDER_UNAVAILABLE", "DEFENDER_UNSUPPORTED_PLATFORM" });
    }

    [Theory]
    [InlineData("ClamAV", true)]
    [InlineData("WindowsDefender", true)]
    [InlineData("windowsdefender", true)]
    [InlineData("Unconfigured", false)]
    [InlineData(null, false)]
    public void IsOperationalProvider_RecognisesOnlyRealScanners(string? provider, bool expected) =>
        Assert.Equal(expected, FVN_REGISTER.AI.Application.Configuration.MalwareScannerOptions.IsOperationalProvider(provider));
}

public sealed class UnconfiguredMalwareScannerTests
{
    [Fact]
    public async Task ScanAsync_ReturnsUnknownAndNeverClean()
    {
        var result = await new UnconfiguredMalwareScanner().ScanAsync(new MemoryStream([1, 2, 3]));
        Assert.Equal(MalwareScanVerdict.Unknown, result.Verdict);
        Assert.Equal("MALWARE_SCANNER_NOT_CONFIGURED", result.FailureCode);
    }
}
