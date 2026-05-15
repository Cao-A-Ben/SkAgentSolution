using System.Text.Json;
using SkAgent.Runtime;
using SkAgent.Runtime.Execution;
using SKAgent.Application.Reflection;
using SKAgent.Core.Agent;
using SKAgent.Core.Memory;
using SKAgent.Core.Observability;
using SKAgent.Core.Personas;
using SKAgent.Core.Planning;
using SKAgent.Core.Routing;
using SKAgent.Core.Tools.Abstractions;
using SKAgent.Runtime;
using Xunit;

namespace SKAgent.Tests.Execution;

public sealed class PlanExecutorRepairTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldEmitRepairPlan_WhenToolFailsAfterRetries()
    {
        var reviewer = new ReflectionAgent();
        var executor = new PlanExecutor(
            new StubStepRouter(),
            new AlwaysTimeoutToolInvoker(),
            reviewer,
            reviewer);
        var sink = new CapturingRunEventSink();
        var run = new AgentRunContext(
            new AgentContext
            {
                Input = "run the tool",
                CancellationToken = CancellationToken.None
            },
            conversationId: "conv-repair-1",
            runId: "run-repair-1",
            eventSink: sink);

        run.ConversationState["persona"] = new PersonaOptions { Name = "default" };
        run.ConversationState["memoryBundle"] = new MemoryBundle([], [], [], []);
        run.SetPlan(new AgentPlan
        {
            Goal = "test repair",
            Steps =
            [
                new PlanStep
                {
                    Order = 1,
                    Kind = PlanStepKind.Tool,
                    Target = "debug.fail",
                    ArgumentsJson = "{}"
                }
            ]
        });

        await executor.ExecuteAsync(run);

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.Contains(sink.Events, evt => evt.Type == "repair_plan_created");
        Assert.Contains(sink.Events, evt => evt.Type == "repair_step_started");
        Assert.Contains(sink.Events, evt => evt.Type == "repair_step_completed");

        var repairPlan = sink.Events.Single(evt => evt.Type == "repair_plan_created");
        Assert.Equal("tool", repairPlan.Payload.GetProperty("failureSource").GetString());
        Assert.Equal(3, repairPlan.Payload.GetProperty("repairSteps").GetArrayLength());
        Assert.Equal("retry_same_tool_step_with_backoff", repairPlan.Payload.GetProperty("repairSteps")[2].GetProperty("action").GetString());

        var runFailed = sink.Events.Single(evt => evt.Type == "run_failed");
        Assert.Equal("tool", runFailed.Payload.GetProperty("failureSource").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitRepairPlan_WhenAgentStepFails()
    {
        var reviewer = new ReflectionAgent();
        var executor = new PlanExecutor(
            new FailingStepRouter(),
            new AlwaysTimeoutToolInvoker(),
            reviewer,
            reviewer);
        var sink = new CapturingRunEventSink();
        var run = new AgentRunContext(
            new AgentContext
            {
                Input = "run the agent",
                CancellationToken = CancellationToken.None
            },
            conversationId: "conv-repair-2",
            runId: "run-repair-2",
            eventSink: sink);

        run.ConversationState["persona"] = new PersonaOptions { Name = "default" };
        run.ConversationState["memoryBundle"] = new MemoryBundle([], [], [], []);
        run.SetPlan(new AgentPlan
        {
            Goal = "test executor repair",
            Steps =
            [
                new PlanStep
                {
                    Order = 1,
                    Kind = PlanStepKind.Agent,
                    Target = "chat",
                    Instruction = "do work"
                }
            ]
        });

        await executor.ExecuteAsync(run);

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        var repairPlan = sink.Events.Single(evt => evt.Type == "repair_plan_created");
        Assert.Equal("executor", repairPlan.Payload.GetProperty("failureSource").GetString());
        Assert.Equal("freeze_remaining_steps_and_replan", repairPlan.Payload.GetProperty("repairSteps")[2].GetProperty("action").GetString());
    }

    private sealed class StubStepRouter : IStepRouter
    {
        public Task<AgentResult> RouteAsync(AgentContext stepContext, CancellationToken ct = default)
            => Task.FromResult(new AgentResult { Output = "unused", IsSuccess = true });
    }

    private sealed class FailingStepRouter : IStepRouter
    {
        public Task<AgentResult> RouteAsync(AgentContext stepContext, CancellationToken ct = default)
            => Task.FromResult(new AgentResult
            {
                Output = "agent failed",
                IsSuccess = false
            });
    }

    private sealed class AlwaysTimeoutToolInvoker : IToolInvoker
    {
        public Task<ToolResult> InvokeAsync(ToolInvocation invocation, CancellationToken ct)
        {
            var output = JsonDocument.Parse("{}").RootElement.Clone();
            return Task.FromResult(new ToolResult(
                Success: false,
                Output: output,
                Error: new ToolError("tool_timeout", "Tool timeout: debug.fail"),
                Metrics: new ToolMetrics(50)));
        }
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
