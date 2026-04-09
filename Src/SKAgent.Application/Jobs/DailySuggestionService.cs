using System.Security.Cryptography;
using System.Text;
using SkAgent.Core.Prompt;
using SKAgent.Application.Persona;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Modeling;
using SKAgent.Core.Observability;
using SKAgent.Core.Profile;
using SKAgent.Core.Retrieval;
using SKAgent.Core.Runtime;
using SKAgent.Core.Suggestions;

namespace SKAgent.Application.Jobs;

public sealed class DailySuggestionService
{
    private readonly IRunPreparationService _preparationService;
    private readonly IShortTermMemory _shortTermMemory;
    private readonly IUserProfileStore _profileStore;
    private readonly PersonaManager _personaManager;
    private readonly ISuggestionStore _suggestionStore;
    private readonly IConversationScopeResolver _conversationScopeResolver;
    private readonly ITextGenerationService _textGenerationService;
    private readonly IRunEventLogFactory _runEventLogFactory;
    private readonly DailySuggestionOptions _options;

    public DailySuggestionService(
        IRunPreparationService preparationService,
        IShortTermMemory shortTermMemory,
        IUserProfileStore profileStore,
        PersonaManager personaManager,
        ISuggestionStore suggestionStore,
        IConversationScopeResolver conversationScopeResolver,
        ITextGenerationService textGenerationService,
        IRunEventLogFactory runEventLogFactory,
        DailySuggestionOptions options)
    {
        _preparationService = preparationService;
        _shortTermMemory = shortTermMemory;
        _profileStore = profileStore;
        _personaManager = personaManager;
        _suggestionStore = suggestionStore;
        _conversationScopeResolver = conversationScopeResolver;
        _textGenerationService = textGenerationService;
        _runEventLogFactory = runEventLogFactory;
        _options = options;
    }

