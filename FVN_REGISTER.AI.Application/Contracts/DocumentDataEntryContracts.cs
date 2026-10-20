using System.Text.Json;

namespace FVN_REGISTER.AI.Application.Contracts;

/// <summary>Template-neutral definition of a destination spreadsheet. The same contract can serve HR, production and quality documents.</summary>
public sealed record DataEntryTemplateDefinition(
    string WorksheetName,
    int HeaderRow,
    int DataStartRow,
    IReadOnlyList<DataEntryColumnDefinition> Columns);

public sealed record DataEntryColumnDefinition(
    string Path,
    string Header,
    string ColumnLetter,
    string DataType = "string",
    bool Required = false,
    IReadOnlyList<string>? SourceHints = null,
    string? NumberFormat = null);

public sealed record DataEntryRunReceipt(
    Guid RunId,
    Guid DocumentId,
    string TemplateCode,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record DataEntryTemplateView(
    Guid Id,
    string TemplateCode,
    string DisplayName,
    string ModuleCode,
    string SchemaCode,
    int Version,
    DataEntryTemplateDefinition Definition,
    DateTimeOffset UpdatedAtUtc);

public sealed record DataEntryRunView(
    Guid RunId,
    Guid DocumentId,
    string FileName,
    string TemplateCode,
    string Status,
    int AttemptCount,
    string? CandidateRowsJson,
    string? ValidationJson,
    string? ConfirmedRowsJson,
    string? Comment,
    string? FailureCode,
    string? ModelProvider,
    string? ModelVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record StoredDataEntryFile(
    string FileName,
    string ContentType,
    Stream Content,
    long Length);

public sealed record DataEntryExtraction(
    IReadOnlyList<DataEntryRow> Rows,
    string? Notes = null);

public sealed record DataEntryRow(
    IReadOnlyDictionary<string, JsonElement> Values,
    IReadOnlyDictionary<string, double>? Confidence = null,
    IReadOnlyDictionary<string, string>? Evidence = null);
