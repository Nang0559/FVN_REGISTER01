using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Infrastructure.Validation;

/// <summary>
/// Deterministic, provider-independent validation. It never silently coerces invalid values
/// and preserves the original candidate/evidence for human review.
/// </summary>
public sealed class ExtractionValidationService : IExtractionValidationService
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
        { "string", "integer", "decimal", "boolean", "date", "datetime", "time", "email" };

    public Task<ExtractionValidationResult> ValidateAsync(
        ExtractionValidationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Schema);
        ArgumentNullException.ThrowIfNull(request.Candidates);
        ArgumentNullException.ThrowIfNull(request.Schema.Fields);
        if (string.IsNullOrWhiteSpace(request.Schema.SchemaCode) || request.Schema.SchemaCode.Length > 128)
            throw new ArgumentException("SchemaCode must contain 1 to 128 characters.");
        if (request.Schema.Version < 1) throw new ArgumentException("Schema version must be positive.");
        if (request.Schema.Fields.Count > 500) throw new ArgumentException("A schema cannot contain more than 500 fields.");
        if (request.Candidates.Count > 5000) throw new ArgumentException("An extraction cannot contain more than 5000 candidates.");

        var issues = new List<ExtractionValidationIssue>();
        var fields = request.Schema.Fields.ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);
        var candidates = new Dictionary<string, ExtractedCandidate>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in request.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(candidate.Path) || !fields.ContainsKey(candidate.Path))
            {
                issues.Add(new("FIELD_NOT_IN_SCHEMA", candidate.Path ?? "", "Warning",
                    "The extracted field is not defined by the selected schema."));
                continue;
            }
            if (!candidates.TryAdd(candidate.Path, candidate))
            {
                issues.Add(new("DUPLICATE_FIELD", candidate.Path, "Error",
                    "More than one candidate was returned for a field that expects one value."));
                continue;
            }
            if (double.IsNaN(candidate.Confidence) || double.IsInfinity(candidate.Confidence) ||
                candidate.Confidence < 0 || candidate.Confidence > 1)
                issues.Add(new("INVALID_CONFIDENCE", candidate.Path, "Error",
                    "Confidence must be a finite number between 0 and 1."));
        }

        foreach (var field in request.Schema.Fields)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!SupportedTypes.Contains(field.DataType))
            {
                issues.Add(new("UNSUPPORTED_SCHEMA_TYPE", field.Path, "Error",
                    $"Schema data type '{field.DataType}' is not supported."));
                continue;
            }
            if (!candidates.TryGetValue(field.Path, out var candidate))
            {
                if (field.Required)
                    issues.Add(new("REQUIRED_FIELD_MISSING", field.Path, "Error", "A required field was not extracted."));
                continue;
            }
            ValidateField(field, candidate.Value, issues);
        }

        ValidateRules(request.Schema.Rules, candidates, issues);
        var accepted = request.Candidates
            .Where(x => fields.ContainsKey(x.Path) && !issues.Any(i => i.Path.Equals(x.Path, StringComparison.OrdinalIgnoreCase) && i.Severity == "Error"))
            .ToArray();
        var valid = issues.All(x => !x.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(new ExtractionValidationResult(
            request.Schema.SchemaCode, request.Schema.Version, accepted, issues, valid));
    }

    private static void ValidateField(ExtractionFieldDefinition field, JsonElement value,
        ICollection<ExtractionValidationIssue> issues)
    {
        void Add(string code, string message) => issues.Add(new(code, field.Path, "Error", message));
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            if (field.Required) Add("REQUIRED_FIELD_EMPTY", "A required field cannot be null.");
            return;
        }

        string? text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        if (field.DataType.Equals("string", StringComparison.OrdinalIgnoreCase) ||
            field.DataType.Equals("email", StringComparison.OrdinalIgnoreCase))
        {
            if (value.ValueKind != JsonValueKind.String) { Add("FIELD_TYPE_MISMATCH", "Expected a string value."); return; }
            text ??= "";
            if (field.MinLength.HasValue && text.Length < field.MinLength) Add("MIN_LENGTH", $"Value must contain at least {field.MinLength} characters.");
            if (field.MaxLength.HasValue && text.Length > field.MaxLength) Add("MAX_LENGTH", $"Value must contain no more than {field.MaxLength} characters.");
            if (field.DataType.Equals("email", StringComparison.OrdinalIgnoreCase) &&
                !Regex.IsMatch(text, @"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                Add("INVALID_EMAIL", "Value is not a valid email address.");
            if (!string.IsNullOrEmpty(field.Pattern))
            {
                try
                {
                    if (!Regex.IsMatch(text, field.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                        Add("PATTERN_MISMATCH", "Value does not match the schema pattern.");
                }
                catch (ArgumentException) { Add("INVALID_SCHEMA_PATTERN", "The schema contains an invalid regular expression."); }
                catch (RegexMatchTimeoutException) { Add("PATTERN_TIMEOUT", "Pattern validation exceeded its time limit."); }
            }
        }
        else if (field.DataType.Equals("integer", StringComparison.OrdinalIgnoreCase) ||
                 field.DataType.Equals("decimal", StringComparison.OrdinalIgnoreCase))
        {
            if (value.ValueKind != JsonValueKind.Number ||
                !decimal.TryParse(value.GetRawText(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            { Add("FIELD_TYPE_MISMATCH", "Expected a JSON number."); return; }
            if (field.DataType.Equals("integer", StringComparison.OrdinalIgnoreCase) && number != decimal.Truncate(number))
                Add("INTEGER_REQUIRED", "Value must be a whole number.");
            if (field.Minimum.HasValue && number < field.Minimum) Add("BELOW_MINIMUM", $"Value must be at least {field.Minimum}.");
            if (field.Maximum.HasValue && number > field.Maximum) Add("ABOVE_MAXIMUM", $"Value must be no more than {field.Maximum}.");
        }
        else if (field.DataType.Equals("boolean", StringComparison.OrdinalIgnoreCase) && value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            Add("FIELD_TYPE_MISMATCH", "Expected a boolean value.");
        else if (field.DataType.Equals("date", StringComparison.OrdinalIgnoreCase) &&
                 (value.ValueKind != JsonValueKind.String || !DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
            Add("INVALID_DATE", "Expected a date in an unambiguous ISO-compatible format.");
        else if (field.DataType.Equals("datetime", StringComparison.OrdinalIgnoreCase) &&
                 (value.ValueKind != JsonValueKind.String || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)))
            Add("INVALID_DATETIME", "Expected an ISO date-time with an explicit offset.");
        else if (field.DataType.Equals("time", StringComparison.OrdinalIgnoreCase) &&
                 (value.ValueKind != JsonValueKind.String || !TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
            Add("INVALID_TIME", "Expected a valid time value.");

        if (field.AllowedValues is { Count: > 0 } allowed &&
            !allowed.Contains(text ?? "", StringComparer.OrdinalIgnoreCase))
            Add("VALUE_NOT_ALLOWED", "Value is not in the schema's allowed value set.");
    }

    private static void ValidateRules(IReadOnlyList<ExtractionRuleDefinition>? rules,
        IReadOnlyDictionary<string, ExtractedCandidate> candidates,
        ICollection<ExtractionValidationIssue> issues)
    {
        if (rules is null) return;
        foreach (var rule in rules)
        {
            var parts = rule.Expression.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || parts.Length > 10)
            {
                issues.Add(new(rule.Code, "", "Error", "Rule expression must contain between 2 and 10 field paths."));
                continue;
            }
            var present = parts.Count(p => candidates.TryGetValue(p, out var c) &&
                c.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) &&
                !(c.Value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(c.Value.GetString())));
            var expression = rule.Code switch
            {
                "REQUIRED_TOGETHER" => present == 0 || present == parts.Length,
                "MUTUALLY_EXCLUSIVE" => present <= 1,
                _ => (bool?)null
            };
            if (expression is null)
                issues.Add(new(rule.Code, "", "Warning", "Unknown cross-field rule was not evaluated."));
            else if (!expression.Value)
                issues.Add(new(rule.Code, string.Join(",", parts), rule.Severity, rule.Message));
        }
    }
}
