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
            Metadata: new Dictionary<string, string> { ["route"] = "vector" });

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
