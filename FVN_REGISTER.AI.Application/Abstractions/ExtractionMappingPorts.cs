using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Application.Abstractions;

public interface IExtractionFieldMappingService
{
    IReadOnlyList<ExtractedCandidate> Apply(
        ExtractionSchema schema,
        IReadOnlyList<ExtractedCandidate> candidates);
}
