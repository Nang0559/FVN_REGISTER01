using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FVN_REGISTER.Contract.Help;

public sealed record GuideSearchHit(GuideChunk Chunk, double Score);

/// <summary>
/// Small BM25 search over guide passages. It folds Vietnamese diacritics ("đăng ký" and "dang ky" match) and also matches
/// word pairs, which matters because Vietnamese terms are made of several syllables. It needs no model and no embedding call,
/// so retrieval costs a few milliseconds and the model only runs when something relevant was found.
/// </summary>
public static class GuideSearch
{
    private const double K1 = 1.2;
    private const double B = 0.75;
    private const double FeatureBoost = 1.4;
    private const double PairWeight = 1.5;
    private const int TitleWeight = 3;
    private const int MinimumPartialChars = 300;

    // "dang" and "ma" are deliberately NOT stop words: after folding they also mean "đăng" (register) and "mã" (code).
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "la", "va", "cua", "cho", "the", "nao", "khong", "co", "toi", "can", "khi", "de", "duoc", "nhu", "gi",
        "trong", "voi", "mot", "cac", "nhung", "thi", "bi", "hay", "nay", "ai", "sao", "ban", "minh", "xin", "vui",
        "cach", "hoi", "what", "how", "is", "are", "of", "and", "to", "for", "an", "in", "on"
    };

    private static readonly Regex Word = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);

    /// <summary>Lower-cases and removes diacritics; "Đăng ký" becomes "dang ky".</summary>
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>Single words followed by pairs of neighbouring words ("nghi phep").</summary>
    public static List<string> Tokens(string text)
    {
        var words = Word.Matches(Fold(text)).Select(m => m.Value)
            .Where(w => (w.Length > 1 || char.IsDigit(w[0])) && !StopWords.Contains(w))
            .ToList();
        var result = new List<string>(words);
        for (var i = 0; i + 1 < words.Count; i++) result.Add(words[i] + " " + words[i + 1]);
        return result;
    }

    /// <summary>
    /// Ranks the visible passages for <paramref name="query"/> and returns the best ones whose total text stays within
    /// <paramref name="maxChars"/>. Passages of <paramref name="currentFeature"/> get a boost. Returns nothing when no
    /// visible passage matches a word pair of the query (or every word of a very short query).
    /// </summary>
    public static IReadOnlyList<GuideSearchHit> Search(
        IReadOnlyList<GuideChunk> chunks,
        string query,
        Func<GuideChunk, bool> isVisible,
        string? currentFeature,
        int take,
        int maxChars)
    {
        var visible = chunks.Where(isVisible).ToArray();
        var queryTokens = Tokens(query).Distinct(StringComparer.Ordinal).ToArray();
        if (visible.Length == 0 || queryTokens.Length == 0 || take <= 0 || maxChars <= 0) return Array.Empty<GuideSearchHit>();

        var docs = visible.Select(chunk =>
        {
            var tf = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var token in Tokens(chunk.Title)) tf[token] = tf.GetValueOrDefault(token) + TitleWeight;
            foreach (var token in Tokens(chunk.Text)) tf[token] = tf.GetValueOrDefault(token) + 1;
            return (Chunk: chunk, Tf: tf, Length: tf.Values.Sum());
        }).ToArray();

        var queryWords = queryTokens.Where(t => !t.Contains(' ')).ToArray();
        var averageLength = Math.Max(1.0, docs.Average(d => (double)d.Length));
        var scored = new List<GuideSearchHit>();
        foreach (var doc in docs)
        {
            var score = 0.0;
            foreach (var token in queryTokens)
            {
                if (!doc.Tf.TryGetValue(token, out var frequency)) continue;
                var documentFrequency = docs.Count(d => d.Tf.ContainsKey(token));
                var idf = Math.Log(1 + (docs.Length - documentFrequency + 0.5) / (documentFrequency + 0.5));
                var weight = token.Contains(' ') ? PairWeight : 1.0;
                score += weight * idf * frequency * (K1 + 1) / (frequency + K1 * (1 - B + B * doc.Length / averageLength));
            }
            if (score <= 0) continue;
            // Folding diacritics makes unrelated words collide ("hôm"/"hom"), so a few single-word matches are not enough:
            // require a matching word pair, or (for one- and two-word questions) every word.
            var matchedWords = queryWords.Count(t => doc.Tf.ContainsKey(t));
            var matchedPair = queryTokens.Any(t => t.Contains(' ') && doc.Tf.ContainsKey(t));
            if (!matchedPair && !(queryWords.Length <= 2 && matchedWords == queryWords.Length)) continue;
            if (currentFeature is not null && doc.Chunk.FeatureCodes.Contains(currentFeature, StringComparer.Ordinal)) score *= FeatureBoost;
            scored.Add(new GuideSearchHit(doc.Chunk, score));
        }

        var picked = new List<GuideSearchHit>();
        var used = 0;
        foreach (var hit in scored.OrderByDescending(h => h.Score))
        {
            if (picked.Count >= take) break;
            var remaining = maxChars - used;
            if (remaining <= 0) break;
            if (hit.Chunk.Text.Length <= remaining)
            {
                picked.Add(hit);
                used += hit.Chunk.Text.Length;
            }
            else if (picked.Count == 0 || remaining >= MinimumPartialChars)
            {
                picked.Add(hit with { Chunk = hit.Chunk with { Text = hit.Chunk.Text[..remaining] } });
                used += remaining;
            }
        }
        return picked;
    }
}
