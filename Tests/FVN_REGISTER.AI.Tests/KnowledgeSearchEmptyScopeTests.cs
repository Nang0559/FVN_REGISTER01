using FVN_REGISTER.AI.Application.Abstractions;
using FVN_REGISTER.AI.Application.Configuration;
using FVN_REGISTER.AI.Application.Contracts;
using FVN_REGISTER.AI.Infrastructure.Persistence;
using FVN_REGISTER.AI.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.AI.Tests;

public sealed class KnowledgeSearchEmptyScopeTests
{
    [Fact]
    public async Task Empty_authorized_scope_returns_without_embedding_or_vector_search()
    {
        var embeddings = new CountingEmbeddingGenerator();
        var store = new CountingVectorStore();
        var options = new DbContextOptionsBuilder<AiPlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new AiPlatformDbContext(options);
        var service = new KnowledgeSearchService(
            embeddings, store, db, Options.Create(new KnowledgeSearchOptions()));

        var result = await service.SearchAsync(
            "FCCVN", "actor-1", Array.Empty<string>(), "quy định nghỉ phép", 8);

        Assert.Empty(result);
        Assert.Equal(0, embeddings.CallCount);
        Assert.Equal(0, store.CallCount);
    }

    private sealed class CountingEmbeddingGenerator : IEmbeddingGenerator
    {
        public int CallCount { get; private set; }
        public Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new EmbeddingVector("test", Array.Empty<float>()));
        }
    }

    private sealed class CountingVectorStore : IVectorKnowledgeStore
    {
        public int CallCount { get; private set; }
        public Task UpsertAsync(string tenantCode, IReadOnlyCollection<KnowledgeChunk> chunks,
            IReadOnlyCollection<EmbeddingVector> vectors, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<KnowledgeSearchCandidate>> SearchAsync(string tenantCode,
            EmbeddingVector queryVector, int limit, IReadOnlyCollection<string> allowedScopes,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<KnowledgeSearchCandidate>>(Array.Empty<KnowledgeSearchCandidate>());
        }
    }
}
