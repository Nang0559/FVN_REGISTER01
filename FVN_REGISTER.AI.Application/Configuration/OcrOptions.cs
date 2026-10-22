namespace FVN_REGISTER.AI.Application.Configuration;

public sealed class OcrOptions
{
    public const string SectionName = "AIPlatform:Ocr";
    public string TesseractExecutable { get; set; } = "tesseract";
    public string PdfToPpmExecutable { get; set; } = "pdftoppm";
    public string Languages { get; set; } = "vie+eng";
    public int Dpi { get; set; } = 300;
    /// <summary>Tesseract --psm (3 = automatic layout, 4 = single column, 6 = one uniform block). Try 4 or 3 for letters with headers, tables and stamps.</summary>
    public int PageSegmentationMode { get; set; } = 6;
    public int TimeoutSeconds { get; set; } = 180;
}
