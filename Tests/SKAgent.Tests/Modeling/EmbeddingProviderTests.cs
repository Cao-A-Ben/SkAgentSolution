using SKAgent.Application.Modeling;
using SKAgent.Core.Modeling;
using SKAgent.Infrastructure.Memory.Embedding;
using Xunit;

namespace SKAgent.Tests.Modeling;

public sealed class EmbeddingProviderTests
{
    [Fact]
    public async Task CreateEmbeddingAsync_ShouldHonorConfiguredLocalHashRoute()
    {
        var provider = new EmbeddingProvider(
            new DefaultModelRouter(new ModelRoutingOptions
            {
                Embedding = new ModelRouteOptions
                {
                    Provider = "local",
                    Model = "hash-embedding-v1-256",
                    Reason = "embedding_configured"
                }
            }),
            dimension: 128);

        var vector = await provider.CreateEmbeddingAsync("week8.5");

        Assert.Equal("hash-embedding-v1-256", provider.ModelId);
        Assert.Equal(256, vector.Length);
    }

    [Fact]
    public async Task CreateEmbeddingAsync_ShouldFallbackToDefaultHash_WhenNonLocalRouteConfigured()
    {
        var provider = new EmbeddingProvider(
            new DefaultModelRouter(new ModelRoutingOptions
            {
                Embedding = new ModelRouteOptions
                {
                    Provider = "openai-compatible",
                    Model = "text-embedding-3-small",
                    Reason = "future_remote_embedding"
                }
            }),
            dimension: 128);

        var vector = await provider.CreateEmbeddingAsync("week8.5");

        Assert.Equal("hash-embedding-v1-128", provider.ModelId);
        Assert.Equal(128, vector.Length);
    }
}
