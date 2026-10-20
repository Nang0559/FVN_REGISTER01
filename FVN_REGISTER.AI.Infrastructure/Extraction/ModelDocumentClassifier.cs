using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Extraction;

/// <summary>Provider-neutral document classification. HR/business module names are intentionally absent.</summary>
public sealed class ModelDocumentClassifier(IAIModelProvider modelProvider) : IDocumentClassifier, IAIModelProvenance
{
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "Form", "Policy", "Procedure", "Report", "Spreadsheet", "Invoice",
        "Correspondence", "TechnicalDocument", "Other", "Unknown"
    };

    public string ProviderName { get; private set; } = modelProvider.ProviderName;
    public string ModelVersion { get; private set; } = modelProvider is IAIModelProvenance p ? p.ModelVersion : "Unconfigured";

    public async Task<(string DocumentType, double Confidence)> ClassifyAsync(
        string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return ("Unknown", 0);
        var sample = text.Length > 12000 ? text[..12000] : text;
        try
        {
            var response = await modelProvider.GenerateAsync(new AIModelRequest(
                "document-classification",
                sample,
                "Classify the document by its content, not by a business module. Return only JSON: " +
                "{\"documentType\":\"Form|Policy|Procedure|Report|Spreadsheet|Invoice|Correspondence|TechnicalDocument|Other|Unknown\",\"confidence\":0.0}. " +
                "Do not infer missing content. Confidence must be between 0 and 1."), cancellationToken);
            ProviderName = response.Provider;
            ModelVersion = response.Model;
            using var json = JsonDocument.Parse(response.Output);
            var type = json.RootElement.GetProperty("documentType").GetString() ?? "Unknown";
            var confidence = json.RootElement.GetProperty("confidence").GetDouble();
            if (!Types.Contains(type) || double.IsNaN(confidence) || double.IsInfinity(confidence) ||
                confidence is < 0 or > 1) return Fallback(text);
            return (type, confidence);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return Fallback(text);
        }
    }

    private static (string DocumentType, double Confidence) Fallback(string text)
    {
        var sample = text[..Math.Min(text.Length, 12000)].ToLowerInvariant();
        if (sample.Contains("chính sách") || sample.Contains("policy")) return ("Policy", 0.65);
        if (sample.Contains("quy trình") || sample.Contains("procedure")) return ("Procedure", 0.65);
        if (sample.Contains("báo cáo") || sample.Contains("report")) return ("Report", 0.6);
        if (sample.Contains("hóa đơn") || sample.Contains("invoice")) return ("Invoice", 0.65);
        if (sample.Contains("đơn đề nghị") || sample.Contains("request form")) return ("Form", 0.6);
        return ("Unknown", 0.35);
    }
}
