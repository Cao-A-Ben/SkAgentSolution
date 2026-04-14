using SKAgent.Application.Modeling;
using SKAgent.Application.Retrieval;
using SKAgent.Core.Memory;
using SKAgent.Core.Modeling;
using SKAgent.Core.Runtime;
using Xunit;

namespace SKAgent.Tests.Retrieval;

public sealed class RetrievalRerankerTests
{
    [Fact]
    public async Task RerankAsync_ShouldUseReturnedOrder_AndEmitRerankEvents()
    {
        var textGeneration = new TestTextGenerationService("C2,C1");
        var reranker = new RetrievalReranker(
            textGeneration,
            new DefaultModelRouter(new ModelRoutingOptions()));

        var run = new TestRunContext("继续推进 Week8.5");
        var candidates = new[]
        {
            new MemoryItem("vec:1", MemoryLayer.LongTerm, "相关历史用户原话：先看文档", DateTimeOffset.UtcNow.AddMinutes(-2), 0.7, new Dictionary<string, string>{{"route","vector"}}),
            new MemoryItem("vec:2", MemoryLayer.LongTerm, "相关历史用户原话：先完成最关键的优化点", DateTimeOffset.UtcNow.AddMinutes(-1), 0.6, new Dictionary<string, string>{{"route","vector"}})
        };

        var ranked = await reranker.RerankAsync(run, "我想继续推进 Week8.5", candidates, 2, CancellationToken.None);

        Assert.Equal("vec:2", ranked[0].Id);
        Assert.Equal("vec:1", ranked[1].Id);
        Assert.Contains(run.Events, e => e.Type == "model_selected");
        Assert.Contains(run.Events, e => e.Type == "rerank_applied");
        Assert.Equal(1, textGeneration.CallCount);
    }

    [Fact]
    public async Task RerankAsync_ShouldFallback_WhenResponseCannotBeParsed()
    {
        var textGeneration = new TestTextGenerationService("not-a-ranking");
        var reranker = new RetrievalReranker(
            textGeneration,
            new DefaultModelRouter(new ModelRoutingOptions()));

        var run = new TestRunContext("继续推进 Week8.5");
        var candidates = new[]
        {
            new MemoryItem("vec:1", MemoryLayer.LongTerm, "A", DateTimeOffset.UtcNow.AddMinutes(-2)),
            new MemoryItem("vec:2", MemoryLayer.LongTerm, "B", DateTimeOffset.UtcNow.AddMinutes(-1))
        };

        var ranked = await reranker.RerankAsync(run, "query", candidates, 2, CancellationToken.None);

        Assert.Equal("vec:1", ranked[0].Id);
        Assert.Equal("vec:2", ranked[1].Id);
        Assert.Contains(run.Events, e => e.Type == "rerank_skipped");
    }

    private sealed class TestTextGenerationService : ITextGenerationService
    {
        private readonly string _output;

        public TestTextGenerationService(string output)
        {
            _output = output;
        }

        public int CallCount { get; private set; }

        public Task<string> GenerateAsync(TextGenerationRequest request, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_output);
        }
    }

    private sealed class TestRunContext : IRunContext
    {
        public TestRunContext(string userInput)
        {
            RunId = Guid.NewGuid().ToString("N");
            ConversationId = "conv-rerank";
            UserInput = userInput;
        }

        public string RunId { get; }
        public string ConversationId { get; }
        public string UserInput { get; }
        public Dictionary<string, object> ConversationState { get; } = new();
        public CancellationToken CancellationToken => CancellationToken.None;
        public List<TestEvent> Events { get; } = [];

        public ValueTask EmitAsync(string type, object payload, CancellationToken ct = default)
        {
            Events.Add(new TestEvent(type, payload));
            return ValueTask.CompletedTask;
        }
    }

    private sealed record TestEvent(string Type, object Payload);
}
