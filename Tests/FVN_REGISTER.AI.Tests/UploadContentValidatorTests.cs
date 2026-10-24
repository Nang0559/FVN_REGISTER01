using System.IO.Compression;
using System.Text;
using FVN_REGISTER.AI.Infrastructure.Validation;

namespace FVN_REGISTER.AI.Tests;

public sealed class UploadContentValidatorTests
{
    private readonly UploadContentValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_AcceptsPdfWithPdfSignature()
    {
        await using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\nbody"));
        var result = await _validator.ValidateAsync(stream, "../incoming/report.PDF");
        Assert.Equal("report.PDF", result.FileName);
        Assert.Equal("application/pdf", result.MediaType);
        Assert.Equal(".pdf", result.Extension);
    }

    [Fact]
    public async Task ValidateAsync_RejectsPdfWithWrongSignature()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a pdf"));
        await Assert.ThrowsAsync<InvalidDataException>(() => _validator.ValidateAsync(stream, "report.pdf"));
    }

    [Fact]
    public async Task ValidateAsync_RejectsUnsupportedExtension()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        await Assert.ThrowsAsync<InvalidDataException>(() => _validator.ValidateAsync(stream, "payload.exe"));
    }

    [Fact]
    public async Task ValidateAsync_AcceptsUtf8Csv()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Code,Name\nA01,Thiết bị"));
        var result = await _validator.ValidateAsync(stream, "items.csv");
        Assert.Equal("text/csv", result.MediaType);
    }

    [Fact]
    public async Task ValidateAsync_RejectsInvalidUtf8Text()
    {
        await using var stream = new MemoryStream(new byte[] { 0xFF, 0xFE, 0xFD });
        await Assert.ThrowsAsync<InvalidDataException>(() => _validator.ValidateAsync(stream, "data.txt"));
    }

    [Fact]
    public async Task ValidateAsync_AcceptsXlsxPackageWithWorkbookParts()
    {
        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using (var types = new StreamWriter(archive.CreateEntry("[Content_Types].xml").Open()))
                await types.WriteAsync("<Types />");
            using (var workbook = new StreamWriter(archive.CreateEntry("xl/workbook.xml").Open()))
                await workbook.WriteAsync("<workbook />");
        }
        stream.Position = 0;
        var result = await _validator.ValidateAsync(stream, "workbook.xlsx");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.MediaType);
    }

    [Fact]
    public async Task ValidateAsync_RejectsZipThatIsNotAnXlsxWorkbook()
    {
        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            await using var entry = new StreamWriter(archive.CreateEntry("readme.txt").Open());
            await entry.WriteAsync("not an Excel workbook");
        }
        stream.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => _validator.ValidateAsync(stream, "not-excel.xlsx"));
    }
}