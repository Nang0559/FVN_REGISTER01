using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Extraction;

namespace FVN_REGISTER.AI.Tests;

public sealed class SchemaDrivenDocumentExtractorTests
{
    [Fact]
    public async Task ExtractAsync_DerivesSourceLocatorFromVerbatimEvidence()
    {
        var model = new StubModelProvider("""{"fields":[{"path":"assetCode","value":"FCC-001","confidence":0.97,"sourceText":"Asset code: FCC-001"}]}""");
        var schema = new ExtractionSchema("Equipment", 1, "Equipment",
            [new("assetCode", "string", Required: true)]);
        var text = "[PAGE 2]\nAsset code: FCC-001\n";

        var result = await new SchemaDrivenDocumentExtractor(model).ExtractAsync(text, schema);

        var candidate = Assert.Single(result);
        Assert.Equal("page:2;line:2", candidate.SourceReference);
        Assert.Equal("Asset code: FCC-001", candidate.EvidenceText);
    }

    private sealed class StubModelProvider(string output) : IAIModelProvider
    {
        public string ProviderName => "Test";
        public Task<AIModelResponse> GenerateAsync(AIModelRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AIModelResponse(output, "Test", "test-model", "stop", 10, 10, TimeSpan.Zero));
    }
}
