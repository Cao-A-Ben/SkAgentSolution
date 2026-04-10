using Xunit;
using SkAgent.Core.Prompt;
using SKAgent.Application.Jobs;
using SKAgent.Application.Persona;
using SKAgent.Core.Memory;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Modeling;
using SKAgent.Core.Observability;
using SKAgent.Core.Personas;
using SKAgent.Core.Profile;
using SKAgent.Core.Runtime;
using SKAgent.Core.Suggestions;

namespace SKAgent.Tests.Suggestions;

public sealed class DailySuggestionServiceTests
{
    [Fact]
    public async Task GenerateIfMissingAsync_ShouldReuseExistingSuggestion_ForSameDateAndConversation()
    {
        var store = new TestSuggestionStore();
        var textGeneration = new TestTextGenerationService("Drink more water today.");
        var service = new DailySuggestionService(
            new CapturingRunPreparationService(),
            new TestShortTermMemory(),
            new TestUserProfileStore(),
            new PersonaManager(new TestPersonaProvider(), new TestConversationPersonaStore()),
            store,
            new TestConversationScopeResolver("conv-1"),
            textGeneration,
            new TestRunEventLogFactory(),
            new DailySuggestionOptions
            {
                PersonaName = "default",
                UseLatestConversation = true,
                CharBudget = 2000,
                RecentTurnTake = 4
            });

        var date = new DateOnly(2026, 4, 9);

        var first = await service.GenerateIfMissingAsync(date);
        var second = await service.GenerateIfMissingAsync(date);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal("Drink more water today.", first.Record.Suggestion);
        Assert.Equal(first.Record.RunId, second.Record.RunId);
        Assert.Equal(1, textGeneration.CallCount);

        var listed = await service.ListRecentAsync();
        Assert.Single(listed);
        Assert.Equal(first.Record.PromptHash, listed[0].PromptHash);
        Assert.False(string.IsNullOrWhiteSpace(first.Record.EventLogPath));
    }

