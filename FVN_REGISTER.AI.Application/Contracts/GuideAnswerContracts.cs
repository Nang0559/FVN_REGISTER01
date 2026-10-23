namespace FVN_REGISTER.AI.Application.Contracts;

/// <summary>One passage of the user guide, selected and permission-filtered by the trusted FVN host (never by the browser).</summary>
/// <param name="Id">Short tag the model uses to cite the passage, "S1".."S6".</param>
public sealed record GuideEvidence(string Id, string Title, string Text);

public sealed record GuideTurn(string Role, string Content);

public sealed record GuideAnswerRequest(
    string Query,
    string? Feature,
    IReadOnlyList<GuideTurn>? History,
    IReadOnlyList<GuideEvidence> Evidence);

/// <param name="Status">Supported | PartiallySupported | NotFound.</param>
/// <param name="CitedEvidenceIds">Evidence ids the answer refers to; all supplied ids when the model did not tag its sources.</param>
public sealed record GuideAnswerResult(
    string Answer,
    string Status,
    IReadOnlyList<string> CitedEvidenceIds,
    string? ModelProvider,
    string? ModelVersion,
    long ElapsedMilliseconds);
