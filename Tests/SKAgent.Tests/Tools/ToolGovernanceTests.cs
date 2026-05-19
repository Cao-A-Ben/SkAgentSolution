using System.Text.Json;
using SKAgent.Application.Reflection;
using SKAgent.Application.Tools.Governance;
using SKAgent.Application.Tools.Invoker;
using SKAgent.Application.Tools.Registry;
using SKAgent.Core.Agent;
using SKAgent.Core.Memory;
using SKAgent.Core.Observability;
using SKAgent.Core.Personas;
using SKAgent.Core.Planning;
using SKAgent.Core.Routing;
using SKAgent.Core.Tools.Abstractions;
using SKAgent.Infrastructure.Tools.Adapters;
using SKAgent.Runtime;
using SkAgent.Runtime.Execution;
using Xunit;

namespace SKAgent.Tests.Tools;

public sealed class ToolGovernanceTests
{
    [Fact]
    public void ToolRegistry_ShouldHideBlockedExternalTools_FromPlannerCatalog()
    {
        var policy = CreatePolicy(allowlistedExternalTools: ["mcp.demo_echo"]);
        var registry = new ToolRegistry(policy);

        registry.Register(CreateTool("string.upper"));
        registry.Register(CreateTool("mcp.demo_echo", tags: ["external", "mcp"]));
        registry.Register(CreateTool("mcp.blocked_echo", tags: ["external", "mcp"]));

        var descriptors = registry.List();

        Assert.Contains(descriptors, tool => tool.Name == "string.upper");
        Assert.Contains(descriptors, tool => tool.Name == "mcp.demo_echo");
        Assert.DoesNotContain(descriptors, tool => tool.Name == "mcp.blocked_echo");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitExternalCallBlocked_WhenExternalToolIsNotAllowlisted()
    {
        var policy = CreatePolicy();
        var registry = new ToolRegistry(policy);
        registry.Register(CreateTool("mcp.blocked_echo", tags: ["external", "mcp"]));

        var reviewer = new ReflectionAgent();
        var executor = new PlanExecutor(
            new StubStepRouter(),
            new ToolInvoker(registry, policy),
            reviewer,
            reviewer,
            registry,
            policy);

        var sink = new CapturingRunEventSink();
        var run = CreateRun("run-week12-blocked", sink);
        run.SetPlan(new AgentPlan
        {
            Goal = "blocked external tool",
            Steps =
            [
                new PlanStep
                {
                    Order = 1,
                    Kind = PlanStepKind.Tool,
                    Target = "mcp.blocked_echo",
                    ArgumentsJson = "{\"query\":\"ping\"}"
                }
            ]
        });

        await executor.ExecuteAsync(run);

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.Contains(sink.Events, evt => evt.Type == "external_call_blocked");
        Assert.DoesNotContain(sink.Events, evt => evt.Type == "external_call_started");
        Assert.DoesNotContain(sink.Events, evt => evt.Type == "external_call_finished");

        var repairPlan = sink.Events.Single(evt => evt.Type == "repair_plan_created");
        Assert.Equal("tool", repairPlan.Payload.GetProperty("failureSource").GetString());
        Assert.Equal("update_allowlist_or_replan", repairPlan.Payload.GetProperty("repairSteps")[2].GetProperty("action").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitExternalCallAudit_WhenExternalToolIsAllowlisted()
    {
        var policy = CreatePolicy(allowlistedExternalTools: ["mcp.demo_echo"]);
        var registry = new ToolRegistry(policy);
        registry.Register(CreateTool("mcp.demo_echo", tags: ["external", "mcp"]));

        var reviewer = new ReflectionAgent();
        var executor = new PlanExecutor(
            new StubStepRouter(),
            new ToolInvoker(registry, policy),
            reviewer,
            reviewer,
            registry,
            policy);

        var sink = new CapturingRunEventSink();
        var run = CreateRun("run-week12-allowed", sink);
        run.SetPlan(new AgentPlan
        {
            Goal = "allowed external tool",
            Steps =
            [
                new PlanStep
                {
                    Order = 1,
                    Kind = PlanStepKind.Tool,
                    Target = "mcp.demo_echo",
                    ArgumentsJson = "{\"query\":\"echo this\"}"
                }
            ]
        });

        await executor.ExecuteAsync(run);

        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Contains(sink.Events, evt => evt.Type == "external_call_started");
        Assert.Contains(sink.Events, evt => evt.Type == "external_call_finished");

        var finished = sink.Events.Single(evt => evt.Type == "external_call_finished");
        Assert.True(finished.Payload.GetProperty("success").GetBoolean());
        Assert.Equal("mcp.demo_echo", finished.Payload.GetProperty("toolName").GetString());
    }

    private static ConfiguredToolAccessPolicy CreatePolicy(string[]? allowlistedExternalTools = null)
        => new(new ToolPolicyOptions
        {
            AllowAllExternalTools = false,
            AllowedExternalTools = allowlistedExternalTools ?? [],
            ExternalTags = ["external", "mcp"]
        });

    private static ITool CreateTool(string name, string[]? tags = null)
        => new FunctionTool(
            new ToolDescriptor(
                Name: name,
                Description: "test tool",
                InputSchema: new ToolParameterSchema(
                    "object",
                    new Dictionary<string, ToolFieldSchema>
                    {
                        ["query"] = new ToolFieldSchema("string", "query")
                    },
                    new[] { "query" }),
                Tags: tags),
            (args, ct) =>
            {
                var query = args.TryGetProperty("query", out var queryValue)
                    ? queryValue.GetString()
                    : null;
                var output = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    ok = true,
                    echoedQuery = query
                })).RootElement.Clone();
                return Task.FromResult(output);
            });

    private static AgentRunContext CreateRun(string runId, IRunEventSink sink)
    {
        var run = new AgentRunContext(
            new AgentContext
            {
                Input = "run tool",
                CancellationToken = CancellationToken.None
            },
            conversationId: $"conv-{runId}",
            runId: runId,
            eventSink: sink);

        run.ConversationState["persona"] = new PersonaOptions { Name = "default" };
        run.ConversationState["memoryBundle"] = new MemoryBundle([], [], [], []);
        return run;
    }

    private sealed class StubStepRouter : IStepRouter
    {
        public Task<AgentResult> RouteAsync(AgentContext stepContext, CancellationToken ct = default)
            => Task.FromResult(new AgentResult { Output = "unused", IsSuccess = true });
    }

    private sealed class CapturingRunEventSink : IRunEventSink
    {
        public List<RunEvent> Events { get; } = [];

        public ValueTask WriteAsync(RunEvent evt, CancellationToken ct = default)
        {
            Events.Add(evt);
            return ValueTask.CompletedTask;
        }
    }
}