    [Fact]
    public async Task GenerateIfMissingAsync_ShouldReuseExistingSuggestion_WhenPersonaChangesButConversationMatches()
    {
        var store = new TestSuggestionStore();
        var textGeneration = new TestTextGenerationService("Finish the most concrete next step first.");
        var service = new DailySuggestionService(
            new CapturingRunPreparationService(),
            new TestShortTermMemory(),
            new TestUserProfileStore(),
            new PersonaManager(new TestPersonaProvider(), new TestConversationPersonaStore()),
            store,
            new TestConversationScopeResolver("conv-1"),
            textGeneration,
            new TestRunEventLogFactory(),
            new DailySuggestionOptions
            {
                PersonaName = "default",
                UseLatestConversation = true,
                CharBudget = 2000,
                RecentTurnTake = 4
            });

        var date = new DateOnly(2026, 4, 12);

        var first = await service.GenerateIfMissingAsync(date, personaName: "default", conversationId: "conv-1");
        var second = await service.GenerateIfMissingAsync(date, personaName: "coach", conversationId: "conv-1");

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Record.RunId, second.Record.RunId);
        Assert.Equal("default", second.Record.PersonaName);
        Assert.Equal(1, textGeneration.CallCount);
    }

    [Fact]
    public async Task GenerateIfMissingAsync_ShouldBuildTaskWithProjectFocus_AndFilterMetaRecallTurns()
    {
        const string metaRecallQuestion = "\u6211\u521A\u521A\u8BF4\u4E86\u4EC0\u4E48\uFF1F";
        const string meaningfulInput = "Week8 daily suggestion has been wired up; next I want to improve suggestion quality.";

        var preparation = new CapturingRunPreparationService();
        var service = new DailySuggestionService(
            preparation,
            new TestShortTermMemory(
                new TurnRecord
                {
                    At = DateTimeOffset.UtcNow.AddMinutes(-10),
                    UserInput = metaRecallQuestion,
                    AssistantOutput = "You just asked a recall question.",
                    Goal = "Handle recall"
                },
                new TurnRecord
                {
                    At = DateTimeOffset.UtcNow.AddMinutes(-20),
                    UserInput = meaningfulInput,
                    AssistantOutput = "Let's improve the quality next.",
                    Goal = "Improve Week8 suggestion quality"
                }),
            new TestUserProfileStore(new Dictionary<string, string>
            {
                ["project"] = "SkAgent",
                ["current_focus"] = "Finish Week8 and improve daily suggestion quality"
            }),
            new PersonaManager(new TestPersonaProvider(), new TestConversationPersonaStore()),
            new TestSuggestionStore(),
            new TestConversationScopeResolver("conv-2"),
            new TestTextGenerationService("Work on the Week8 suggestion prompt today."),
            new TestRunEventLogFactory(),
            new DailySuggestionOptions
            {
                PersonaName = "default",
                UseLatestConversation = true,
                CharBudget = 4000,
                RecentTurnTake = 6
            });

        await service.GenerateIfMissingAsync(new DateOnly(2026, 4, 10));

        Assert.NotNull(preparation.LastTask);
        Assert.Contains("Week8", preparation.LastTask);
        Assert.Contains("SkAgent", preparation.LastTask);
        Assert.DoesNotContain(metaRecallQuestion, preparation.LastTask);
        Assert.Contains("Return Chinese only.", preparation.LastTask);
        Assert.Contains("[BEST NEXT STEP CANDIDATE]", preparation.LastTask);
        Assert.Contains("优化 Daily Suggestion 的 prompt", preparation.LastTask);
    }

    [Fact]
    public async Task GenerateIfMissingAsync_ShouldStillBuildConcreteFallbackCandidate_WhenSignalsAreWeak()
    {
        const string metaRecallQuestion = "\u6211\u521A\u521A\u8BF4\u4E86\u4EC0\u4E48\uFF1F";

        var preparation = new CapturingRunPreparationService();
        var service = new DailySuggestionService(
            preparation,
            new TestShortTermMemory(
                new TurnRecord
                {
                    At = DateTimeOffset.UtcNow.AddMinutes(-10),
                    UserInput = metaRecallQuestion,
                    AssistantOutput = "You asked a memory question.",
                    Goal = "Handle recall",
                    Steps =
                    [
                        new StepRecord
                        {
                            Order = 1,
                            Kind = "Tool",
                            Target = "memory.vector",
                            Output = "ok",
                            Status = "Success"
                        }
                    ]
                }),
            new TestUserProfileStore(new Dictionary<string, string>()),
            new PersonaManager(new TestPersonaProvider(), new TestConversationPersonaStore()),
            new TestSuggestionStore(),
            new TestConversationScopeResolver("conv-3"),
            new TestTextGenerationService("Today refine the next actionable step."),
            new TestRunEventLogFactory(),
            new DailySuggestionOptions
            {
                PersonaName = "default",
                UseLatestConversation = true,
                CharBudget = 4000,
                RecentTurnTake = 6
            });

        await service.GenerateIfMissingAsync(new DateOnly(2026, 4, 11));

        Assert.Contains("[BEST NEXT STEP CANDIDATE]", preparation.LastTask);
        Assert.DoesNotContain("[BEST NEXT STEP CANDIDATE]\n- none", preparation.LastTask, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(metaRecallQuestion, preparation.LastTask);
    }

    [Fact]
    public async Task GenerateIfMissingAsync_ShouldInjectCoachStyle_WhenPersonaIsCoach()
    {
        var preparation = new CapturingRunPreparationService();
        var service = new DailySuggestionService(
            preparation,
            new TestShortTermMemory(
                new TurnRecord
                {
                    At = DateTimeOffset.UtcNow.AddMinutes(-20),
                    UserInput = "我想继续推进 Week8.x 的 persona 和建议质量。",
                    AssistantOutput = "我们可以先补 coach persona。",
                    Goal = "推进 Week8.x"
                }),
            new TestUserProfileStore(new Dictionary<string, string>
            {
                ["project"] = "SkAgent",
                ["current_focus"] = "Week8.x persona coach"
            }),
            new PersonaManager(new TestPersonaProvider(), new TestConversationPersonaStore()),
            new TestSuggestionStore(),
            new TestConversationScopeResolver("conv-coach"),
            new TestTextGenerationService("今天先把 Week8.x 的 coach persona 验收跑完，再继续推进下一步。"),
            new TestRunEventLogFactory(),
            new DailySuggestionOptions
            {
                PersonaName = "default",
                UseLatestConversation = true,
                CharBudget = 4000,
                RecentTurnTake = 6
            });

        await service.GenerateIfMissingAsync(new DateOnly(2026, 4, 14), personaName: "coach", conversationId: "conv-coach");

        Assert.Contains("- name: coach", preparation.LastTask);
        Assert.Contains("[PERSONA STYLE]", preparation.LastTask);
        Assert.Contains("Use a coaching tone", preparation.LastTask);
        Assert.Contains("最小可执行动作", preparation.LastTask);
    }

    private sealed class CapturingRunPreparationService : IRunPreparationService
    {
        public string LastTask { get; private set; } = string.Empty;

        public Task PrepareAsync(IRunContext run, CancellationToken ct)
        {
            run.ConversationState["memoryBundle"] = new MemoryBundle([], [], [], []);
            return Task.CompletedTask;
        }

        public Task<ComposedPrompt> GetPromptAsync(IRunContext run, PromptTarget target, string task, int charBudget, CancellationToken ct)
        {
            LastTask = task;
            return Task.FromResult(new ComposedPrompt(
                target,
                "system-daily",
                task,
                "prompt-hash-daily",
                charBudget,
                ["recent-history", "long-term"]));
        }
    }

    private sealed class TestShortTermMemory : IShortTermMemory
    {
        private readonly IReadOnlyList<TurnRecord> _turns;

        public TestShortTermMemory(params TurnRecord[] turns)
        {
            _turns = turns;
        }

        public Task AppendAsync(string conversationId, TurnRecord record, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearAsync(string conversationId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TurnRecord>> GetRecentAsync(string conversationId, int take, CancellationToken ct = default)
            => Task.FromResult(_turns.Take(take).ToList() as IReadOnlyList<TurnRecord>);
    }

    private sealed class TestUserProfileStore : IUserProfileStore
    {
        private readonly Dictionary<string, string> _profile;

        public TestUserProfileStore(Dictionary<string, string>? profile = null)
        {
            _profile = profile ?? new Dictionary<string, string> { ["goal"] = "sleep earlier" };
        }

        public Task<Dictionary<string, string>> GetAsync(string conversationId, CancellationToken ct = default)
            => Task.FromResult(new Dictionary<string, string>(_profile));

        public Task UpsertAsync(string conversationId, Dictionary<string, string> patch, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class TestSuggestionStore : ISuggestionStore
    {
        private readonly Dictionary<string, SuggestionRecord> _store = new(StringComparer.OrdinalIgnoreCase);

        public Task<SuggestionRecord?> GetAsync(DateOnly date, string conversationId, CancellationToken ct = default)
        {
            _store.TryGetValue(Key(date, conversationId), out var record);
            return Task.FromResult(record);
        }

        public Task SaveAsync(SuggestionRecord record, CancellationToken ct = default)
        {
            _store[Key(record.Date, record.ConversationId)] = record;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SuggestionRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SuggestionRecord>>(_store.Values.ToList());

        private static string Key(DateOnly date, string conversationId) => $"{date:yyyy-MM-dd}:{conversationId}";
    }

    private sealed class TestConversationScopeResolver : IConversationScopeResolver
    {
        private readonly string _conversationId;

        public TestConversationScopeResolver(string conversationId)
        {
            _conversationId = conversationId;
        }

        public Task<string?> ResolveAsync(CancellationToken ct = default)
            => Task.FromResult<string?>(_conversationId);
    }

    private sealed class TestTextGenerationService : ITextGenerationService
    {
        private readonly string _output;

        public TestTextGenerationService(string output)
        {
            _output = output;
        }

        public int CallCount { get; private set; }

        public Task<string> GenerateAsync(TextGenerationRequest request, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_output);
        }
    }

    private sealed class TestRunEventLogFactory : IRunEventLogFactory
    {
        public RunEventLogHandle CreateDailySuggestionLog(DateOnly date)
            => new(new NullSink(), $"test-events/{date:yyyyMMdd}.jsonl");
    }

    private sealed class NullSink : IRunEventSink
    {
        public ValueTask WriteAsync(RunEvent evt, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    private sealed class TestPersonaProvider : IPersonaProvider
    {
        private readonly PersonaOptions _defaultPersona = new()
        {
            Name = "default",
            SystemPrompt = "You are a calm assistant.",
            PlannerHint = string.Empty,
            Policy = new PersonaPolicy
            {
                DefaultPersonaName = "default",
                PersistSelection = true,
                AllowSwitch = true
            }
        };

        private readonly PersonaOptions _coachPersona = new()
        {
            Name = "coach",
            SystemPrompt = "You are a coaching assistant.",
            PlannerHint = string.Empty,
            Policy = new PersonaPolicy
            {
                DefaultPersonaName = "default",
                PersistSelection = true,
                AllowSwitch = true
            }
        };

        public IReadOnlyList<PersonaOptions> GetAll() => [_defaultPersona, _coachPersona];

        public PersonaOptions? GetByName(string name)
            => string.Equals(name, _defaultPersona.Name, StringComparison.OrdinalIgnoreCase)
                ? _defaultPersona
                : string.Equals(name, _coachPersona.Name, StringComparison.OrdinalIgnoreCase)
                    ? _coachPersona
                    : null;
    }

    private sealed class TestConversationPersonaStore : IConversationPersonaStore
    {
        private readonly Dictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

        public string? Get(string conversationId)
            => _store.TryGetValue(conversationId, out var personaName) ? personaName : null;

        public void Set(string conversationId, string personaName)
            => _store[conversationId] = personaName;
    }
}