    public Task<IReadOnlyList<SuggestionRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default)
        => _suggestionStore.ListRecentAsync(take, ct);

    public async Task<DailySuggestionResult> GenerateIfMissingAsync(
        DateOnly? date = null,
        string? personaName = null,
        string? conversationId = null,
        CancellationToken ct = default)
    {
        var options = _options;
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        var targetPersona = string.IsNullOrWhiteSpace(personaName) ? options.PersonaName : personaName.Trim();

        var existing = await _suggestionStore.GetAsync(targetDate, targetPersona, ct).ConfigureAwait(false);
        if (existing is not null)
            return new DailySuggestionResult(existing, Created: false);

        var resolvedConversationId = await ResolveConversationIdAsync(options, conversationId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(resolvedConversationId))
            throw new InvalidOperationException("No active conversation could be resolved for daily suggestion generation.");

        var eventLog = _runEventLogFactory.CreateDailySuggestionLog(targetDate);
        var run = new BackgroundRunContext(
            resolvedConversationId,
            "请生成今天的一条建议。",
            ct,
            eventLog.Sink);

        await run.EmitAsync("daily_job_started", new
        {
            date = targetDate.ToString("yyyy-MM-dd"),
            personaName = targetPersona,
            conversationId = resolvedConversationId
        }, ct).ConfigureAwait(false);

        try
        {
            var recentTurns = await _shortTermMemory.GetRecentAsync(resolvedConversationId, options.RecentTurnTake, ct).ConfigureAwait(false);
            run.ConversationState["recent_turns"] = recentTurns;

            var profile = await _profileStore.GetAsync(resolvedConversationId, ct).ConfigureAwait(false);
            run.ConversationState["profile"] = profile;

            var selectedPersona = _personaManager.GetOrSelect(run.RunId, resolvedConversationId, targetPersona);
            run.ConversationState["persona"] = selectedPersona.Persona;
            run.ConversationState["personaName"] = selectedPersona.Persona.Name;

            await run.EmitAsync("persona_selected", new
            {
                conversationId = resolvedConversationId,
                personaName = selectedPersona.Persona.Name,
                source = selectedPersona.Source,
                reason = selectedPersona.Reason
            }, ct).ConfigureAwait(false);

            run.ConversationState["retrieval_intents"] = RetrievalIntent.None;
            run.ConversationState["retrieval_plan"] = BuildDailyRetrievalPlan();

            await _preparationService.PrepareAsync(run, ct).ConfigureAwait(false);

            var prompt = await _preparationService
                .GetPromptAsync(run, PromptTarget.Daily, BuildDailyTask(targetDate), options.CharBudget, ct)
                .ConfigureAwait(false);

            var suggestion = (await _textGenerationService.GenerateAsync(new TextGenerationRequest(
                prompt.System,
                prompt.User,
                ModelPurpose.Daily,
                Temperature: 0.4,
                TopP: 0.9), ct).ConfigureAwait(false)).Trim();

            var record = new SuggestionRecord(
                Date: targetDate,
                Suggestion: suggestion,
                RunId: run.RunId,
                ConversationId: resolvedConversationId,
                PersonaName: selectedPersona.Persona.Name,
                ProfileHash: ComputeProfileHash(profile),
                PromptHash: prompt.Hash,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                EventLogPath: eventLog.Path);

            await _suggestionStore.SaveAsync(record, ct).ConfigureAwait(false);

            await run.EmitAsync("suggestion_saved", new
            {
                date = targetDate.ToString("yyyy-MM-dd"),
                runId = run.RunId,
                hash = prompt.Hash,
                chars = suggestion.Length,
                eventLogPath = eventLog.Path
            }, ct).ConfigureAwait(false);

            await run.EmitAsync("run_completed", new
            {
                finalOutput = suggestion,
                stepCount = 0,
                retries = new Dictionary<int, int>(),
                toolCallCount = 0
            }, ct).ConfigureAwait(false);

            await run.EmitAsync("daily_job_finished", new
            {
                date = targetDate.ToString("yyyy-MM-dd"),
                runId = run.RunId,
                created = true
            }, ct).ConfigureAwait(false);

            return new DailySuggestionResult(record, Created: true);
        }
        catch (Exception ex)
        {
            await run.EmitAsync("daily_job_failed", new
            {
                date = targetDate.ToString("yyyy-MM-dd"),
                runId = run.RunId,
                error = ex.Message
            }, ct).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<string?> ResolveConversationIdAsync(DailySuggestionOptions options, string? conversationId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(conversationId))
            return conversationId.Trim();

        if (!string.IsNullOrWhiteSpace(options.ConversationId))
            return options.ConversationId.Trim();

        if (!options.UseLatestConversation)
            return null;

        return await _conversationScopeResolver.ResolveAsync(ct).ConfigureAwait(false);
    }

    private static RetrievalPlan BuildDailyRetrievalPlan()
    {
        var routes = new[]
        {
            RetrievalRoute.RecentHistory,
            RetrievalRoute.ShortTerm,
            RetrievalRoute.Working,
            RetrievalRoute.Profile,
            RetrievalRoute.Facts,
            RetrievalRoute.Vector
        };

        var budgets = new Dictionary<RetrievalRoute, int>
        {
            [RetrievalRoute.RecentHistory] = 1600,
            [RetrievalRoute.ShortTerm] = 3200,
            [RetrievalRoute.Working] = 2000,
            [RetrievalRoute.Profile] = 1200,
            [RetrievalRoute.Facts] = 1800,
            [RetrievalRoute.Vector] = 3200
        };

        var topK = new Dictionary<RetrievalRoute, int>
        {
            [RetrievalRoute.RecentHistory] = 6,
            [RetrievalRoute.ShortTerm] = 12,
            [RetrievalRoute.Working] = 10,
            [RetrievalRoute.Profile] = 6,
            [RetrievalRoute.Facts] = 8,
            [RetrievalRoute.Vector] = 6
        };

        return new RetrievalPlan(
            Routes: routes,
            Budgets: budgets,
            TopK: topK,
            RewriteQuery: true,
            NeedClarification: false,
            SafetyPolicy: null,
            Rationale: "daily_suggestion_latest_conversation");
    }

    private static string BuildDailyTask(DateOnly date)
        => $"""
请基于已有 persona、最近会话、用户画像与长期记忆，为用户生成 {date:yyyy-MM-dd} 的一条每日建议。
要求：
1. 输出中文，1 到 3 句。
2. 建议要具体、可执行、温和，不要空泛鼓励。
3. 优先结合最近对话里已经出现的目标、偏好或问题。
4. 如果信息不足，就给出稳妥且通用的一条建议，但不要编造用户事实。
5. 直接输出建议正文，不要加标题，不要解释推理过程。
""";

    private static string ComputeProfileHash(IReadOnlyDictionary<string, string> profile)
    {
        if (profile.Count == 0)
            return "empty";

        var normalized = string.Join("|", profile
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
