using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace FVN_REGISTER.Shared.Services.Help;

/// <summary>Cuts sections out of the Markdown user guide and renders them to HTML for <c>/huong-dan</c>.</summary>
public static class GuideMarkdown
{
    /// <summary>Where the guide's screenshots are published by the Web host (see Program.cs / FVN_REGISTER.Web.csproj).</summary>
    public const string ScreenshotsUrl = "/guide-assets/screenshots/";

    private const string ScreenshotsRelative = "../../screenshots/";

    private static readonly Regex Heading = new(@"^(#{1,6})\s+(.*?)\s*$", RegexOptions.Compiled);
    private static readonly Regex NumberedTitle = new(@"^(\d+(?:\.\d+)*)\.\s", RegexOptions.Compiled);

    // Raw HTML inside the .md is not rendered: the file is documentation, never a place for markup or scripts.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAutoLinks()
        .DisableHtml()
        .Build();

    /// <summary>
    /// Returns the section selected by <paramref name="id"/> including its heading and sub-sections: "7.3" selects the heading
    /// numbered "7.3." and "~Some title" selects the heading with exactly that text. Empty when it does not exist.
    /// </summary>
    public static string ExtractSection(string markdown, string id)
    {
        if (string.IsNullOrWhiteSpace(markdown) || string.IsNullOrWhiteSpace(id)) return string.Empty;

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var result = new List<string>();
        var inFence = false;
        var startLevel = 0;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                inFence = !inFence;

            var heading = inFence ? Match.Empty : Heading.Match(line);
            if (heading.Success)
            {
                var level = heading.Groups[1].Length;
                var title = heading.Groups[2].Value;
                if (startLevel == 0)
                {
                    if (Selects(id, title))
                    {
                        startLevel = level;
                        result.Add(line);
                    }
                    continue;
                }
                if (level <= startLevel) break;
            }

            if (startLevel > 0) result.Add(line);
        }

        while (result.Count > 0 && (string.IsNullOrWhiteSpace(result[^1]) || result[^1].Trim() == "---"))
            result.RemoveAt(result.Count - 1);

        return string.Join('\n', result);
    }

    /// <summary>Joins the existing sections of <paramref name="ids"/> (in the given order); ids that do not exist are skipped.</summary>
    public static string ExtractSections(string markdown, IEnumerable<string> ids)
    {
        var builder = new StringBuilder();
        foreach (var id in ids)
        {
            var section = ExtractSection(markdown, id);
            if (section.Length == 0) continue;
            if (builder.Length > 0) builder.Append("\n\n");
            builder.Append(section);
        }
        return builder.ToString();
    }

    /// <summary>Renders Markdown to HTML and points the guide's screenshot links at <see cref="ScreenshotsUrl"/>.</summary>
    public static string ToHtml(string markdown) =>
        Markdown.ToHtml(markdown.Replace(ScreenshotsRelative, ScreenshotsUrl, StringComparison.Ordinal), Pipeline);

    private static bool Selects(string id, string title)
    {
        if (id.StartsWith('~'))
            return string.Equals(title.Trim(), id[1..].Trim(), StringComparison.OrdinalIgnoreCase);
        var numbered = NumberedTitle.Match(title);
        return numbered.Success && string.Equals(numbered.Groups[1].Value, id, StringComparison.Ordinal);
    }
}
