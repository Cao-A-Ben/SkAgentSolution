using SKAgent.Application.Memory;
using SKAgent.Application.Modeling;
using SKAgent.Application.Retrieval;
using SKAgent.Core.Memory;
using SKAgent.Core.Memory.Facts;
using SKAgent.Core.Memory.LongTerm;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Memory.Working;
using SKAgent.Core.Modeling;
using SKAgent.Core.Personas;
using SKAgent.Core.Retrieval;
using SKAgent.Core.Runtime;
using Xunit;

namespace SKAgent.Tests.Memory;

public sealed class MemoryOrchestratorTests
{
    [Fact]
    public async Task BuildAsync_ShouldCreateProgressSummaryCandidate_ForProgressSummaryRecall()
    {
        var recentHistory = new TestRecentConversationHistory(
            new TurnRecord
            {
                At = DateTimeOffset.UtcNow.AddMinutes(-20),
                UserInput = "我想继续推进 Week8.x 的 persona 和建议质量。",
                AssistantOutput = "我们可以先补 coach persona。",
                Goal = "推进 Week8.x"
            },
            new TurnRecord
            {
                At = DateTimeOffset.UtcNow.AddMinutes(-10),
                UserInput = "我刚刚说了什么？",
                AssistantOutput = "你刚刚说了“你好啊”。",
                Goal = "处理回忆问题"
            });

        var longTerm = new TestLongTermMemory(
            new MemoryItem(
                "vec:1",
                MemoryLayer.LongTerm,
                "相关历史用户原话：Week8.5 把 planner / chat / daily / embedding 的模型选择收敛为配置驱动",
                DateTimeOffset.UtcNow.AddMinutes(-5),
                0.9,
                new Dictionary<string, string> { ["route"] = "vector", ["role"] = "user" }),
            new MemoryItem(
                "vec:2",
                MemoryLayer.LongTerm,
                "相关历史助手回复摘要：Daily Suggestion 的幂等已经升级到 conversation 维度",
                DateTimeOffset.UtcNow.AddMinutes(-4),
                0.8,
                new Dictionary<string, string> { ["route"] = "vector", ["role"] = "assistant" }));

        var reranker = new RetrievalReranker(
            new TestTextGenerationService("C1,C2"),
            new DefaultModelRouter(new ModelRoutingOptions()));

        var orchestrator = new MemoryOrchestrator(
            recentHistory,
            new TestShortTermMemory(),
            new TestWorkingMemoryStore(),
            longTerm,
            new TestFactStore(),
            new TestQueryRewriter(),
            new TestProgressMilestoneProvider(),
            reranker,
            new RetrievalFusion(),
            new MemoryBudgeter());

        var run = new TestRunContext("总结一下我最近在 Week8 到 Week8.5 主要推进了什么");
        run.ConversationState["retrieval_intents"] = RetrievalIntent.Recall;
        run.ConversationState["retrieval_plan"] = new RetrievalPlan(
            Routes: new[] { RetrievalRoute.RecentHistory, RetrievalRoute.ShortTerm, RetrievalRoute.Working, RetrievalRoute.Profile, RetrievalRoute.Facts, RetrievalRoute.Vector },
            Budgets: new Dictionary<RetrievalRoute, int>
            {
                [RetrievalRoute.RecentHistory] = 1800,
                [RetrievalRoute.ShortTerm] = 4000,
                [RetrievalRoute.Working] = 3000,
                [RetrievalRoute.Profile] = 1200,
                [RetrievalRoute.Facts] = 2000,
                [RetrievalRoute.Vector] = 4000
            },
            TopK: new Dictionary<RetrievalRoute, int>
            {
                [RetrievalRoute.RecentHistory] = 6,
                [RetrievalRoute.Vector] = 8
            },
            RewriteQuery: false,
            NeedClarification: false,
            SafetyPolicy: null,
            Rationale: "test");

        await orchestrator.BuildAsync(
            run,
            new PersonaOptions
            {
                Name = "coach",
                SystemPrompt = "You are a coach.",
                PlannerHint = string.Empty,
                Policy = new PersonaPolicy()
            },
            run.UserInput,
            CancellationToken.None);

        var candidate = Assert.IsType<string>(run.ConversationState["recall_answer_candidate"]);
        Assert.Contains("最近你主要推进了：", candidate);
        Assert.DoesNotContain("你刚刚说了", candidate);
        Assert.DoesNotContain("我想继续推进", candidate);
        Assert.DoesNotContain("转换处理", candidate);
        Assert.Contains("persona 切换与 coach 风格能力", candidate);
        Assert.Contains("Daily Suggestion 的生成、幂等与内容优化", candidate);
        Assert.Contains("planner / chat / daily / embedding 的模型路由收敛", candidate);
    }

