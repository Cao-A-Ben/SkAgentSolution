using SkAgent.Core.Prompt;
using SkAgent.Runtime;
using SkAgent.Runtime.Execution;
using SkAgent.Runtime.Runtime;
using SKAgent.Application.Memory;
using SKAgent.Application.Memory.Chunker;
using SKAgent.Application.Reflection;
using SKAgent.Core.Agent;
using SKAgent.Core.Embedding;
using SKAgent.Core.Memory;
using SKAgent.Core.Memory.Facts;
using SKAgent.Core.Memory.LongTerm;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Observability;
using SKAgent.Core.Personas;
using SKAgent.Core.Planning;
using SKAgent.Core.Profile;
using SKAgent.Core.Reflection;
using SKAgent.Core.Runtime;
using SKAgent.Core.Routing;
using Xunit;

namespace SKAgent.Tests.Runtime;

public sealed class AgentRuntimeServiceRepairTests
{
    [Fact]
    public async Task RunAsync_ShouldEmitMemoryRepairPlan_WhenPreparationFails()
    {
        var sink = new CapturingRunEventSink();
        var runtime = CreateRuntime(
            reviewer: new ReflectionAgent(),
            planner: new StubPlanner(),
            prep: new ThrowingRunPreparationService(
                prepareException: new InvalidOperationException("memory route unavailable")));

        var run = await runtime.RunAsync(
            conversationId: "conv-memory-1",
            input: "continue week11",
            runId: "run-memory-1",
            eventSink: sink,
            ct: CancellationToken.None);

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        var repairPlan = sink.Events.Single(evt => evt.Type == "repair_plan_created");
        Assert.Equal("memory", repairPlan.Payload.GetProperty("failureSource").GetString());
        Assert.Equal("fallback_to_recent_history_only", repairPlan.Payload.GetProperty("repairSteps")[2].GetProperty("action").GetString());
        Assert.Contains(sink.Events, evt => evt.Type == "run_failed");
    }

    [Fact]
    public async Task RunAsync_ShouldEmitPlannerRepairPlan_WhenPlannerFails()
    {
        var sink = new CapturingRunEventSink();
        var runtime = CreateRuntime(
            reviewer: new ReflectionAgent(),
            planner: new ThrowingPlanner(new InvalidOperationException("planner json invalid")),
            prep: new ThrowingRunPreparationService());

        var run = await runtime.RunAsync(
            conversationId: "conv-planner-1",
            input: "continue week11",
            runId: "run-planner-1",
            eventSink: sink,
            ct: CancellationToken.None);

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        var repairPlan = sink.Events.Single(evt => evt.Type == "repair_plan_created");
        Assert.Equal("planner", repairPlan.Payload.GetProperty("failureSource").GetString());
        Assert.Equal("rebuild_plan_from_simplified_input", repairPlan.Payload.GetProperty("repairSteps")[2].GetProperty("action").GetString());
        Assert.Contains(sink.Events, evt => evt.Type == "run_failed");
    }

    private static AgentRuntimeService CreateRuntime(
        ReflectionAgent reviewer,
        IPlanner planner,
        IRunPreparationService prep)
    {
        var executor = new PlanExecutor(
            new StubStepRouter(),
            new StubToolInvoker(),
            reviewer,
            reviewer);

        return new AgentRuntimeService(
            stm: new StubShortTermMemory(),
            planner: planner,
            executor: executor,
            profileStore: new StubProfileStore(),
            profileExtractor: new StubProfileExtractor(),
            factStore: new StubFactStore(),
            longTermMemoryService: new LongTermMemoryService(
                new StubLongTermMemory(),
                new StubEmbeddingProvider(),
                new MemoryExtractor(),
                new TurnChunker()),
            prep: prep,
            planRequestFactory: new StubPlanRequestFactory(),
            reviewer: reviewer);
    }

    private sealed class ThrowingRunPreparationService : IRunPreparationService
    {
        private readonly Exception? _prepareException;

        public ThrowingRunPreparationService(Exception? prepareException = null)
        {
            _prepareException = prepareException;
        }

