namespace FVN_REGISTER.AI.Application.Contracts;

/// <summary>Module-neutral source categories for every document entering the AI platform.</summary>
public static class DocumentSourceTypes
{
    public const string Upload = "Upload";
    public const string Folder = "Folder";
    public const string Connector = "Connector";
    public const string Api = "Api";
    public const string Unknown = "Unknown";

    public static bool IsSupported(string value) => value is Upload or Folder or Connector or Api or Unknown;
}

public sealed record DocumentIngestionRequest(
    string TenantCode,
    string ActorId,
    string FileName,
    Stream Content,
    string SourceType = DocumentSourceTypes.Upload,
    string? SourceReference = null,
    DateTimeOffset? RetentionUntilUtc = null,
    string? ModuleCode = null,
    string? SchemaCode = null);

public sealed record DocumentSourceMetadata(
    string SourceType,
    string? SourceReference,
    DateTimeOffset? RetentionUntilUtc);

public sealed record DocumentRetentionResult(
    int ArchivedDocuments,
    int DeletedContent,
    DateTimeOffset ExecutedAtUtc);
