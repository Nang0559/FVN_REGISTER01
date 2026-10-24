using System.Text.Json;

namespace FVN_REGISTER.AI.Application.Contracts;

/// <summary>Module-neutral, versioned schema used to extract and validate structured data from any document.</summary>
public sealed record ExtractionSchema(
    string SchemaCode,
    int Version,
    string DisplayName,
    IReadOnlyList<ExtractionFieldDefinition> Fields,
    IReadOnlyList<ExtractionRuleDefinition>? Rules = null,
    IReadOnlyList<ExtractionFieldMapping>? Mappings = null);

public sealed record ExtractionFieldDefinition(
    string Path,
    string DataType,
    bool Required = false,
    int? MinLength = null,
    int? MaxLength = null,
    decimal? Minimum = null,
    decimal? Maximum = null,
    string? Pattern = null,
    IReadOnlyList<string>? AllowedValues = null);

/// <summary>Declarative source-to-target mapping. Source hints guide extraction; TargetPath is the canonical output path.</summary>
public sealed record ExtractionFieldMapping(
    string TargetPath,
    IReadOnlyList<string> SourceHints,
    string Normalization = "None");

public sealed record ExtractionRuleDefinition(
    string Code,
    string Expression,
    string Message,
    string Severity = "Error");

public sealed record ExtractedCandidate(
    string Path,
    JsonElement Value,
    double Confidence,
    string? SourceReference = null,
    string? EvidenceText = null);

public sealed record ExtractionValidationRequest(
    ExtractionSchema Schema,
    IReadOnlyList<ExtractedCandidate> Candidates);

public sealed record ExtractionValidationIssue(
    string Code,
    string Path,
    string Severity,
    string Message);

public sealed record ExtractionValidationResult(
    string SchemaCode,
    int SchemaVersion,
    IReadOnlyList<ExtractedCandidate> AcceptedCandidates,
    IReadOnlyList<ExtractionValidationIssue> Issues,
    bool IsValid);

public sealed record StandardizedExtractionResult(
    string SchemaCode,
    int SchemaVersion,
    IReadOnlyList<ExtractedCandidate> Candidates,
    ExtractionValidationResult Validation,
    string RepresentationVersion,
    string SourceTextSha256,
    string ParserVersion,
    string? ModelProvider,
    string? ModelVersion,
    string? PromptVersion);