    [Fact]
    public async Task BuildAsync_ShouldPreferMilestoneThemes_WhenAssistantFollowUpPollutesProgressRecall()
    {
        var recentHistory = new TestRecentConversationHistory(
            new TurnRecord
            {
                At = DateTimeOffset.UtcNow.AddMinutes(-12),
                UserInput = "总结一下我最近在 Week8 到 Week8.5 主要推进了什么",
                AssistantOutput = "好的，请分享一下你在 Week8 到 Week8.5 期间主要推进的任务和进展情况，这样我能更好地帮助你总结。具体来说，你在这些周里完成了哪些关键任务或目标？",
                Goal = "总结近期进展"
            },
            new TurnRecord
            {
                At = DateTimeOffset.UtcNow.AddMinutes(-30),
                UserInput = "我想继续推进 Week8.5。",
                AssistantOutput = "我们继续推进 Week8.5。",
                Goal = "推进 Week8.5"
            });

        var longTerm = new TestLongTermMemory(
            new MemoryItem(
                "vec:theme:1",
                MemoryLayer.LongTerm,
                "相关历史用户原话：Week8 完成了 persona 切换与 coach 风格能力的验收",
                DateTimeOffset.UtcNow.AddMinutes(-9),
                0.9,
                new Dictionary<string, string> { ["route"] = "vector", ["role"] = "user" }),
            new MemoryItem(
                "vec:theme:2",
                MemoryLayer.LongTerm,
                "相关历史助手回复摘要：Daily Suggestion 的生成、幂等与内容优化已经完成一轮收口",
                DateTimeOffset.UtcNow.AddMinutes(-8),
                0.8,
                new Dictionary<string, string> { ["route"] = "vector", ["role"] = "assistant" }),
            new MemoryItem(
                "vec:theme:3",
                MemoryLayer.LongTerm,
                "相关历史用户原话：Week8.5 把 planner / chat / daily / embedding 的模型路由收敛为配置驱动",
                DateTimeOffset.UtcNow.AddMinutes(-7),
                0.7,
                new Dictionary<string, string> { ["route"] = "vector", ["role"] = "user" }),
            new MemoryItem(
                "vec:theme:4",
                MemoryLayer.LongTerm,
                "相关历史助手回复摘要：rerank 接入与检索链路增强已经完成验收",
                DateTimeOffset.UtcNow.AddMinutes(-6),
                0.6,
                new Dictionary<string, string> { ["route"] = "vector", ["role"] = "assistant" }),
            new MemoryItem(
                "vec:theme:5",
                MemoryLayer.LongTerm,
                "相关历史助手回复摘要：最近你主要推进了：Week8 到 Week8.5 的推进收口。",
                DateTimeOffset.UtcNow.AddMinutes(-5),
                0.5,
                new Dictionary<string, string> { ["route"] = "vector", ["role"] = "assistant" }));

        var reranker = new RetrievalReranker(
            new TestTextGenerationService("C1,C2,C3,C4"),
            new DefaultModelRouter(new ModelRoutingOptions()));

        var orchestrator = new MemoryOrchestrator(
            recentHistory,
            new TestShortTermMemory(),
            new TestWorkingMemoryStore(),
            longTerm,
            new TestFactStore(),
            new TestQueryRewriter(),
            new TestProgressMilestoneProvider(),
            reranker,
            new RetrievalFusion(),
            new MemoryBudgeter());

        var run = new TestRunContext("总结一下我最近在 Week8 到 Week8.5 主要推进了什么");
        run.ConversationState["retrieval_intents"] = RetrievalIntent.Recall;
        run.ConversationState["retrieval_plan"] = new RetrievalPlan(
            Routes: new[] { RetrievalRoute.RecentHistory, RetrievalRoute.ShortTerm, RetrievalRoute.Working, RetrievalRoute.Profile, RetrievalRoute.Facts, RetrievalRoute.Vector },
            Budgets: new Dictionary<RetrievalRoute, int>
            {
                [RetrievalRoute.RecentHistory] = 1800,
                [RetrievalRoute.ShortTerm] = 4000,
                [RetrievalRoute.Working] = 3000,
                [RetrievalRoute.Profile] = 1200,
                [RetrievalRoute.Facts] = 2000,
                [RetrievalRoute.Vector] = 4000
            },
            TopK: new Dictionary<RetrievalRoute, int>
            {
                [RetrievalRoute.RecentHistory] = 6,
                [RetrievalRoute.Vector] = 8
            },
            RewriteQuery: false,
            NeedClarification: false,
            SafetyPolicy: null,
            Rationale: "test");

        await orchestrator.BuildAsync(
            run,
            new PersonaOptions
            {
                Name = "coach",
                SystemPrompt = "You are a coach.",
                PlannerHint = string.Empty,
                Policy = new PersonaPolicy()
            },
            run.UserInput,
            CancellationToken.None);

        var candidate = Assert.IsType<string>(run.ConversationState["recall_answer_candidate"]);
        Assert.Contains("最近你主要推进了：", candidate);
        Assert.DoesNotContain("好的，请分享一下", candidate);
        Assert.DoesNotContain("这样我能更好地帮助你总结", candidate);
        Assert.DoesNotContain("你在这些周里完成了哪些关键任务或目标", candidate);
        Assert.DoesNotContain("你刚刚说了", candidate);
        Assert.DoesNotContain("最近你主要推进了：Week8 到 Week8.5 的推进收口", candidate);
        Assert.Contains("persona 切换与 coach 风格能力", candidate);
        Assert.Contains("Daily Suggestion 的生成、幂等与内容优化", candidate);
        Assert.Contains("planner / chat / daily / embedding 的模型路由收敛", candidate);
        Assert.DoesNotContain("Week8 到 Week8.5 的推进收口", candidate);
    }

