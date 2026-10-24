using FVN_REGISTER.AI.Application.Contracts;

namespace FVN_REGISTER.AI.Application.Abstractions;

/// <summary>Finds approved knowledge-base passages the actor is allowed to read, shaped as evidence for the guide assistant.</summary>
public interface IGuideDocumentEvidenceProvider
{
    /// <summary>Never throws for retrieval problems (the guide answer must still work); returns nothing when the actor has no access.</summary>
    Task<IReadOnlyList<GuideDocumentEvidence>> FindAsync(string tenantCode, string actorId, string query, CancellationToken cancellationToken = default);
}
