namespace FVN_REGISTER.AI.Application.Configuration;

public sealed class OcrOptions
{
    public const string SectionName = "AIPlatform:Ocr";
    public string TesseractExecutable { get; set; } = "tesseract";
    public string PdfToPpmExecutable { get; set; } = "pdftoppm";
    public string Languages { get; set; } = "vie+eng+jpn";
    public int Dpi { get; set; } = 200;
    public int TimeoutSeconds { get; set; } = 180;
}
