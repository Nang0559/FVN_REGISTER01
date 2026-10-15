using System.Text.Json;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Validation;

namespace FVN_REGISTER.AI.Tests;

public sealed class ExtractionValidationTests
{
    [Fact]
    public async Task ValidateAsync_ReportsMissingRequiredAndTypeMismatch()
    {
        using var doc = JsonDocument.Parse("\"not-a-number\"");
        var schema = new ExtractionSchema("EquipmentRequest", 1, "Equipment request",
        [
            new("quantity", "integer", Required: true, Minimum: 1, Maximum: 100),
            new("assetCode", "string", Required: true, MaxLength: 30)
        ]);
        var candidates = new[] { new ExtractedCandidate("quantity", doc.RootElement.Clone(), 0.99) };

        var result = await new ExtractionValidationService().ValidateAsync(new(schema, candidates));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == "FIELD_TYPE_MISMATCH" && x.Path == "quantity");
        Assert.Contains(result.Issues, x => x.Code == "REQUIRED_FIELD_MISSING" && x.Path == "assetCode");
    }

    [Fact]
    public async Task ValidateAsync_AcceptsValidFieldsAndChecksCrossFieldRule()
    {
        using var first = JsonDocument.Parse("\"FCC-001\"");
        using var second = JsonDocument.Parse("12");
        var schema = new ExtractionSchema("Generic", 2, "Generic",
        [
            new("code", "string", Required: true, MinLength: 3),
            new("quantity", "integer", Required: true, Minimum: 1)
        ],
        [new("REQUIRED_TOGETHER", "code,quantity", "Both fields must be supplied.")]);
        var candidates = new[]
        {
            new ExtractedCandidate("code", first.RootElement.Clone(), 0.95),
            new ExtractedCandidate("quantity", second.RootElement.Clone(), 0.91)
        };

        var result = await new ExtractionValidationService().ValidateAsync(new(schema, candidates));

        Assert.True(result.IsValid);
        Assert.Equal(2, result.AcceptedCandidates.Count);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ValidateAsync_RejectsConfidenceOutsideZeroToOne()
    {
        using var value = JsonDocument.Parse("true");
        var schema = new ExtractionSchema("Boolean", 1, "Boolean", [new("enabled", "boolean")]);

        var result = await new ExtractionValidationService().ValidateAsync(new(schema,
            [new ExtractedCandidate("enabled", value.RootElement.Clone(), 1.5)]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == "INVALID_CONFIDENCE");
    }
}
