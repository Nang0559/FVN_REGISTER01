using System.Globalization;
using System.Text.Json;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Extraction;

/// <summary>Applies only deterministic, schema-declared normalizations after model extraction and before validation.</summary>
public sealed class ExtractionFieldMappingService : IExtractionFieldMappingService
{
    public IReadOnlyList<ExtractedCandidate> Apply(ExtractionSchema schema, IReadOnlyList<ExtractedCandidate> candidates)
    {
        if (schema.Mappings is null || schema.Mappings.Count == 0) return candidates;
        var mappings = schema.Mappings.ToDictionary(x => x.TargetPath, StringComparer.OrdinalIgnoreCase);
        var result = new List<ExtractedCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (!mappings.TryGetValue(candidate.Path, out var mapping) || mapping.Normalization.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(candidate);
                continue;
            }
            if (candidate.Value.ValueKind != JsonValueKind.String)
            {
                result.Add(candidate);
                continue;
            }
            var normalized = Normalize(candidate.Value.GetString() ?? "", mapping.Normalization);
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(normalized));
            result.Add(candidate with { Value = doc.RootElement.Clone() });
        }
        return result;
    }

    private static string Normalize(string value, string normalization) => normalization.ToLowerInvariant() switch
    {
        "trim" => value.Trim(),
        "upper" => value.Trim().ToUpperInvariant(),
        "lower" => value.Trim().ToLowerInvariant(),
        "digitsonly" => new string(value.Where(char.IsDigit).ToArray()),
        "isodate" when DateOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        "none" => value,
        _ => throw new ArgumentException($"Unsupported extraction normalization '{normalization}'.")
    };
}
