namespace FVN_REGISTER.Shared.Services.Help;

/// <summary>
/// Supplies the Markdown user guide to <c>/huong-dan</c>. The Web host reads it from the published Guide folder; the MAUI
/// host reads the packaged Markdown and embeds screenshot assets so the same FeatureGuide page works in both hosts.
/// </summary>
public interface IGuideDocumentProvider
{
    /// <summary>Returns the text of <paramref name="fileName"/> (a bare file name such as <c>04_USER_GUIDE.md</c>), or <c>null</c> when it is not available.</summary>
    Task<string?> GetMarkdownAsync(string fileName, CancellationToken cancellationToken = default);
}