        public Task PrepareAsync(IRunContext run, CancellationToken ct)
        {
            if (_prepareException is not null)
                throw _prepareException;

            run.ConversationState["persona"] = new PersonaOptions { Name = "default" };
            run.ConversationState["memoryBundle"] = new MemoryBundle([], [], [], []);
            return Task.CompletedTask;
        }

        public Task<ComposedPrompt> GetPromptAsync(IRunContext run, PromptTarget target, string task, int charBudget, CancellationToken ct)
            => Task.FromResult(new ComposedPrompt(
                PromptTarget.Planner,
                "system",
                "user",
                "hash",
                charBudget,
                ["recent-history"]));
    }

    private sealed class ThrowingPlanner : IPlanner
    {
        private readonly Exception _exception;

        public ThrowingPlanner(Exception exception)
        {
            _exception = exception;
        }

        public Task<AgentPlan> CreatePlanAsync(IPlanner.PlanRequest request)
            => throw _exception;
    }

    private sealed class StubPlanner : IPlanner
    {
        public Task<AgentPlan> CreatePlanAsync(IPlanner.PlanRequest request)
            => Task.FromResult(new AgentPlan { Goal = "unused", Steps = [] });
    }

    private sealed class StubPlanRequestFactory : IPlanRequestFactory
    {
        public IPlanner.PlanRequest Create(IRunContext run)
            => new(
                RunId: run.RunId,
                ConversationId: run.ConversationId,
                UserInput: run.UserInput,
                RecentTurns: [],
                Profile: new Dictionary<string, string>(),
                PlannerHint: "test");
    }

    private sealed class StubShortTermMemory : IShortTermMemory
    {
        public Task AppendAsync(string conversationId, TurnRecord record, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<TurnRecord>> GetRecentAsync(string conversationId, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TurnRecord>>([]);

        public Task ClearAsync(string conversationId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubProfileStore : IUserProfileStore
    {
        public Task<Dictionary<string, string>> GetAsync(string conversationId, CancellationToken ct = default)
            => Task.FromResult(new Dictionary<string, string>());

        public Task UpsertAsync(string conversationId, Dictionary<string, string> patch, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubProfileExtractor : IProfileExtractor
    {
        public Dictionary<string, string> ExtractPatch(string userInput) => [];
    }

    private sealed class StubFactStore : IFactStore
    {
        public Task<IReadOnlyList<FactRecord>> ListAsync(string conversationId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FactRecord>>([]);

        public Task<FactConflictDecision> UpsertAsync(string conversationId, FactRecord fact, CancellationToken ct = default)
            => Task.FromResult(new FactConflictDecision(fact.Key, FactConflictAction.Skipped, null, fact.Value, "not used"));

        public Task ClearAsync(string conversationId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubLongTermMemory : ILongTermMemory
    {
        public Task<IReadOnlyList<MemoryItem>> QueryAsync(MemoryQuery query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MemoryItem>>([]);

        public Task<LongTermUpsertResult> UpsertAsync(IReadOnlyList<LongTermMemoryWrite> writes, CancellationToken ct = default)
            => Task.FromResult(new LongTermUpsertResult(0, writes.Count));
    }

    private sealed class StubEmbeddingProvider : IEmbeddingProvider
    {
        public string ModelId => "test-embedding";

        public Task<float[]> CreateEmbeddingAsync(string text, CancellationToken ct = default)
            => Task.FromResult(Array.Empty<float>());
    }

    private sealed class StubStepRouter : IStepRouter
    {
        public Task<AgentResult> RouteAsync(AgentContext stepContext, CancellationToken ct = default)
            => Task.FromResult(new AgentResult { Output = "unused", IsSuccess = true });
    }

    private sealed class StubToolInvoker : SKAgent.Core.Tools.Abstractions.IToolInvoker
    {
        public Task<SKAgent.Core.Tools.Abstractions.ToolResult> InvokeAsync(SKAgent.Core.Tools.Abstractions.ToolInvocation invocation, CancellationToken ct)
            => throw new NotImplementedException();
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
