namespace FVN_REGISTER.Shared.Services.Help;

/// <summary>
/// Supplies the Markdown user guide to <c>/huong-dan</c>. Hosts that can read the guide files (the Web server) register an
/// implementation; hosts that cannot (e.g. the MAUI shell) simply do not, and the page falls back to the short overview in
/// <c>Localization/*.help.json</c>.
/// </summary>
public interface IGuideDocumentProvider
{
    /// <summary>Returns the text of <paramref name="fileName"/> (a bare file name such as <c>04_USER_GUIDE.md</c>), or <c>null</c> when it is not available.</summary>
    Task<string?> GetMarkdownAsync(string fileName, CancellationToken cancellationToken = default);
}