    [Fact]
    public async Task BuildAsync_ShouldBackfillProgressThemes_FromProvider_WhenMemoryIsTooGeneric()
    {
        var recentHistory = new TestRecentConversationHistory(
            new TurnRecord
            {
                At = DateTimeOffset.UtcNow.AddMinutes(-12),
                UserInput = "总结一下我最近在 Week8 到 Week8.5 主要推进了什么",
                AssistantOutput = "好的，请分享一下你在 Week8 到 Week8.5 期间主要推进的任务和进展情况。",
                Goal = "总结近期进展"
            });

        var longTerm = new TestLongTermMemory(
            new MemoryItem(
                "vec:generic:1",
                MemoryLayer.LongTerm,
                "相关历史助手回复摘要：最近你主要推进了：Week8 到 Week8.5 的推进收口。",
                DateTimeOffset.UtcNow.AddMinutes(-5),
                0.5,
                new Dictionary<string, string> { ["route"] = "vector", ["role"] = "assistant" }));

        var reranker = new RetrievalReranker(
            new TestTextGenerationService("C1"),
            new DefaultModelRouter(new ModelRoutingOptions()));

        var orchestrator = new MemoryOrchestrator(
            recentHistory,
            new TestShortTermMemory(),
            new TestWorkingMemoryStore(),
            longTerm,
            new TestFactStore(),
            new TestQueryRewriter(),
            new TestProgressMilestoneProvider(
                "persona 切换与 coach 风格能力",
                "Daily Suggestion 的生成、幂等与内容优化",
                "planner / chat / daily / embedding 的模型路由收敛"),
            reranker,
            new RetrievalFusion(),
            new MemoryBudgeter());

        var run = new TestRunContext("总结一下我最近在 Week8 到 Week8.5 主要推进了什么");
        run.ConversationState["retrieval_intents"] = RetrievalIntent.Recall;
        run.ConversationState["retrieval_plan"] = new RetrievalPlan(
            Routes: new[] { RetrievalRoute.RecentHistory, RetrievalRoute.Vector },
            Budgets: new Dictionary<RetrievalRoute, int>
            {
                [RetrievalRoute.RecentHistory] = 1800,
                [RetrievalRoute.Vector] = 4000
            },
            TopK: new Dictionary<RetrievalRoute, int>
            {
                [RetrievalRoute.RecentHistory] = 6,
                [RetrievalRoute.Vector] = 8
            },
            RewriteQuery: false,
            NeedClarification: false,
            SafetyPolicy: null,
            Rationale: "test");

        await orchestrator.BuildAsync(
            run,
            new PersonaOptions
            {
                Name = "coach",
                SystemPrompt = "You are a coach.",
                PlannerHint = string.Empty,
                Policy = new PersonaPolicy()
            },
            run.UserInput,
            CancellationToken.None);

        var candidate = Assert.IsType<string>(run.ConversationState["recall_answer_candidate"]);
        Assert.Contains("persona 切换与 coach 风格能力", candidate);
        Assert.Contains("Daily Suggestion 的生成、幂等与内容优化", candidate);
        Assert.Contains("planner / chat / daily / embedding 的模型路由收敛", candidate);
        Assert.DoesNotContain("Week8 到 Week8.5 的推进收口", candidate);
    }

