using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Application.Abstractions;

public interface IDocumentRepresentationExtractor
{
    Task<DocumentRepresentation> ExtractAsync(
        string text,
        string mediaType,
        CancellationToken cancellationToken = default);
}
