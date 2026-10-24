using System.Text.RegularExpressions;
using FVN_REGISTER.Shared.Services.Help;
using Microsoft.Maui.Storage;

namespace FVN_REGISTER.Services;

/// <summary>
/// Loads the packaged user guide and packages screenshot images as data URLs so the same Markdown renderer works in
/// the MAUI BlazorWebView without relying on Web-server static-file routes.
/// </summary>
public sealed class MauiGuideDocumentProvider : IGuideDocumentProvider
{
    private static readonly Regex SafeFileName = new(@"^[A-Za-z0-9_\-]+\.md$", RegexOptions.Compiled);
    private static readonly Regex ScreenshotReference = new(@"\.\./\.\./screenshots/([A-Za-z0-9_.\-]+\.(?:png|jpe?g|webp|gif))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<string?> GetMarkdownAsync(string fileName, CancellationToken cancellationToken = default)
    {
        // The API accepts only a bare Markdown file name; it cannot be used to read arbitrary packaged files.
        if (string.IsNullOrWhiteSpace(fileName) || !SafeFileName.IsMatch(fileName))
            return null;

        string markdown;
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
            using var reader = new StreamReader(stream);
            markdown = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        // Markdown references are relative to the source guide folder. Embed only referenced images so they work on
        // Android, iOS, Mac Catalyst and Windows without needing a server endpoint.
        var images = ScreenshotReference.Matches(markdown)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var imageName in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var imageStream = await FileSystem.OpenAppPackageFileAsync($"screenshots/{imageName}");
                using var memory = new MemoryStream();
                await imageStream.CopyToAsync(memory, cancellationToken);
                var mimeType = GetMimeType(imageName);
                var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(memory.ToArray())}";
                markdown = markdown.Replace($"../../screenshots/{imageName}", dataUrl, StringComparison.OrdinalIgnoreCase);
            }
            catch (FileNotFoundException)
            {
                // Keep the original link if an optional screenshot is not packaged; the guide text still renders.
            }
            catch (DirectoryNotFoundException)
            {
                // Keep the original link if the screenshots folder is absent.
            }
        }

        return markdown;
    }

    private static string GetMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png"
        };
}