    private sealed class TestRunContext : IRunContext
    {
        public TestRunContext(string userInput)
        {
            RunId = Guid.NewGuid().ToString("N");
            ConversationId = "conv-progress";
            UserInput = userInput;
        }

        public string RunId { get; }
        public string ConversationId { get; }
        public string UserInput { get; }
        public Dictionary<string, object> ConversationState { get; } = new();
        public CancellationToken CancellationToken => CancellationToken.None;

        public ValueTask EmitAsync(string type, object payload, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    private sealed class TestRecentConversationHistory : IRecentConversationHistory
    {
        private readonly IReadOnlyList<TurnRecord> _turns;

        public TestRecentConversationHistory(params TurnRecord[] turns)
        {
            _turns = turns;
        }

        public Task<IReadOnlyList<TurnRecord>> GetRecentAsync(string conversationId, int take, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<TurnRecord>)_turns.Take(take).ToList());
    }

    private sealed class TestShortTermMemory : IShortTermMemory
    {
        public Task AppendAsync(string conversationId, TurnRecord record, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearAsync(string conversationId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TurnRecord>> GetRecentAsync(string conversationId, int take, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<TurnRecord>)Array.Empty<TurnRecord>());
    }

    private sealed class TestWorkingMemoryStore : IWorkingMemoryStore
    {
        public Task<IReadOnlyList<MemoryItem>> ListAsync(string conversationId, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<MemoryItem>)Array.Empty<MemoryItem>());

        public Task AppendAsync(string conversationId, MemoryItem item, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearAsync(string conversationId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TestLongTermMemory : ILongTermMemory
    {
        private readonly IReadOnlyList<MemoryItem> _items;

        public TestLongTermMemory(params MemoryItem[] items)
        {
            _items = items;
        }

        public Task<IReadOnlyList<MemoryItem>> QueryAsync(MemoryQuery query, CancellationToken ct = default)
            => Task.FromResult(_items);

        public Task<LongTermUpsertResult> UpsertAsync(IReadOnlyList<LongTermMemoryWrite> writes, CancellationToken ct = default)
            => Task.FromResult(new LongTermUpsertResult(0, 0));
    }

    private sealed class TestFactStore : IFactStore
    {
        public Task<IReadOnlyList<FactRecord>> ListAsync(string conversationId, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<FactRecord>)Array.Empty<FactRecord>());

        public Task<FactConflictDecision> UpsertAsync(string conversationId, FactRecord fact, CancellationToken ct = default)
            => Task.FromResult(new FactConflictDecision(
                fact.Key,
                FactConflictAction.Upserted,
                ExistingValue: null,
                IncomingValue: fact.Value,
                Reason: "test"));

        public Task ClearAsync(string conversationId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TestQueryRewriter : IQueryRewriter
    {
        public Task<IReadOnlyList<string>> RewriteAsync(string query, RetrievalIntent intents, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<string>)new[] { query });
    }

    private sealed class TestTextGenerationService : ITextGenerationService
    {
        private readonly string _output;

        public TestTextGenerationService(string output)
        {
            _output = output;
        }

        public Task<string> GenerateAsync(TextGenerationRequest request, CancellationToken ct = default)
            => Task.FromResult(_output);
    }

    private sealed class TestProgressMilestoneProvider : IProgressMilestoneProvider
    {
        private readonly IReadOnlyList<string> _milestones;

        public TestProgressMilestoneProvider(params string[] milestones)
        {
            _milestones = milestones;
        }

        public Task<IReadOnlyList<string>> GetMilestonesAsync(string conversationId, string userInput, CancellationToken ct = default)
            => Task.FromResult(_milestones);
    }
}
