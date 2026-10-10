namespace FVN_REGISTER.Contract.Dtos.Language;

public sealed class LocalizationEntryDto
{
    public string Key { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Vi { get; set; } = string.Empty;
    public string Ja { get; set; } = string.Empty;
}

public sealed class LocalizationUpsertRequest
{
    public string Key { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Vi { get; set; } = string.Empty;
    public string Ja { get; set; } = string.Empty;
}

public sealed class LocalizationCatalogDto
{
    public List<LocalizationEntryDto> Entries { get; set; } = new();
    public List<string> Modules { get; set; } = new();
    public int ViCount { get; set; }
    public int JaCount { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public sealed class LocalizationImportResultDto
{
    public int Imported { get; set; }
    public int Updated { get; set; }
    public int Created { get; set; }
    public List<string> Errors { get; set; } = new();
}

public sealed class LocalizationAuditFindingDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string SourceFile { get; set; } = string.Empty;
    public int? Line { get; set; }
    public string? Component { get; set; }
    public string? ExistingKey { get; set; }
    public string? DetectedText { get; set; }
    public string? SuggestedKey { get; set; }
    public string? ViValue { get; set; }
    public string? JaValue { get; set; }
    public string? Detail { get; set; }
}

public sealed class LocalizationAuditSummaryDto
{
    public int Total { get; set; }
    public int MissingKey { get; set; }
    public int HardCodedUi { get; set; }
    public int MissingVi { get; set; }
    public int MissingJa { get; set; }
    public int SameValue { get; set; }
    public int UnusedKey { get; set; }
    public int PlaceholderMismatch { get; set; }
}

public sealed class LocalizationAuditResultDto
{
    public LocalizationAuditSummaryDto Summary { get; set; } = new();
    public List<LocalizationAuditFindingDto> Findings { get; set; } = new();
    public DateTime ScannedAt { get; set; }
    public string? SourceRoot { get; set; }
    public bool SourceAvailable { get; set; }
}

/// <summary>
/// The texts every client applies on top of its embedded catalog, so that what the Language Center saved shows on screen
/// without a rebuild. <see cref="Version"/> changes only when a catalog file changes; <see cref="Unchanged"/> means the client
/// already holds this version and the two dictionaries are empty.
/// </summary>
public sealed class LocalizationRuntimeSnapshotDto
{
    public long Version { get; set; }
    public bool Unchanged { get; set; }
    public Dictionary<string, string> Vi { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Ja { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
