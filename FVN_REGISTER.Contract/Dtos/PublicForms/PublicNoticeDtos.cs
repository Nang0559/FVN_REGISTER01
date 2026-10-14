namespace FVN_REGISTER.Contract.Dtos.PublicForms;

/// <summary>
/// Anonymous counter for the login page / public portal: how many public notices and registration forms are
/// effective right now. Computed server-side with the same filters as the lists, so the badge always matches the page.
/// </summary>
public sealed class PublicNoticeSummaryDto
{
    /// <summary>Published public information whose effective window contains "now".</summary>
    public int InformationCount { get; set; }

    /// <summary>Published, active registration forms open to the whole company (AllCompany audience) and inside their time window.</summary>
    public int FormCount { get; set; }

    public int TotalCount => InformationCount + FormCount;

    /// <summary>At least one effective notice is flagged important (UI switches the badge to the alert colour).</summary>
    public bool HasImportant { get; set; }

    public DateTime? LatestPublishedAt { get; set; }
}

/// <summary>Minimal projection of a company-wide registration form that anonymous visitors may see (no questions, no audience).</summary>
public sealed class PublicFormNoticeDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
}
