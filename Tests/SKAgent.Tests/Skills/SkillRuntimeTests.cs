using SkAgent.Runtime.Planning;
using SKAgent.Application.Prompt;
using SKAgent.Application.Skills;
using SKAgent.Core.Memory;
using SKAgent.Core.Observability;
using SKAgent.Core.Personas;
using SKAgent.Core.Runtime;
using SKAgent.Core.Skills;
using SkAgent.Core.Prompt;
using Xunit;

namespace SKAgent.Tests.Skills;

public sealed class SkillRuntimeTests
{
    [Fact]
    public void SkillRegistry_ShouldResolveDemoSkill()
    {
        var registry = new InMemorySkillRegistry(SkillCatalog.All);

        var skill = registry.GetByName("tech.mcp_demo");

        Assert.NotNull(skill);
        Assert.Equal("Tech MCP Demo", skill!.DisplayName);
        Assert.Contains("mcp.demo_echo", skill.RecommendedTools ?? []);
    }

    [Fact]
    public async Task PromptComposer_ShouldAppendSkillSystemPrompt()
    {
        var run = new FakeRunContext();
        run.ConversationState["skill"] = SkillCatalog.TechMcpDemo;

        var composer = new PromptComposer();
        var prompt = await composer.ComposeAsync(
            run,
            new PersonaOptions
            {
                Name = "default",
                SystemPrompt = "You are helpful.",
                PlannerHint = "Keep the plan short."
            },
            new MemoryBundle([], [], [], []),
            PromptTarget.Chat,
            "Explain the current skill route.",
            4000,
            CancellationToken.None);

        Assert.Contains("Tech MCP Demo skill", prompt.System, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mcp.demo_echo", prompt.System, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultPlanRequestFactory_ShouldMergePersonaAndSkillHints()
    {
        var factory = new DefaultPlanRequestFactory();
        var run = new FakeRunContext
        {
            RunIdValue = "run-skill-1",
            ConversationIdValue = "conv-skill-1",
            UserInputValue = "Use the demo skill"
        };

        run.ConversationState["persona"] = new PersonaOptions
        {
            Name = "default",
            PlannerHint = "Keep the plan short."
        };
        run.ConversationState["skill"] = SkillCatalog.TechMcpDemo;

        var request = factory.Create(run);

        Assert.Contains("Keep the plan short.", request.PlannerHint);
        Assert.Contains("mcp.demo_echo", request.PlannerHint);
    }

    private sealed class FakeRunContext : IRunContext
    {
        public string RunId { get => RunIdValue; }
        public string ConversationId { get => ConversationIdValue; }
        public string UserInput { get => UserInputValue; }
        public string RunIdValue { get; set; } = "run";
        public string ConversationIdValue { get; set; } = "conv";
        public string UserInputValue { get; set; } = "input";
        public Dictionary<string, object> ConversationState { get; } = new(StringComparer.OrdinalIgnoreCase);
        public CancellationToken CancellationToken => CancellationToken.None;

        public ValueTask EmitAsync(string type, object payload, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }
}
