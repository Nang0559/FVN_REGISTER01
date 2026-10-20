using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Extraction;

public sealed class SchemaDrivenDocumentExtractor(IAIModelProvider modelProvider) : ISchemaDrivenDocumentExtractor, IAIModelProvenance
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public string ProviderName { get; private set; } = modelProvider.ProviderName;
    public string ModelVersion { get; private set; } = modelProvider is IAIModelProvenance p ? p.ModelVersion : "Unconfigured";

    public async Task<IReadOnlyList<ExtractedCandidate>> ExtractAsync(string text, ExtractionSchema schema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(schema.Fields);
        if(string.IsNullOrWhiteSpace(text)||text.Length>100_000)throw new ArgumentException("Document text must contain 1 to 100000 characters.",nameof(text));
        if(schema.Fields.Count is <1 or >500)throw new ArgumentException("Schema must define between 1 and 500 fields.",nameof(schema));
        var schemaJson=JsonSerializer.Serialize(new{schema.SchemaCode,schema.Version,fields=schema.Fields.Select(x=>new{x.Path,x.DataType,x.Required,x.MinLength,x.MaxLength,x.Minimum,x.Maximum,x.Pattern,x.AllowedValues}),mappings=schema.Mappings?.Select(x=>new{x.TargetPath,x.SourceHints,x.Normalization})},JsonOptions);
        using var schemaDocument=JsonDocument.Parse(schemaJson);var input=JsonSerializer.Serialize(new{schema=schemaDocument.RootElement,documentText=text},JsonOptions);
        var response=await modelProvider.GenerateAsync(new AIModelRequest("schema-driven-extraction",input,"Extract only values supported by the supplied document text. Follow the supplied target field paths and source hints. Do not invent or infer missing values. Return only JSON in this shape: {\"fields\":[{\"path\":\"field.path\",\"value\":null,\"confidence\":0.0,\"sourceText\":\"exact verbatim excerpt from the document\"}]}. Use JSON-native values. Omit fields with no evidence. Confidence must be between 0 and 1. Do not apply business decisions or modify source data."),cancellationToken);
        ProviderName=response.Provider;ModelVersion=response.Model;
        using var json=JsonDocument.Parse(response.Output);if(!json.RootElement.TryGetProperty("fields",out var fields)||fields.ValueKind!=JsonValueKind.Array||fields.GetArrayLength()>schema.Fields.Count*2)throw new InvalidDataException("STRUCTURED_EXTRACTION_RESPONSE_INVALID");
        var candidates=new List<ExtractedCandidate>();foreach(var field in fields.EnumerateArray()){cancellationToken.ThrowIfCancellationRequested();if(!field.TryGetProperty("path",out var pathElement)||pathElement.ValueKind!=JsonValueKind.String||!field.TryGetProperty("value",out var value)||!field.TryGetProperty("confidence",out var confidenceElement)||!confidenceElement.TryGetDouble(out var confidence)||!double.IsFinite(confidence)||confidence is <0 or >1)throw new InvalidDataException("STRUCTURED_EXTRACTION_FIELD_INVALID");var path=pathElement.GetString()!;if(!schema.Fields.Any(x=>x.Path.Equals(path,StringComparison.OrdinalIgnoreCase)))continue;var evidenceText=field.TryGetProperty("sourceText",out var evidenceElement)&&evidenceElement.ValueKind==JsonValueKind.String?evidenceElement.GetString():null;candidates.Add(new ExtractedCandidate(path,value.Clone(),confidence,FindSourceReference(text,evidenceText),evidenceText));}return candidates;
    }
    private static string? FindSourceReference(string documentText,string? evidenceText){if(string.IsNullOrWhiteSpace(evidenceText)||evidenceText.Length is <5 or >1000)return null;var index=documentText.IndexOf(evidenceText,StringComparison.Ordinal);if(index<0)return null;var lineNumber=1+documentText[..index].Count(ch=>ch=='\n');var pageNumber=1;var pageMarker=System.Text.RegularExpressions.Regex.Matches(documentText[..index],@"\[PAGE (?<page>\d+)\]");if(pageMarker.Count>0&&int.TryParse(pageMarker[pageMarker.Count-1].Groups["page"].Value,out var parsedPage))pageNumber=parsedPage;return $"page:{pageNumber};line:{lineNumber}";}
}
