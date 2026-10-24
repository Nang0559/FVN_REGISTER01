namespace FVN_REGISTER.AI.Application.Abstractions;

public interface IDocumentSourceConnector
{
    string ConnectorType { get; }
    Task<int> IngestAvailableAsync(CancellationToken cancellationToken = default);
}
