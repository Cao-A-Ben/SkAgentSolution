using SKAgent.Application.Modeling;
using SKAgent.Core.Modeling;
using Xunit;

namespace SKAgent.Tests.Modeling;

public sealed class DefaultModelRouterTests
{
    [Fact]
    public void Select_ShouldReturnConfiguredRoute_WhenSectionExists()
    {
        var router = new DefaultModelRouter(new ModelRoutingOptions
        {
            Planner = new ModelRouteOptions
            {
                Provider = "openai-compatible",
                Model = "gpt-4.1-mini",
                Reason = "planner_configured"
            }
        });

        var selection = router.Select(ModelPurpose.Planner);

        Assert.Equal(ModelPurpose.Planner, selection.Purpose);
        Assert.Equal("openai-compatible", selection.Provider);
        Assert.Equal("gpt-4.1-mini", selection.Model);
        Assert.Equal("planner_configured", selection.Reason);
    }

    [Fact]
    public void Select_ShouldFallbackToDefaults_WhenRouteIsMissing()
    {
        var router = new DefaultModelRouter(new ModelRoutingOptions());

        var planner = router.Select(ModelPurpose.Planner);
        var chat = router.Select(ModelPurpose.Chat);
        var daily = router.Select(ModelPurpose.Daily);
        var embedding = router.Select(ModelPurpose.Embedding);

        Assert.Equal("gpt-4o-mini", planner.Model);
        Assert.Equal("gpt-4o", chat.Model);
        Assert.Equal("gpt-4o-mini", daily.Model);
        Assert.Equal("hash-embedding-v1-128", embedding.Model);
    }
}
