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
        Assert.Equal(2, decision.RepairSteps.Count);
        Assert.Equal("inspect_failure_context", decision.RepairSteps[0].Id);
        Assert.Equal("tool_repair_recommendation", decision.RepairSteps[1].Id);
        Assert.Equal(RepairStepStatus.Planned, decision.RepairSteps[1].Status);
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
        Assert.Contains(decision.RepairSteps, step => step.Action == "rebuild_plan");
    }
}
