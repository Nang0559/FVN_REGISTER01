using System.Text;
using System.Text.RegularExpressions;

namespace FVN_REGISTER.Contract.Help;

/// <summary>One searchable passage of the Markdown guide. <see cref="FeatureCodes"/> drives who may see it.</summary>
/// <param name="Id">Stable id inside one index build, e.g. "04_USER_GUIDE.md#12".</param>
/// <param name="SectionId">Nearest numbered section ("7.3"), or empty when the passage is not inside a numbered section.</param>
/// <param name="Title">Heading breadcrumb, e.g. "7.3. Cấu hình hạn mức OT › Bước 2".</param>
/// <param name="FeatureCodes">Help feature codes whose guide contains this passage (via <see cref="GuideSectionMap"/>).</param>
/// <param name="General">True for passages every signed-in assistant user may read (quick start, glossary, FAQ).</param>
public sealed record GuideChunk(
    string Id,
    string SourceFile,
    string SectionId,
    string Title,
    IReadOnlyList<string> FeatureCodes,
    bool General,
    string Text);

/// <summary>Cuts the Markdown guide into small passages by heading. Pure function: no file or network access.</summary>
public static class GuideChunker
{
    /// <summary>Level-2 headings of 04_USER_GUIDE.md that are written for everyone.</summary>
    public static readonly IReadOnlyCollection<string> DefaultGeneralTitles = new[]
    {
        "Bắt đầu nhanh — 3 việc nhân viên thường làm",
        "Thuật ngữ thường gặp",
        "Hỗ trợ khi gặp vấn đề",
    };

    /// <summary>Screenshot appendices contain images only; they are not useful as evidence.</summary>
    public static readonly IReadOnlyCollection<string> DefaultExcludedPrefixes = new[] { "Phụ lục" };

    private static readonly Regex Heading = new(@"^(#{1,6})\s+(.*?)\s*$", RegexOptions.Compiled);
    private static readonly Regex NumberedTitle = new(@"^(\d+(?:\.\d+)*)\.\s", RegexOptions.Compiled);
    private static readonly Regex Image = new(@"!\[[^\]]*\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex Link = new(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex ManyBlankLines = new(@"\n{3,}", RegexOptions.Compiled);

    private sealed record Node(int Level, string Title);

    /// <param name="allGeneral">Mark every passage of the file as general (used for the quick guide and the FAQ).</param>
    public static IReadOnlyList<GuideChunk> Chunk(
        string fileName,
        string markdown,
        bool allGeneral,
        IReadOnlyCollection<string>? generalTitles = null,
        IReadOnlyCollection<string>? excludedPrefixes = null,
        int maxChunkChars = 1500)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return Array.Empty<GuideChunk>();
        generalTitles ??= DefaultGeneralTitles;
        excludedPrefixes ??= DefaultExcludedPrefixes;

        var chunks = new List<GuideChunk>();
        var stack = new List<Node>();
        var body = new StringBuilder();
        var inFence = false;

        void Flush()
        {
            var text = Clean(body.ToString());
            body.Clear();
            if (text.Length == 0 || stack.Count == 0) return;

            var level2 = stack.FirstOrDefault(n => n.Level == 2);
            if (level2 is not null && excludedPrefixes.Any(p => level2.Title.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return;

            var ids = new List<string>();
            var sectionId = string.Empty;
            foreach (var node in stack)
            {
                var numbered = NumberedTitle.Match(node.Title);
                if (numbered.Success) { ids.Add(numbered.Groups[1].Value); sectionId = numbered.Groups[1].Value; }
                ids.Add("~" + node.Title.Trim());
            }
            var features = ids.SelectMany(id => GuideSectionMap.FeaturesFor(id)).Distinct(StringComparer.Ordinal).ToArray();
            var general = allGeneral || level2 is null || generalTitles.Any(t => string.Equals(t, level2.Title.Trim(), StringComparison.OrdinalIgnoreCase));
            var path = stack.Select(n => Plain(n.Title)).ToList();
            var title = string.Join(" › ", path.Skip(path.Count > 1 ? 1 : 0));

            foreach (var part in Split(text, maxChunkChars))
                chunks.Add(new GuideChunk($"{fileName}#{chunks.Count}", fileName, sectionId, title, features, general, part));
        }

        foreach (var line in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                body.Append(line).Append('\n');
                continue;
            }
            var heading = inFence ? Match.Empty : Heading.Match(line);
            if (heading.Success)
            {
                Flush();
                var level = heading.Groups[1].Length;
                stack.RemoveAll(n => n.Level >= level);
                stack.Add(new Node(level, heading.Groups[2].Value.Trim()));
                continue;
            }
            body.Append(line).Append('\n');
        }
        Flush();
        return chunks;
    }

    private static string Clean(string text)
    {
        text = Image.Replace(text, string.Empty);
        text = Link.Replace(text, "$1");
        var lines = text.Split('\n').Where(l => l.Trim() != "---");
        return ManyBlankLines.Replace(string.Join('\n', lines), "\n\n").Trim();
    }

    private static string Plain(string title) => title.Replace("**", string.Empty).Replace("`", string.Empty).Trim();

    private static IEnumerable<string> Split(string text, int max)
    {
        if (text.Length <= max) { yield return text; yield break; }
        var current = new StringBuilder();
        foreach (var paragraph in text.Split("\n\n"))
        {
            if (current.Length > 0 && current.Length + paragraph.Length + 2 > max)
            {
                yield return current.ToString().Trim();
                current.Clear();
            }
            // A single paragraph (or table) larger than the limit is cut on line boundaries.
            if (paragraph.Length > max)
            {
                foreach (var line in paragraph.Split('\n'))
                {
                    if (current.Length > 0 && current.Length + line.Length + 1 > max)
                    {
                        yield return current.ToString().Trim();
                        current.Clear();
                    }
                    current.Append(line).Append('\n');
                }
                current.Append('\n');
                continue;
            }
            current.Append(paragraph).Append("\n\n");
        }
        if (current.ToString().Trim().Length > 0) yield return current.ToString().Trim();
    }
}
