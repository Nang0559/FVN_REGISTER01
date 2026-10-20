using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Storage;
using Xunit;

namespace FVN_REGISTER.AI.Tests;

public sealed class XlsxTemplateWriterTests
{
    [Fact]
    public void Write_PreservesTemplateAndWritesMappedRows()
    {
        using var template = CreateMinimalWorkbook();
        var definition = new DataEntryTemplateDefinition("Sheet1", 1, 2, new[]
        {
            new DataEntryColumnDefinition("items[].itemCode", "Mã hàng", "A", "string", true),
            new DataEntryColumnDefinition("items[].quantity", "Số lượng", "B", "decimal", true)
        });
        var rows = new[]
        {
            new DataEntryRow(new Dictionary<string, JsonElement>
            {
                ["items[].itemCode"] = JsonDocument.Parse("\"ABC-01\"").RootElement.Clone(),
                ["items[].quantity"] = JsonDocument.Parse("12.5").RootElement.Clone()
            })
        };

        var output = new XlsxTemplateWriter().Write(template, definition, rows);
        using var workbook = new MemoryStream(output);
        using var archive = new ZipArchive(workbook, ZipArchiveMode.Read);
        var sheet = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet);
        using var reader = new StreamReader(sheet!.Open());
        var xml = XDocument.Parse(reader.ReadToEnd());
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        Assert.Contains("ABC-01", xml.ToString());
        Assert.Contains("12.5", xml.ToString());
        Assert.NotNull(xml.Root!.Element(ns + "sheetData")!.Elements(ns + "row").SingleOrDefault(x => (string?)x.Attribute("r") == "2"));
    }

    private static MemoryStream CreateMinimalWorkbook()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Add(archive, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add(archive, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Add(archive, "xl/worksheets/sheet1.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Mã hàng</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Số lượng</t></is></c></row></sheetData></worksheet>");
        }
        stream.Position = 0;
        return stream;
    }

    private static void Add(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open()); writer.Write(content);
    }
}
