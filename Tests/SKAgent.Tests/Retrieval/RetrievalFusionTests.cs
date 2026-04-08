using Xunit;
using SKAgent.Application.Retrieval;
using SKAgent.Core.Memory;
using SKAgent.Core.Retrieval;

namespace SKAgent.Tests.Retrieval;

public sealed class RetrievalFusionTests
{
    private readonly RetrievalFusion _fusion = new();

    [Fact]
    public void Fuse_ShouldPreferFactsOverVector_WhenContentDuplicates()
    {
        var now = DateTimeOffset.UtcNow;
        var factItem = new MemoryItem(
            Id: "fact:1",
            Layer: MemoryLayer.LongTerm,
            Content: "name=小明",
            At: now,
            Score: 0.9,
            Metadata: new Dictionary<string, string> { ["route"] = "facts" });

        var vectorItem = new MemoryItem(
            Id: "vec:1",
            Layer: MemoryLayer.LongTerm,
            Content: "name=小明",
            At: now.AddDays(-30),
            Score: 0.4,
            Metadata: new Dictionary<string, string> { ["route"] = "vector", ["role"] = "user" });

        var input = new RetrievalFusionInput(
            ItemsByRoute: new Dictionary<RetrievalRoute, IReadOnlyList<MemoryItem>>
            {
                [RetrievalRoute.Facts] = [factItem],
                [RetrievalRoute.Vector] = [vectorItem]
            },
            Budgets: new Dictionary<RetrievalRoute, int>
            {
                [RetrievalRoute.Facts] = 1000,
                [RetrievalRoute.Vector] = 1000
            });

        var result = _fusion.Fuse(input);

        Assert.Single(result.LongTerm);
        Assert.Equal("fact:1", result.LongTerm[0].Id);
        Assert.True(result.DedupeCount >= 1);
    }

    [Fact]
    public void Fuse_ShouldPreferRecentUserRecall_OverVectorAssistantSummary()
    {
        var now = DateTimeOffset.UtcNow;
        var recentUser = new MemoryItem(
            Id: "rh:user:1",
            Layer: MemoryLayer.ShortTerm,
            Content: "最近用户原话：你好啊",
            At: now,
            Score: 1.0,
            Metadata: new Dictionary<string, string>
            {
                ["route"] = "recent_history",
                ["role"] = "user"
            });

        var vectorAssistant = new MemoryItem(
            Id: "vec:assistant:1",
            Layer: MemoryLayer.LongTerm,
            Content: "相关历史助手回复摘要：你好啊",
            At: now.AddMinutes(-1),
            Score: 0.8,
            Metadata: new Dictionary<string, string>
            {
                ["route"] = "vector",
                ["role"] = "assistant"
            });

        var input = new RetrievalFusionInput(
            ItemsByRoute: new Dictionary<RetrievalRoute, IReadOnlyList<MemoryItem>>
            {
                [RetrievalRoute.RecentHistory] = [recentUser],
                [RetrievalRoute.Vector] = [vectorAssistant]
            },
            Budgets: new Dictionary<RetrievalRoute, int>
            {
                [RetrievalRoute.RecentHistory] = 1000,
                [RetrievalRoute.Vector] = 1000
            });

        var result = _fusion.Fuse(input);

        Assert.Single(result.RecentHistory);
        Assert.Equal("rh:user:1", result.RecentHistory[0].Id);
        Assert.Empty(result.LongTerm);
    }

    [Fact]
    public void Fuse_ShouldClipByBudget()
    {
        var item1 = new MemoryItem("s1", MemoryLayer.ShortTerm, new string('a', 12), DateTimeOffset.UtcNow);
        var item2 = new MemoryItem("s2", MemoryLayer.ShortTerm, new string('b', 12), DateTimeOffset.UtcNow);

        var input = new RetrievalFusionInput(
            ItemsByRoute: new Dictionary<RetrievalRoute, IReadOnlyList<MemoryItem>>
            {
                [RetrievalRoute.ShortTerm] = [item1, item2]
            },
            Budgets: new Dictionary<RetrievalRoute, int>
            {
                [RetrievalRoute.ShortTerm] = 15
            });

        var result = _fusion.Fuse(input);
        Assert.Single(result.ShortTerm);
    }
}
