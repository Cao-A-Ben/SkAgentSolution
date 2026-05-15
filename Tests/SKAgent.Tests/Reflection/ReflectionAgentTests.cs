using SKAgent.Application.Reflection;
using SKAgent.Core.Reflection;
using Xunit;

namespace SKAgent.Tests.Reflection;

public sealed class ReflectionAgentTests
{
    [Fact]
    public async Task ReviewFailureAsync_ShouldBuildToolRepairPlan()
    {
        var agent = new ReflectionAgent();

        var decision = await agent.ReviewFailureAsync(
            new FailureReviewRequest(
                Run: new ReflectionContext(
                    RunId: "run-1",
                    ConversationId: "conv-1",
                    UserInput: "test",
                    PersonaId: "default",
                    LastStep: null,
                    LastTool: null,
                    RetryCounts: new Dictionary<int, int>()),
                FailureSource: FailureSource.Tool,
                FailurePhase: "tool",
                Error: new ErrorInfo("tool_timeout", "Tool timeout: debug.fail"),
                Attempt: 3,
                MaxRetries: 3),
            CancellationToken.None);

        Assert.True(decision.ShouldRepair);
        Assert.Equal(FailureSource.Tool, decision.FailureSource);
        Assert.Equal("timeout", decision.FailureCategory);
        Assert.Equal(3, decision.RepairSteps.Count);
        Assert.Equal("inspect_failure_context", decision.RepairSteps[0].Id);
        Assert.Equal("stabilize_tool_runtime", decision.RepairSteps[1].Id);
        Assert.Equal("retry_same_tool_step_with_backoff", decision.RepairSteps[2].Action);
        Assert.Equal(RepairStepStatus.Planned, decision.RepairSteps[2].Status);
    }

    [Fact]
    public async Task ReviewFailureAsync_ShouldBuildPlannerRepairPlan()
    {
        var agent = new ReflectionAgent();

        var decision = await agent.ReviewFailureAsync(
            new FailureReviewRequest(
                Run: new ReflectionContext(
                    RunId: "run-2",
                    ConversationId: "conv-2",
                    UserInput: "test",
                    PersonaId: "coach",
                    LastStep: null,
                    LastTool: null,
                    RetryCounts: new Dictionary<int, int>()),
                FailureSource: FailureSource.Planner,
                FailurePhase: "planner_plan",
                Error: new ErrorInfo("planner_exception", "Planner output was not valid JSON."),
                Attempt: 0,
                MaxRetries: 0),
            CancellationToken.None);

        Assert.True(decision.ShouldRepair);
        Assert.Equal(FailureSource.Planner, decision.FailureSource);
        Assert.Equal("planner_failure", decision.FailureCategory);
        Assert.Contains("planner", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(decision.RepairSteps, step => step.Action == "rebuild_plan_from_simplified_input");
    }

    [Fact]
    public async Task ReviewFailureAsync_ShouldSuggestRegistryValidation_ForMissingTool()
    {
        var agent = new ReflectionAgent();

        var decision = await agent.ReviewFailureAsync(
            new FailureReviewRequest(
                Run: new ReflectionContext(
                    RunId: "run-3",
                    ConversationId: "conv-3",
                    UserInput: "test",
                    PersonaId: "default",
                    LastStep: null,
                    LastTool: null,
                    RetryCounts: new Dictionary<int, int>()),
                FailureSource: FailureSource.Tool,
                FailurePhase: "tool",
                Error: new ErrorInfo("tool_not_found", "Tool was not found."),
                Attempt: 0,
                MaxRetries: 3,
                FailedStep: new SKAgent.Core.Planning.PlanStep { Order = 1, Kind = SKAgent.Core.Planning.PlanStepKind.Tool, Target = "ghost.tool" },
                FailedOrder: 1),
            CancellationToken.None);

        Assert.Contains(decision.RepairSteps, step => step.Action == "validate_tool_registry");
        Assert.Contains(decision.RepairSteps, step => step.Action == "replan_with_registered_tool");
    }
}
