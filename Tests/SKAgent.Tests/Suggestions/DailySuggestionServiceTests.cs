using Xunit;
using SkAgent.Core.Prompt;
using SKAgent.Application.Jobs;
using SKAgent.Application.Persona;
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
    public async Task GenerateIfMissingAsync_ShouldReuseExistingSuggestion_ForSameDateAndPersona()
    {
        var store = new TestSuggestionStore();
        var textGeneration = new TestTextGenerationService("今天记得多喝水。\n");
        var service = new DailySuggestionService(
            new TestRunPreparationService(),
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
        Assert.Equal("今天记得多喝水。", first.Record.Suggestion);
        Assert.Equal(first.Record.RunId, second.Record.RunId);
        Assert.Equal(1, textGeneration.CallCount);

        var listed = await service.ListRecentAsync();
        Assert.Single(listed);
        Assert.Equal(first.Record.PromptHash, listed[0].PromptHash);
        Assert.False(string.IsNullOrWhiteSpace(first.Record.EventLogPath));
    }

    private sealed class TestRunPreparationService : IRunPreparationService
    {
        public Task PrepareAsync(IRunContext run, CancellationToken ct)
        {
            run.ConversationState["memoryBundle"] = new SKAgent.Core.Memory.MemoryBundle([], [], [], []);
            return Task.CompletedTask;
        }

        public Task<ComposedPrompt> GetPromptAsync(IRunContext run, PromptTarget target, string task, int charBudget, CancellationToken ct)
            => Task.FromResult(new ComposedPrompt(
                target,
                "system-daily",
                task,
                "prompt-hash-daily",
                charBudget,
                ["recent-history", "long-term"]));
    }

    private sealed class TestShortTermMemory : IShortTermMemory
    {
        public Task AppendAsync(string conversationId, TurnRecord record, CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearAsync(string conversationId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TurnRecord>> GetRecentAsync(string conversationId, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TurnRecord>>([]);
    }

    private sealed class TestUserProfileStore : IUserProfileStore
    {
        public Task<Dictionary<string, string>> GetAsync(string conversationId, CancellationToken ct = default)
            => Task.FromResult(new Dictionary<string, string> { ["goal"] = "早睡" });

        public Task UpsertAsync(string conversationId, Dictionary<string, string> patch, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class TestSuggestionStore : ISuggestionStore
    {
        private readonly Dictionary<string, SuggestionRecord> _store = new(StringComparer.OrdinalIgnoreCase);

        public Task<SuggestionRecord?> GetAsync(DateOnly date, string personaName, CancellationToken ct = default)
        {
            _store.TryGetValue(Key(date, personaName), out var record);
            return Task.FromResult(record);
        }

        public Task SaveAsync(SuggestionRecord record, CancellationToken ct = default)
        {
            _store[Key(record.Date, record.PersonaName)] = record;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SuggestionRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SuggestionRecord>>(_store.Values.ToList());

        private static string Key(DateOnly date, string personaName) => $"{date:yyyy-MM-dd}:{personaName}";
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
        private readonly PersonaOptions _persona = new()
        {
            Name = "default",
            SystemPrompt = "你是一个温和的助理。",
            PlannerHint = string.Empty,
            Policy = new PersonaPolicy
            {
                DefaultPersonaName = "default",
                PersistSelection = true,
                AllowSwitch = true
            }
        };

        public IReadOnlyList<PersonaOptions> GetAll() => [_persona];

        public PersonaOptions? GetByName(string name)
            => string.Equals(name, _persona.Name, StringComparison.OrdinalIgnoreCase) ? _persona : null;
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
