using Xunit;
using SKAgent.Application.Retrieval;
using SKAgent.Core.Retrieval;

namespace SKAgent.Tests.Retrieval;

public sealed class IntentRouterTests
{
    private readonly IntentRouter _router = new();

    [Fact]
    public async Task RouteAsync_ShouldMarkRecallAndTool_WhenInputHasBothSignals()
    {
        var result = await _router.RouteAsync("帮我回忆上次目标并查下今天新闻", null);

        Assert.True(result.Intents.HasFlag(RetrievalIntent.Recall));
        Assert.True(result.Intents.HasFlag(RetrievalIntent.ToolNeeded));
        Assert.Contains(RetrievalRoute.RecentHistory, result.Plan.Routes);
        Assert.Contains(RetrievalRoute.Vector, result.Plan.Routes);
        Assert.Contains(RetrievalRoute.Tool, result.Plan.Routes);
    }

    [Fact]
    public async Task RouteAsync_ShouldMarkRecall_ForRecentConversationQuestion()
    {
        var result = await _router.RouteAsync("我刚刚说了什么？", null);

        Assert.True(result.Intents.HasFlag(RetrievalIntent.Recall));
        Assert.Equal(RetrievalRoute.RecentHistory, result.Plan.Routes[0]);
        Assert.Contains(RetrievalRoute.Vector, result.Plan.Routes);
        Assert.Contains(RetrievalRoute.Facts, result.Plan.Routes);
        Assert.DoesNotContain("fallback_chitchat", result.Signals);
    }

    [Fact]
    public async Task RouteAsync_ShouldMarkRecall_ForProgressSummaryQuestion()
    {
        var result = await _router.RouteAsync("总结一下我最近在 Week8 到 Week8.5 主要推进了什么", null);

        Assert.True(result.Intents.HasFlag(RetrievalIntent.Recall));
        Assert.Contains(RetrievalRoute.RecentHistory, result.Plan.Routes);
        Assert.Contains(RetrievalRoute.Vector, result.Plan.Routes);
        Assert.Contains(RetrievalRoute.Facts, result.Plan.Routes);
        Assert.DoesNotContain("fallback_chitchat", result.Signals);
    }

    [Fact]
    public async Task RouteAsync_ShouldMarkHealthSensitive_WhenInputHasHealthRiskKeywords()
    {
        var result = await _router.RouteAsync("怀孕期间针灸有什么禁忌", null);

        Assert.True(result.Intents.HasFlag(RetrievalIntent.HealthSensitive));
        Assert.Equal("health_sensitive_v1", result.Plan.SafetyPolicy);
        Assert.Contains(RetrievalRoute.Facts, result.Plan.Routes);
    }
}
